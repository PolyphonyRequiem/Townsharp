﻿using Townsharp.Infrastructure.Models;

namespace Townsharp.Infrastructure.WebApi;

public record ConsoleAccess(
    int server_id,
    bool allowed,
    string? token,
    ConsoleConnectionInfo? connection)
{

    public bool IndicatesAccessGranted => this.allowed && !(this.token is null) && !(this.connection is null);

    /// <summary>
    /// Builds the Uri needed for the <see cref="ConsoleClientFactory"/> to create a <see cref="IConsoleClient"/> instance.
    /// </summary>
    /// <returns>The Uri needed for the <see cref="ConsoleClientFactory"/> to create a <see cref="IConsoleClient"/> instance.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Uri BuildConsoleUri()
    {
        if (!this.IndicatesAccessGranted)
        {
            throw new InvalidOperationException("Unable to build a console uri from this ConsoleAccess instance. Either the console is offline, or access was denied.");
        }

        return new Uri($"ws://{this.connection!.address}:{this.connection.websocket_port}");
    }
}

public record ConsoleConnectionInfo(
    string address,
    int websocket_port);

public record InvitedGroupInfo(
    DateTime invited_at,
    GroupServerInfo[] servers,
    int allowed_server_count,
    GroupRoleInfo[] roles,
    int id,
    string? name,
    string? description,
    int member_count,
    DateTime created_at,
    string type,
    string[] tags);