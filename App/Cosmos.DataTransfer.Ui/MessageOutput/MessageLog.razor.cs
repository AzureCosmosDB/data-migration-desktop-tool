using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Cosmos.DataTransfer.Ui.MessageOutput;

public partial class MessageLog
{
    [Inject]
    public IJSRuntime JS { get; set; } = null!;

    [Parameter]
    public IEnumerable<LogMessage>? Messages { get; set; }

    private ElementReference _scrollAreaRef;

    private IJSObjectReference? _module;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Cosmos.DataTransfer.Ui/MessageOutput/MessageLog.razor.js");
        await base.OnAfterRenderAsync(firstRender);
    }

    protected override Task OnParametersSetAsync()
    {
        if (_module != null)
        {
            Task.Delay(50).ContinueWith(async t =>
            {
                await _module.InvokeVoidAsync("scrollToEnd", new object[] { _scrollAreaRef });
            });
        }

        return base.OnParametersSetAsync();
    }
}

public class LogMessage
{
    public static LogMessage Error(string text) => new(text, MessageType.Error);
    public static LogMessage Warn(string text) => new(text, MessageType.Warning);
    public static LogMessage App(string text) => new(text, MessageType.AppLog);
    public static LogMessage Data(string text) => new(text, MessageType.Data);

    public LogMessage(string? text, MessageType type = MessageType.Message)
    {
        Text = text;
        Type = type;
        if (type == MessageType.AppLog && text != null)
        {
            if (text.StartsWith("info: "))
                Type = MessageType.AppLogInfo;
            else if (text.StartsWith("warn: "))
                Type = MessageType.AppLogWarning;
            else if (text.StartsWith("fail: "))
                Type = MessageType.AppLogError;
        }
    }

    public MessageType Type { get; set; }
    public string? Text { get; set; }
    public DateTime Time { get; } = DateTime.Now;
}

public enum MessageType
{
    Message,
    AppLog,
    AppLogInfo,
    AppLogWarning,
    AppLogError,
    Error,
    Warning,
    Data,
}