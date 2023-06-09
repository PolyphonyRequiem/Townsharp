﻿using System.Text.Json;

namespace Townsharp.Infrastructure.Subscriptions.Models;

public record EventMessage(long id, string @event, string key, string content, int responseCode)
{
    public static EventMessage None => NoEventMessage.Instance;
}

file record NoEventMessage : EventMessage
{
    public static NoEventMessage Instance = new();

    private NoEventMessage() : base(0, string.Empty, string.Empty, string.Empty, 0)
    {

    }
}

public record SubscriptionEvent
{
    public string EventId { get; init; }
    public long KeyId { get; init; }
    public JsonElement Content { get; init; }

    private SubscriptionEvent(string eventId, long key, JsonElement content)
    {
        this.EventId = eventId;
        this.KeyId = key;
        this.Content = content;
    }

    public static SubscriptionEvent Create(EventMessage eventMessage)
    {
        return new SubscriptionEvent(eventMessage.@event, long.Parse(eventMessage.key), JsonSerializer.Deserialize<JsonElement>(eventMessage.content));
    }
}