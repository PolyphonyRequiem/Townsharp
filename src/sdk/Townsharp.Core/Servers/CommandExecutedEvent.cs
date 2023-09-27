namespace Townsharp.Servers;

public record CommandExecutedEvent (UserInfo User, string Command);