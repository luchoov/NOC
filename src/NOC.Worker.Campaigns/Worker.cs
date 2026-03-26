// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Infrastructure.Data;
using NOC.Shared.Infrastructure.Evolution;
using StackExchange.Redis;

namespace NOC.Worker.Campaigns;

public class Worker(
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer redis,
    IEvolutionApiClient evolutionApiClient,
    IConfiguration configuration,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly string _workerId = $"campaign-{Environment.MachineName}-{Guid.NewGuid():N}"[..32];
    private readonly int _batchSize = configuration.GetValue<int?>("Workers:Campaigns:BatchSize") ?? 50;
    private readonly int _leaseMinutes = configuration.GetValue<int?>("Workers:Campaigns:LeaseMinutes") ?? 5;
    private readonly int _defaultMsgsPerMinute = configuration.GetValue<int?>("Workers:Campaigns:MsgsPerMinute") ?? 10;
    private readonly double _banThresholdRatio = configuration.GetValue<double?>("Workers:Campaigns:BanThresholdRatio") ?? 0.4;
    private readonly int _banMinFailures = configuration.GetValue<int?>("Workers:Campaigns:BanMinAbsoluteFailures") ?? 5;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly Random Jitter = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Campaign worker started. Id={WorkerId}, BatchSize={BatchSize}, LeaseMinutes={LeaseMinutes}",
            _workerId, _batchSize, _leaseMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCampaignsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in campaign worker main loop");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessCampaignsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NocDbContext>();

        // Activate scheduled campaigns whose time has come
        var now = DateTimeOffset.UtcNow;
        var scheduledCampaigns = await db.Campaigns
            .Where(c => c.Status == CampaignStatus.SCHEDULED && c.ScheduledAt != null && c.ScheduledAt <= now)
            .ToListAsync(ct);

        foreach (var sc in scheduledCampaigns)
        {
            sc.Status = CampaignStatus.RUNNING;
            sc.StartedAt = now;
            sc.UpdatedAt = now;
            logger.LogInformation("Campaign {CampaignId} ({Name}) activated from SCHEDULED", sc.Id, sc.Name);
        }
        if (scheduledCampaigns.Count > 0)
            await db.SaveChangesAsync(ct);

        // Process running campaigns
        var runningCampaigns = await db.Campaigns
            .Include(c => c.Inbox)
            .Where(c => c.Status == CampaignStatus.RUNNING)
            .ToListAsync(ct);

        foreach (var campaign in runningCampaigns)
        {
            try
            {
                await ProcessCampaignAsync(db, campaign, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing campaign {CampaignId}", campaign.Id);
            }
        }
    }

    private async Task ProcessCampaignAsync(NocDbContext db, Campaign campaign, CancellationToken ct)
    {
        // Check send window
        if (campaign.SendWindowStart.HasValue && campaign.SendWindowEnd.HasValue)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(campaign.SendWindowTz ?? "UTC");
            var localNow = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
            if (localNow < campaign.SendWindowStart.Value || localNow > campaign.SendWindowEnd.Value)
            {
                logger.LogDebug("Campaign {CampaignId} outside send window, skipping", campaign.Id);
                return;
            }
        }

        // Lease recovery: reset expired claims
        var expiredCount = await db.CampaignRecipients
            .Where(r => r.CampaignId == campaign.Id && r.Status == "CLAIMED" && r.LeaseExpiresAt < DateTimeOffset.UtcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, "QUEUED")
                .SetProperty(r => r.ClaimedAt, (DateTimeOffset?)null)
                .SetProperty(r => r.ClaimedBy, (string?)null)
                .SetProperty(r => r.LeaseExpiresAt, (DateTimeOffset?)null), ct);
        if (expiredCount > 0)
            logger.LogWarning("Campaign {CampaignId}: recovered {Count} expired leases", campaign.Id, expiredCount);

        // Reset RETRY_PENDING to QUEUED
        await db.CampaignRecipients
            .Where(r => r.CampaignId == campaign.Id && r.Status == "RETRY_PENDING")
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "QUEUED"), ct);

        // Ban detection
        if (campaign.FailedCount >= _banMinFailures && campaign.SentCount > 0)
        {
            var failureRatio = (double)campaign.FailedCount / campaign.SentCount;
            if (failureRatio >= _banThresholdRatio)
            {
                campaign.Status = CampaignStatus.PAUSED;
                campaign.PausedAt = DateTimeOffset.UtcNow;
                campaign.PausedReason = $"Auto-paused: {failureRatio:P0} failure rate ({campaign.FailedCount}/{campaign.SentCount})";
                campaign.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                logger.LogWarning("Campaign {CampaignId} auto-paused due to high failure rate", campaign.Id);
                await PublishProgressAsync(campaign);
                return;
            }
        }

        // Claim a batch (include Contact for template variables)
        var leaseExpiry = DateTimeOffset.UtcNow.AddMinutes(_leaseMinutes);
        var claimed = await db.CampaignRecipients
            .Include(r => r.Contact)
            .Where(r => r.CampaignId == campaign.Id && r.Status == "QUEUED")
            .OrderBy(r => r.Id)
            .Take(_batchSize)
            .ToListAsync(ct);

        if (claimed.Count == 0)
        {
            // Check if campaign is complete
            var anyPending = await db.CampaignRecipients
                .AnyAsync(r => r.CampaignId == campaign.Id && (r.Status == "QUEUED" || r.Status == "CLAIMED" || r.Status == "RETRY_PENDING"), ct);
            if (!anyPending)
            {
                await ReconcileCountersAsync(db, campaign, ct);
                campaign.Status = CampaignStatus.COMPLETED;
                campaign.CompletedAt = DateTimeOffset.UtcNow;
                campaign.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Campaign {CampaignId} ({Name}) completed", campaign.Id, campaign.Name);
                await PublishProgressAsync(campaign);
            }
            return;
        }

        // Mark batch as claimed
        foreach (var r in claimed)
        {
            r.Status = "CLAIMED";
            r.ClaimedAt = DateTimeOffset.UtcNow;
            r.ClaimedBy = _workerId;
            r.LeaseExpiresAt = leaseExpiry;
        }
        await db.SaveChangesAsync(ct);

        // Calculate delay between messages
        var msgsPerMinute = campaign.MessagesPerMinute > 0 ? campaign.MessagesPerMinute : _defaultMsgsPerMinute;
        var minDelayMs = Math.Max(campaign.DelayMinMs, 60_000 / msgsPerMinute);
        var maxDelayMs = Math.Max(campaign.DelayMaxMs, minDelayMs + 1000);

        var instanceName = campaign.Inbox.EvolutionInstanceName;
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            logger.LogError("Campaign {CampaignId}: Inbox {InboxId} has no Evolution instance", campaign.Id, campaign.InboxId);
            return;
        }

        // Send messages
        var sentThisBatch = 0;
        var failedThisBatch = 0;

        foreach (var recipient in claimed)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Resolve template variables
                var messageText = ResolveTemplateVariables(campaign.MessageTemplate, recipient);

                var response = await evolutionApiClient.SendMessageAsync(instanceName, new EvolutionSendMessageRequest
                {
                    Number = recipient.Phone,
                    Text = messageText,
                }, cancellationToken: ct);

                // Extract external ID from response
                var externalId = response.Payload["key"]?["id"]?.GetValue<string>();

                recipient.Status = "SENT";
                recipient.SentAt = DateTimeOffset.UtcNow;
                recipient.ExternalId = externalId;
                sentThisBatch++;
            }
            catch (Exception ex)
            {
                recipient.RetryCount++;
                if (recipient.RetryCount >= 3)
                {
                    recipient.Status = "FAILED";
                    recipient.FailedAt = DateTimeOffset.UtcNow;
                    recipient.FailureReason = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    failedThisBatch++;
                }
                else
                {
                    recipient.Status = "RETRY_PENDING";
                }

                logger.LogWarning(ex, "Campaign {CampaignId}: failed to send to {Phone} (attempt {Attempt})",
                    campaign.Id, recipient.Phone, recipient.RetryCount);
            }

            // Jitter delay between messages
            var delayMs = Jitter.Next(minDelayMs, maxDelayMs);
            await Task.Delay(delayMs, ct);
        }

        // Update campaign counters
        campaign.SentCount += sentThisBatch;
        campaign.FailedCount += failedThisBatch;
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Campaign {CampaignId}: batch done. Sent={Sent}, Failed={Failed}, Total={TotalSent}/{Total}",
            campaign.Id, sentThisBatch, failedThisBatch, campaign.SentCount, campaign.TotalRecipients);

        await PublishProgressAsync(campaign);
    }

    private async Task ReconcileCountersAsync(NocDbContext db, Campaign campaign, CancellationToken ct)
    {
        var stats = await db.CampaignRecipients
            .Where(r => r.CampaignId == campaign.Id)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        campaign.SentCount = stats.Where(s => s.Status is "SENT" or "DELIVERED" or "READ").Sum(s => s.Count);
        campaign.DeliveredCount = stats.Where(s => s.Status is "DELIVERED" or "READ").Sum(s => s.Count);
        campaign.ReadCount = stats.Where(s => s.Status == "READ").Sum(s => s.Count);
        campaign.FailedCount = stats.Where(s => s.Status == "FAILED").Sum(s => s.Count);
    }

    private static string ResolveTemplateVariables(string template, CampaignRecipient recipient)
    {
        var contact = recipient.Contact;
        if (contact is null) return template;

        return template
            .Replace("{{nombre}}", contact.Name ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{{name}}", contact.Name ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{{telefono}}", recipient.Phone, StringComparison.OrdinalIgnoreCase)
            .Replace("{{phone}}", recipient.Phone, StringComparison.OrdinalIgnoreCase)
            .Replace("{{email}}", contact.Email ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{{localidad}}", contact.Locality ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PublishProgressAsync(Campaign campaign)
    {
        try
        {
            var redisDb = redis.GetDatabase();
            var payload = JsonSerializer.Serialize(new
            {
                Event = "CampaignProgress",
                CampaignId = campaign.Id.ToString(),
                Payload = new
                {
                    Status = campaign.Status.ToString(),
                    campaign.TotalRecipients,
                    campaign.SentCount,
                    campaign.DeliveredCount,
                    campaign.ReadCount,
                    campaign.FailedCount,
                }
            });
            await redisDb.PublishAsync(RedisChannel.Literal("signalr:events"), payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish campaign progress via Redis");
        }
    }
}
