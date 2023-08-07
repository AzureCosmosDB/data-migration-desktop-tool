namespace Cosmos.DataTransfer.Ui.Common;

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
