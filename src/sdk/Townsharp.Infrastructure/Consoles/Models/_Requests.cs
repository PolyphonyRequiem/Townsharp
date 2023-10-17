namespace Townsharp.Infrastructure.Consoles.Models;

internal record CommandRequestMessage (int id, string content);

internal record AuthenticationRequest: CommandRequestMessage
{
    internal AuthenticationRequest(string authToken) : base(0, authToken)
    {
    }
}