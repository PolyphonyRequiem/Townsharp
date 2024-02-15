using System.Text.Json;
using System.Text.Json.Nodes;

namespace Townsharp.Infrastructure.WebApi;

/// <summary>
/// Represents the result of a WebApi request.
/// </summary>
/// <typeparam name="TResult">The type of the result.</typeparam>
public class WebApiResult<TResult>
{
    private static JsonSerializerOptions serializerOptions = new JsonSerializerOptions
    {
        TypeInfoResolver = WebApiSerializerContext.Default
    };

    private string rawResponse;

    /// <summary>
    /// Gets a value indicating whether the request was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message, if any.
    /// </summary>
    public string ErrorMessage { get; init; }

    private readonly Lazy<TResult> lazyContent;

    /// <summary>
    /// Gets the content of the response.
    /// </summary>
    public TResult Content => this.lazyContent.Value;

    private readonly Lazy<JsonNode> lazyRawJson;

    /// <summary>
    /// Gets the raw JSON response.
    /// </summary>
    public JsonNode RawJson => this.lazyRawJson.Value;

    private WebApiResult(string rawResponse, string? errorMessage = default)
    {
        this.rawResponse = rawResponse;
        this.IsSuccess = errorMessage == default;
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

    internal static WebApiResult<TResult> Success(string rawResponse) => new WebApiResult<TResult>(rawResponse);

    internal static WebApiResult<TResult> Failure(string rawResponse, string errorMessage) => new WebApiResult<TResult>(rawResponse, errorMessage);
}
