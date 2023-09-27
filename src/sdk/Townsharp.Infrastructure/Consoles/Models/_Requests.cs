namespace Townsharp.Infrastructure.Consoles.Models;

public record CommandRequestMessage (int id, string content);

public record AuthenticationRequest: CommandRequestMessage
{
    public AuthenticationRequest(string authToken) : base(0, authToken)
    {
    }
}