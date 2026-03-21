// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace NOC.Web.Hubs;

/// <summary>
/// Background service that subscribes to Redis Pub/Sub channels
/// and forwards events to SignalR groups.
/// This bridges the gap between Workers (separate processes) and the SignalR hub.
/// </summary>
public class SignalRBridge(
    IConnectionMultiplexer redis,
    IHubContext<NocHub> hub,
    ILogger<SignalRBridge> logger) : BackgroundService
{
    private const string Channel = "signalr:events";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = redis.GetSubscriber();

        await subscriber.SubscribeAsync(RedisChannel.Literal(Channel), async (_, message) =>
        {
            try
            {
                if (message.IsNullOrEmpty) return;
                var evt = JsonSerializer.Deserialize<SignalREvent>((string)message!);
                if (evt is null) return;

                switch (evt.Event)
                {
                    case "MessageReceived":
                        // Send to both inbox and conversation groups
                        if (evt.InboxId is not null)
                            await hub.Clients.Group($"inbox:{evt.InboxId}")
                                .SendAsync("MessageReceived", evt.ConversationId, evt.Payload, stoppingToken);
                        if (evt.ConversationId is not null)
                            await hub.Clients.Group($"conversation:{evt.ConversationId}")
                                .SendAsync("MessageReceived", evt.ConversationId, evt.Payload, stoppingToken);
                        break;

                    case "ConversationAssigned":
                        if (evt.InboxId is not null)
                            await hub.Clients.Group($"inbox:{evt.InboxId}")
                                .SendAsync("ConversationAssigned", evt.ConversationId, evt.AgentId, stoppingToken);
                        break;

                    case "ConversationStatusChanged":
                        if (evt.InboxId is not null)
                            await hub.Clients.Group($"inbox:{evt.InboxId}")
                                .SendAsync("ConversationStatusChanged", evt.ConversationId, evt.Status, stoppingToken);
                        break;
                }

                logger.LogDebug("Forwarded SignalR event {Event} for conversation {ConversationId}", evt.Event, evt.ConversationId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error forwarding SignalR event");
            }
        });

        logger.LogInformation("SignalR bridge listening on Redis channel {Channel}", Channel);

        // Keep running until cancelled
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }
}

public class SignalREvent
{
    public string Event { get; set; } = "";
    public string? InboxId { get; set; }
    public string? ConversationId { get; set; }
    public string? AgentId { get; set; }
    public string? Status { get; set; }
    public JsonElement? Payload { get; set; }
}
