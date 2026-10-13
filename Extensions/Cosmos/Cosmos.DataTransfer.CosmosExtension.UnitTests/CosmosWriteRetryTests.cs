using System.Collections.Concurrent;
using System.Dynamic;
using System.Net;
using System.Text;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Polly.Retry;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosWriteRetryTests
{
    public static IEnumerable<object[]> WriteModes =>
        Enum.GetValues<DataWriteMode>().Select(mode => new object[] { mode });

    [DataTestMethod]
    [DataRow(DataWriteMode.Insert)]
    [DataRow(DataWriteMode.Upsert)]
    [DataRow(DataWriteMode.Delete)]
    [DataRow(DataWriteMode.InsertStream)]
    [DataRow(DataWriteMode.UpsertStream)]
    [DataRow(DataWriteMode.DeleteStream)]
    public async Task Write_ThrottledThenSuccessful_Retries(DataWriteMode mode)
    {
        int calls = 0;
        var container = CreateContainer(mode, () => ++calls == 1
            ? HttpStatusCode.TooManyRequests : SuccessStatus(mode));
        var result = await WriteAsync(container.Object, mode);

        Assert.AreEqual(2, calls);
        Assert.AreEqual(1, result.ItemCount);
    }

    [DataTestMethod]
    [DynamicData(nameof(WriteModes))]
    public async Task Write_UsesFreshServiceDelayBeforeFallback(DataWriteMode mode)
    {
        int calls = 0;
        string?[] hints = { "61000", "2", null };
        var responses = new List<TrackedResponse>();
        var payloads = new List<string>();
        var container = CreateContainer(mode, () => ++calls <= hints.Length
            ? HttpStatusCode.TooManyRequests : SuccessStatus(mode),
            () => calls <= hints.Length ? hints[calls - 1] : null, responses, payloads);
        var delays = new List<TimeSpan>();
        var policy = CosmosDataSinkExtension.GetRetryPolicy(3, 200, (attempt, exception) =>
        {
            AssertDisposed(container, responses);
            delays.Add(CosmosDataSinkExtension.GetRetryDelay(exception, attempt, 200));
            return TimeSpan.Zero;
        });

        var result = await WriteAsync(container.Object, mode, policy);

        Assert.AreEqual(4, calls);
        Assert.AreEqual(1, result.ItemCount);
        Assert.AreEqual(TimeSpan.FromSeconds(61), delays[0]);
        Assert.AreEqual(TimeSpan.FromMilliseconds(2), delays[1]);
        Assert.IsTrue(delays[2].TotalMilliseconds >= 800 && delays[2].TotalMilliseconds < 900);
        AssertDisposed(container, responses);
        if (mode is DataWriteMode.InsertStream or DataWriteMode.UpsertStream)
        {
            Assert.AreEqual(4, payloads.Count);
            Assert.IsTrue(payloads.All(p => p == "{\"id\":\"test\",\"value\":\"private-payload\"}"));
            Assert.AreEqual(4, container.Invocations.Select(i => i.Arguments[0]).Distinct().Count());
        }
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("invalid")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("1.5")]
    public void RetryDelay_UnusableHint_UsesBoundedFallback(string? hint)
    {
        var exception = Throttled(hint);
        Assert.AreEqual(hint == "0" ? TimeSpan.Zero : hint == "-1" ? TimeSpan.FromMilliseconds(-1) : null,
            exception.RetryAfter);
        for (int attempt = 1; attempt <= 40; attempt++)
        {
            double baseDelay = Math.Min(30_000, 200 * Math.Pow(2, attempt - 1));
            double delay = CosmosDataSinkExtension.GetRetryDelay(exception, attempt, 200).TotalMilliseconds;
            Assert.IsTrue(delay >= baseDelay && delay <= Math.Min(30_000, baseDelay + 99));
        }
    }

    [DataTestMethod]
    [DataRow(1, 1)]
    [DataRow(200, 40)]
    [DataRow(int.MaxValue, int.MaxValue)]
    public void RetryDelay_LargeSettingsAndCounts_DoNotOverflow(int initialDelay, int attempt)
    {
        double delay = CosmosDataSinkExtension.GetRetryDelay(Throttled(null), attempt, initialDelay).TotalMilliseconds;
        Assert.IsTrue(delay >= 1 && delay <= 30_000);
        if (attempt > 1)
            Assert.AreEqual(30_000d, delay);
    }

    [DataTestMethod]
    [DataRow("1", 1d)]
    [DataRow("61000", 61000d)]
    [DataRow("4294967294", 4294967294d)]
    public void RetryDelay_UsableHint_IsNotShortenedOrJittered(string hint, double expected)
    {
        Assert.AreEqual(TimeSpan.FromMilliseconds(expected),
            CosmosDataSinkExtension.GetRetryDelay(Throttled(hint), 40, int.MaxValue));
    }

    [DataTestMethod]
    [DataRow("4294967295")]
    [DataRow("9223372036854775807")]
    [DataRow("9223372036854775808")]
    [DataRow("9999999999999999999999999999999999999999")]
    public void RetryDelay_UnsupportedServiceDelay_FailsInsteadOfRetryingEarly(string hint)
    {
        var exception = Assert.ThrowsException<NotSupportedException>(() =>
            CosmosDataSinkExtension.GetRetryDelay(Throttled(hint), 1, 200));
        StringAssert.Contains(exception.Message, "refusing to retry early");
    }

    [DataTestMethod]
    [DynamicData(nameof(WriteModes))]
    public async Task Write_ExhaustionAndZeroRetries_AreFiniteAndLogged(DataWriteMode mode)
    {
        foreach (int maxRetries in new[] { 0, 2, 40 })
        {
            int calls = 0;
            int waits = 0;
            var responses = new List<TrackedResponse>();
            var container = CreateContainer(mode, () => { calls++; return HttpStatusCode.TooManyRequests; },
                responses: responses);
            var logger = new RecordingLogger();
            var policy = CosmosDataSinkExtension.GetRetryPolicy(maxRetries, 200, (_, _) =>
            {
                waits++;
                return TimeSpan.Zero;
            });

            var exception = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                WriteAsync(container.Object, mode, policy, logger: logger));

            Assert.AreEqual(HttpStatusCode.TooManyRequests, exception.StatusCode);
            Assert.AreEqual(maxRetries + 1, calls);
            Assert.AreEqual(maxRetries, waits);
            StringAssert.Contains(logger.Messages.Single(), $"Cosmos sink {mode} exhausted {maxRetries}");
            Assert.IsFalse(logger.Messages.Single().Contains("private-payload"));
            Assert.IsTrue(logger.Exceptions.All(e => e == null));
            AssertDisposed(container, responses);
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(WriteModes))]
    public async Task Write_UnsupportedServiceDelay_DoesNotRetryAndDisposesResources(DataWriteMode mode)
    {
        var responses = new List<TrackedResponse>();
        var container = CreateContainer(mode, () => HttpStatusCode.TooManyRequests, () => "4294967295",
            responses: responses);
        var exception = await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
            WriteAsync(container.Object, mode));
        StringAssert.Contains(exception.Message, "refusing to retry early");
        Assert.AreEqual(1, container.Invocations.Count);
        AssertDisposed(container, responses);
    }

    [DataTestMethod]
    [DataRow(DataWriteMode.Insert)]
    [DataRow(DataWriteMode.InsertStream)]
    public async Task Write_UnusableHints_UseFallback(DataWriteMode mode)
    {
        foreach (string? hint in new[] { null, "", "0", "-1", "invalid", "NaN", "1.5" })
        {
            int calls = 0;
            var container = CreateContainer(mode, () => ++calls == 1
                ? HttpStatusCode.TooManyRequests : SuccessStatus(mode), () => hint);
            TimeSpan? selectedDelay = null;
            var policy = CosmosDataSinkExtension.GetRetryPolicy(1, 200, (attempt, exception) =>
            {
                selectedDelay = CosmosDataSinkExtension.GetRetryDelay(exception, attempt, 200);
                return TimeSpan.Zero;
            });
            var result = await WriteAsync(container.Object, mode, policy);
            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, result.ItemCount);
            Assert.IsTrue(selectedDelay >= TimeSpan.FromMilliseconds(200) && selectedDelay < TimeSpan.FromMilliseconds(300));
        }
    }

    [DataTestMethod]
    [DataRow(DataWriteMode.Delete)]
    [DataRow(DataWriteMode.DeleteStream)]
    public async Task Write_ThrottledThenNotFound_CountsDeleteOnce(DataWriteMode mode)
    {
        int calls = 0;
        var container = CreateContainer(mode, () => ++calls == 1
            ? HttpStatusCode.TooManyRequests : HttpStatusCode.NotFound);
        var result = await WriteAsync(container.Object, mode);
        Assert.AreEqual(2, calls);
        Assert.AreEqual(1, result.ItemCount);
    }

    [DataTestMethod]
    [DataRow(DataWriteMode.InsertStream)]
    [DataRow(DataWriteMode.UpsertStream)]
    [DataRow(DataWriteMode.DeleteStream)]
    public async Task Write_StreamSdkThrows429_AlsoRetries(DataWriteMode mode)
    {
        int calls = 0;
        var container = CreateContainer(mode, () => SuccessStatus(mode), onCall: _ =>
        {
            if (++calls == 1)
                throw Throttled("1");
        });
        var result = await WriteAsync(container.Object, mode);
        Assert.AreEqual(2, calls);
        Assert.AreEqual(1, result.ItemCount);
        AssertDisposed(container, new List<TrackedResponse>());
    }

    [DataTestMethod]
    [DynamicData(nameof(WriteModes))]
    public async Task Write_Non429Errors_AreNotRetried(DataWriteMode mode)
    {
        foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized,
                     HttpStatusCode.Forbidden, HttpStatusCode.Conflict, HttpStatusCode.RequestTimeout,
                     HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable })
        {
            var responses = new List<TrackedResponse>();
            var container = CreateContainer(mode, () => status, responses: responses);
            var policy = CosmosDataSinkExtension.GetRetryPolicy(5, 200, (_, _) =>
                throw new AssertFailedException("Non-429 response must not be retried."));
            if (IsStream(mode))
            {
                var result = await WriteAsync(container.Object, mode, policy);
                Assert.AreEqual(status, result.StatusCode);
                Assert.AreEqual(0, result.ItemCount);
            }
            else
            {
                var exception = await Assert.ThrowsExceptionAsync<CosmosException>(() =>
                    WriteAsync(container.Object, mode, policy));
                Assert.AreEqual(status, exception.StatusCode);
            }
            Assert.AreEqual(1, container.Invocations.Count);
            AssertDisposed(container, responses);
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(WriteModes))]
    public async Task Write_NotFound_IsSuccessOnlyForDeletes(DataWriteMode mode)
    {
        var responses = new List<TrackedResponse>();
        var container = CreateContainer(mode, () => HttpStatusCode.NotFound, responses: responses);
        if (IsStream(mode) || mode == DataWriteMode.Delete)
        {
            var result = await WriteAsync(container.Object, mode);
            Assert.AreEqual(mode is DataWriteMode.Delete or DataWriteMode.DeleteStream ? 1 : 0, result.ItemCount);
        }
        else
        {
            await Assert.ThrowsExceptionAsync<CosmosException>(() => WriteAsync(container.Object, mode));
        }
        Assert.AreEqual(1, container.Invocations.Count);
        AssertDisposed(container, responses);
    }

    [DataTestMethod]
    [DynamicData(nameof(WriteModes))]
    public async Task Write_CancellationDuringLongServiceWait_DoesNotRetry(DataWriteMode mode)
    {
        using var cancellation = new CancellationTokenSource();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var responses = new List<TrackedResponse>();
        var container = CreateContainer(mode, () => HttpStatusCode.TooManyRequests, () => "600000",
            responses: responses);
        var policy = CosmosDataSinkExtension.GetRetryPolicy(5, 1, (attempt, exception) =>
        {
            AssertDisposed(container, responses);
            var delay = CosmosDataSinkExtension.GetRetryDelay(exception, attempt, 1);
            waiting.SetResult();
            return delay;
        });
        var write = WriteAsync(container.Object, mode, policy, cancellation.Token);
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await AssertCanceled(write);
        Assert.AreEqual(1, container.Invocations.Count);
        Assert.AreEqual(cancellation.Token, container.Invocations.Single().Arguments.Last());
    }

    [DataTestMethod]
    [DynamicData(nameof(WriteModes))]
    public async Task Write_CancellationBeforeCall_DoesNotCallSdk(DataWriteMode mode)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var container = CreateContainer(mode, () => SuccessStatus(mode));
        await AssertCanceled(WriteAsync(container.Object, mode, cancellationToken: cancellation.Token));
        Assert.AreEqual(0, container.Invocations.Count);
    }

    [DataTestMethod]
    [DynamicData(nameof(WriteModes))]
    public async Task Write_SdkCancellation_PropagatesAndDisposesPayload(DataWriteMode mode)
    {
        using var cancellation = new CancellationTokenSource();
        var container = CreateContainer(mode, () => SuccessStatus(mode), onCall: token =>
        {
            Assert.AreEqual(cancellation.Token, token);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
        });
        await AssertCanceled(WriteAsync(container.Object, mode, cancellationToken: cancellation.Token));
        Assert.AreEqual(1, container.Invocations.Count);
        AssertDisposed(container, new List<TrackedResponse>());
    }

    [DataTestMethod]
    [DynamicData(nameof(WriteModes))]
    public async Task Write_UnexpectedSdkFailure_PropagatesOriginalException(DataWriteMode mode)
    {
        var failure = new InvalidOperationException("private-payload");
        var container = CreateContainer(mode, () => SuccessStatus(mode), onCall: _ => throw failure);
        var logger = new RecordingLogger();
        var actual = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            WriteAsync(container.Object, mode, logger: logger));
        Assert.AreSame(failure, actual);
        Assert.AreEqual(1, container.Invocations.Count);
        Assert.IsFalse(logger.Messages.Single().Contains(failure.Message));
        AssertDisposed(container, new List<TrackedResponse>());
    }

    [DataTestMethod]
    [DataRow(DataWriteMode.Insert)]
    [DataRow(DataWriteMode.InsertStream)]
    public async Task Write_SharedPolicy_IsolatesConcurrentItems(DataWriteMode mode)
    {
        var delays = new ConcurrentBag<TimeSpan>();
        var policy = CosmosDataSinkExtension.GetRetryPolicy(2, 200, (attempt, exception) =>
        {
            delays.Add(CosmosDataSinkExtension.GetRetryDelay(exception, attempt, 200));
            return TimeSpan.Zero;
        });
        using var barrier = new Barrier(8);
        var writes = Enumerable.Range(1, 8).Select(index => Task.Factory.StartNew(async () =>
        {
            int calls = 0;
            var container = CreateContainer(mode, () =>
            {
                if (++calls == 1)
                {
                    Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
                    return HttpStatusCode.TooManyRequests;
                }
                return SuccessStatus(mode);
            }, () => (index * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture));
            var result = await WriteAsync(container.Object, mode, policy);
            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, result.ItemCount);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap()).ToArray();
        await Task.WhenAll(writes);
        CollectionAssert.AreEquivalent(Enumerable.Range(1, 8).Select(i => TimeSpan.FromSeconds(i)).ToArray(),
            delays.ToArray());
    }

    private static async Task AssertCanceled(Task task)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Fail("Expected cancellation.");
        }
        catch (OperationCanceledException)
        {
            Assert.IsTrue(task.IsCanceled);
        }
    }

    private static CosmosException Throttled(string? hint)
    {
        var exception = new CosmosException("private-payload", HttpStatusCode.TooManyRequests, 0, "activity", 0);
        if (hint != null)
            exception.Headers.Set("x-ms-retry-after-ms", hint);
        return exception;
    }

    private static bool IsStream(DataWriteMode mode) =>
        mode is DataWriteMode.InsertStream or DataWriteMode.UpsertStream or DataWriteMode.DeleteStream;

    private static void AssertDisposed(Mock<Container> container, List<TrackedResponse> responses)
    {
        Assert.IsTrue(responses.All(r => r.Disposed && !r.Body.CanRead));
        Assert.IsTrue(container.Invocations.SelectMany(i => i.Arguments).OfType<Stream>().All(s => !s.CanRead));
    }

    private static HttpStatusCode SuccessStatus(DataWriteMode mode) =>
        mode is DataWriteMode.Delete or DataWriteMode.DeleteStream
            ? HttpStatusCode.NoContent : HttpStatusCode.Created;

    private static Mock<Container> CreateContainer(DataWriteMode mode, Func<HttpStatusCode> nextStatus,
        Func<string?>? retryAfter = null, List<TrackedResponse>? responses = null,
        List<string>? payloads = null, Action<CancellationToken>? onCall = null)
    {
        var container = new Mock<Container>(MockBehavior.Strict);
        Task<ResponseMessage> StreamResponse()
        {
            var invocation = container.Invocations.Last();
            if (invocation.Arguments[0] is Stream payload)
            {
                Assert.AreEqual(0L, payload.Position);
                using var reader = new StreamReader(payload, leaveOpen: true);
                payloads?.Add(reader.ReadToEnd());
            }
            onCall?.Invoke((CancellationToken)invocation.Arguments.Last());
            var response = new TrackedResponse(nextStatus());
            var hint = retryAfter == null ? "1" : retryAfter();
            if (hint != null)
                response.Headers.Set("x-ms-retry-after-ms", hint);
            responses?.Add(response);
            return Task.FromResult<ResponseMessage>(response);
        }
        Task<ItemResponse<ExpandoObject>> TypedResponse()
        {
            onCall?.Invoke((CancellationToken)container.Invocations.Last().Arguments.Last());
            var status = nextStatus();
            if ((int)status >= 400)
            {
                var exception = new CosmosException("private-payload", status, 0, "activity", 0);
                var hint = retryAfter == null ? "1" : retryAfter();
                if (hint != null)
                    exception.Headers.Set("x-ms-retry-after-ms", hint);
                return Task.FromException<ItemResponse<ExpandoObject>>(exception);
            }
            var response = new Mock<ItemResponse<ExpandoObject>>();
            response.SetupGet(r => r.StatusCode).Returns(status);
            return Task.FromResult(response.Object);
        }

        switch (mode)
        {
            case DataWriteMode.Insert:
                container.Setup(c => c.CreateItemAsync(It.IsAny<ExpandoObject>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Returns(TypedResponse);
                break;
            case DataWriteMode.Upsert:
                container.Setup(c => c.UpsertItemAsync(It.IsAny<ExpandoObject>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Returns(TypedResponse);
                break;
            case DataWriteMode.Delete:
                container.Setup(c => c.DeleteItemAsync<ExpandoObject>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Returns(TypedResponse);
                break;
            case DataWriteMode.InsertStream:
                container.Setup(c => c.CreateItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Returns(StreamResponse);
                break;
            case DataWriteMode.UpsertStream:
                container.Setup(c => c.UpsertItemStreamAsync(It.IsAny<Stream>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Returns(StreamResponse);
                break;
            case DataWriteMode.DeleteStream:
                container.Setup(c => c.DeleteItemStreamAsync(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Returns(StreamResponse);
                break;
        }
        return container;
    }

    private static Task<CosmosDataSinkExtension.ItemResult> WriteAsync(Container container, DataWriteMode mode,
        AsyncRetryPolicy? policy = null, CancellationToken cancellationToken = default, ILogger? logger = null)
    {
        policy ??= CosmosDataSinkExtension.GetRetryPolicy(2, 2);
        dynamic item = new ExpandoObject();
        item.id = "test";
        item.value = "private-payload";
        return CosmosDataSinkExtension.AddItemAsync(container, item, "/id", mode, policy, logger ?? NullLogger.Instance, cancellationToken);
    }

    private sealed class TrackedResponse : ResponseMessage
    {
        public MemoryStream Body { get; } = new(Encoding.UTF8.GetBytes("{}"));
        public bool Disposed { get; private set; }

        public TrackedResponse(HttpStatusCode status) : base(status) => Content = Body;

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = new();
        public List<Exception?> Exceptions { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }
}
