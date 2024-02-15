namespace Townsharp.Consoles;

public interface IConsoleEventFactory<TConsoleEvent, TEventData>
    where TEventData : notnull
    where TConsoleEvent : ConsoleEvent
{

    TConsoleEvent Create(TEventData eventData);
}
