namespace Townsharp.Infrastructure.Subscriptions.Models;

public record InfrastructureError(string message, string connectionId, string requestId);