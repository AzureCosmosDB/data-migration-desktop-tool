using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Cosmos.DataTransfer.Common.UnitTests;

public sealed class HttpTestServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(15));
    private readonly Task responseTask;

    public string Url { get; }

    public HttpTestServer(HttpStatusCode statusCode, string content)
    {
        listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/source.json";
        responseTask = RespondAsync(statusCode, content);
    }

    private async Task RespondAsync(HttpStatusCode statusCode, string content)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, leaveOpen: true);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellation.Token)))
        {
        }

        var body = Encoding.UTF8.GetBytes(content);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(int)statusCode} {statusCode}\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers, cancellation.Token);
        await stream.WriteAsync(body, cancellation.Token);
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        listener.Stop();
        try
        {
            await responseTask;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }
}
