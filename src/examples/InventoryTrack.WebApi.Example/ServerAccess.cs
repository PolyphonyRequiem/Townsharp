public record ServerAccess(Uri Uri, string AccessToken)
{
    public static ServerAccess None => new(new Uri("ws://none.goaway"), String.Empty);
}