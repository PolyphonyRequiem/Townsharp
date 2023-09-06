namespace Townsharp.Internals.Consoles;

internal record ConsoleAccess(Uri Uri, string AccessToken)
{
    internal static ConsoleAccess None => new(new Uri("ws://none.goaway"), string.Empty);
}
