// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOC.Shared.Infrastructure.Data;
using NOC.Shared.Infrastructure.Redis;

namespace NOC.Shared.Infrastructure.Outbox;

/// <summary>
/// Background service that polls outbox_events for unpublished events,
/// publishes them to Redis Streams, and marks them as published.
/// </summary>
public class OutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    RedisStreamPublisher redisPublisher,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private const int MaxRetries = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox publisher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error processing outbox batch");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NocDbContext>();

        var events = await db.OutboxEvents
            .Where(e => !e.Published && e.RetryCount < MaxRetries)
            .OrderBy(e => e.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (events.Count == 0) return;

        foreach (var evt in events)
        {
            try
            {
                await redisPublisher.PublishAsync(
                    evt.Stream,
                    evt.EventType,
                    evt.Payload,
                    evt.CorrelationId?.ToString());

                evt.Published = true;
                evt.PublishedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                evt.RetryCount++;
                evt.LastError = ex.Message;
                logger.LogWarning(ex, "Failed to publish outbox event {EventId}, retry {Retry}", evt.Id, evt.RetryCount);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
