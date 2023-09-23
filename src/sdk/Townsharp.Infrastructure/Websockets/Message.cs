//namespace Townsharp.Infrastructure.Websockets;

//internal record Message(bool IsError, bool IsDisconnected, string Content)
//{
//    public static Message Error(string errorMessage) => new(true, false, errorMessage);

//    public static Message Disconnected(string? reason = default, bool isError = false) => new(isError, true, reason ?? string.Empty);

//    public static Message Text(string content) => new(false, false, content);
//}
