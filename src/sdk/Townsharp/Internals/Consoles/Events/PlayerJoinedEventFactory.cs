using System.Text.Json;

using Townsharp.Consoles;

namespace Townsharp.Internals.Consoles.Events;

public class PlayerJoinedEventFactory : IConsoleEventFactory<PlayerJoinedEvent, JsonDocument>
{
    public PlayerJoinedEvent Create(JsonDocument eventData)
    {
        throw new NotImplementedException();
    }
}
