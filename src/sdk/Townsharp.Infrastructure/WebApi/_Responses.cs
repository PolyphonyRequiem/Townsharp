namespace Townsharp.Infrastructure.WebApi;

public record ConsoleAccess(int server_id, bool allowed, string? token, ConsoleConnectionInfo? connection)
{
    public Uri BuildConsoleUri()
    {
        if (!this.allowed || this.token is null || connection is null)
        {
            throw new InvalidOperationException("Unable to build a console uri from this ConsoleAccess instance. Either the console is offline, or access was denied.");
        }

        return new Uri($"ws://{this.connection.address}:{this.connection.websocket_port}");
    }
}

public record ConsoleConnectionInfo (string address, int websocket_port);