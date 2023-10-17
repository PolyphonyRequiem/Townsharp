using System.Text.Json;
using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.WebApi;

public class WebApiResult<TResult>
{
    private static JsonSerializerOptions serializerOptions = new JsonSerializerOptions
    {
        TypeInfoResolver = WebApiSerializerContext.Default
    };

    private string rawResponse;

    public bool IsSuccess { get; init; }

    public string ErrorMessage { get; init; }

    private readonly Lazy<TResult> lazyContent;

    public TResult Content => this.lazyContent.Value;

    private readonly Lazy<JsonNode> lazyRawJson;

    public JsonNode RawJson => this.lazyRawJson.Value;

    private WebApiResult(string rawResponse, string? errorMessage = default)
    {
        this.rawResponse = rawResponse;
        this.IsSuccess = errorMessage != default;
        this.ErrorMessage = errorMessage ?? String.Empty;

        if (this.IsSuccess)
        {
            this.lazyContent = new Lazy<TResult>(JsonSerializer.Deserialize<TResult>(rawResponse, serializerOptions) ?? throw new InvalidOperationException("Could not deserialize response"));
        }
        else
        {
            this.lazyContent = new Lazy<TResult>(() => throw new InvalidOperationException("Cannot access content on a failed response"));
        }

        this.lazyRawJson = new Lazy<JsonNode>(() => JsonNode.Parse(rawResponse) ?? JsonNode.Parse("{}")!);
    }

    public JsonNode GetRawJson() => JsonNode.Parse(rawResponse)!;

    internal static WebApiResult<TResult> Success(string rawResponse) => new WebApiResult<TResult>(rawResponse);

    internal static WebApiResult<TResult> Failure(string rawResponse, string errorMessage) => new WebApiResult<TResult>(rawResponse, errorMessage);
}
