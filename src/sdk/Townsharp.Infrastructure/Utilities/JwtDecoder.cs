using System.Text;
using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.Utilities;

internal static class JwtDecoder
{
    public static JsonNode DecodeJwtClaims(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException("JWT does not have 3 parts!");
        }

        var payload = parts[1];

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(payload));

        return JsonNode.Parse(payloadJson)!;
    }

    // Helper function to decode from Base64Url
    private static byte[] Base64UrlDecode(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
