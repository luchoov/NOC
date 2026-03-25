// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Data;
using NOC.Web.Campaigns;
using NOC.Web.Segments;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/campaigns")]
[Authorize]
public class CampaignController(NocDbContext db, AuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);

        var campaigns = await db.Campaigns
            .AsNoTracking()
            .Include(c => c.Inbox)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return Ok(campaigns.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var campaign = await db.Campaigns
            .AsNoTracking()
            .Include(c => c.Inbox)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign is null)
            return NotFound(new { message = "Campaign not found" });

        return Ok(MapToResponse(campaign));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required" });
        if (string.IsNullOrWhiteSpace(request.MessageTemplate))
            return BadRequest(new { message = "MessageTemplate is required" });

        var inbox = await db.Inboxes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.InboxId);
        if (inbox is null)
            return BadRequest(new { message = "Inbox not found" });
        if (!inbox.IsActive)
            return BadRequest(new { message = "Inbox is not active" });

        // Resolve audience
        var audienceCount = (request.ContactListId.HasValue ? 1 : 0)
            + (request.SegmentId.HasValue ? 1 : 0)
            + (request.ContactIds is { Count: > 0 } ? 1 : 0);
        if (audienceCount != 1)
            return BadRequest(new { message = "Provide exactly one audience source: contactListId, segmentId, or contactIds" });

        var contacts = await ResolveAudienceAsync(request);
        if (contacts.Count == 0)
            return BadRequest(new { message = "Audience resolved to 0 contacts with phone numbers" });

        var now = DateTimeOffset.UtcNow;
        var campaign = new Campaign
        {
            Id = Guid.CreateVersion7(),
            InboxId = request.InboxId,
            Name = request.Name.Trim(),
            MessageTemplate = request.MessageTemplate.Trim(),
            MediaUrl = request.MediaUrl?.Trim(),
            MessagesPerMinute = request.MessagesPerMinute ?? 10,
            DelayMinMs = request.DelayMinMs ?? 2000,
            DelayMaxMs = request.DelayMaxMs ?? 8000,
            SendWindowStart = request.SendWindowStart,
            SendWindowEnd = request.SendWindowEnd,
            SendWindowTz = request.SendWindowTz?.Trim(),
            ScheduledAt = request.ScheduledAt,
            Status = CampaignStatus.DRAFT,
            TotalRecipients = contacts.Count,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Materialize recipients
        var recipients = contacts.Select(c => new CampaignRecipient
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaign.Id,
            ContactId = c.Id,
            Phone = c.Phone,
            Status = "QUEUED",
        }).ToList();

        db.Campaigns.Add(campaign);
        db.CampaignRecipients.AddRange(recipients);
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "CAMPAIGN_CREATED",
            entityType: "CAMPAIGN",
            entityId: campaign.Id,
            payload: new { campaign.Name, campaign.TotalRecipients });

        campaign.Inbox = inbox;
        return CreatedAtAction(nameof(GetById), new { id = campaign.Id }, MapToResponse(campaign));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCampaignRequest request)
    {
        var campaign = await db.Campaigns.Include(c => c.Inbox).FirstOrDefaultAsync(c => c.Id == id);
        if (campaign is null)
            return NotFound(new { message = "Campaign not found" });
        if (campaign.Status != CampaignStatus.DRAFT)
            return BadRequest(new { message = "Only DRAFT campaigns can be edited" });

        if (request.Name is not null) campaign.Name = request.Name.Trim();
        if (request.MessageTemplate is not null) campaign.MessageTemplate = request.MessageTemplate.Trim();
        if (request.MessagesPerMinute.HasValue) campaign.MessagesPerMinute = request.MessagesPerMinute.Value;
        if (request.ScheduledAt.HasValue) campaign.ScheduledAt = request.ScheduledAt.Value;

        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(MapToResponse(campaign));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id);
        if (campaign is null)
            return NotFound(new { message = "Campaign not found" });
        if (campaign.Status != CampaignStatus.DRAFT)
            return BadRequest(new { message = "Only DRAFT campaigns can be deleted" });

        db.Campaigns.Remove(campaign); // cascade deletes recipients
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "CAMPAIGN_DELETED",
            entityType: "CAMPAIGN",
            entityId: id,
            payload: new { campaign.Name });

        return NoContent();
    }

    [HttpPost("{id:guid}/schedule")]
    public async Task<IActionResult> Schedule(Guid id, [FromBody] ScheduleCampaignRequest request)
    {
        var campaign = await db.Campaigns.Include(c => c.Inbox).FirstOrDefaultAsync(c => c.Id == id);
        if (campaign is null)
            return NotFound(new { message = "Campaign not found" });
        if (campaign.Status != CampaignStatus.DRAFT)
            return BadRequest(new { message = "Only DRAFT campaigns can be scheduled" });
        if (request.ScheduledAt <= DateTimeOffset.UtcNow)
            return BadRequest(new { message = "ScheduledAt must be in the future" });

        campaign.ScheduledAt = request.ScheduledAt;
        campaign.Status = CampaignStatus.SCHEDULED;
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(MapToResponse(campaign));
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id)
    {
        var campaign = await db.Campaigns.Include(c => c.Inbox).FirstOrDefaultAsync(c => c.Id == id);
        if (campaign is null)
            return NotFound(new { message = "Campaign not found" });
        if (campaign.Status is not (CampaignStatus.DRAFT or CampaignStatus.SCHEDULED))
            return BadRequest(new { message = "Campaign must be DRAFT or SCHEDULED to start" });

        campaign.Status = CampaignStatus.RUNNING;
        campaign.StartedAt = DateTimeOffset.UtcNow;
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(MapToResponse(campaign));
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id)
    {
        var campaign = await db.Campaigns.Include(c => c.Inbox).FirstOrDefaultAsync(c => c.Id == id);
        if (campaign is null)
            return NotFound(new { message = "Campaign not found" });
        if (campaign.Status != CampaignStatus.RUNNING)
            return BadRequest(new { message = "Only RUNNING campaigns can be paused" });

        campaign.Status = CampaignStatus.PAUSED;
        campaign.PausedAt = DateTimeOffset.UtcNow;
        campaign.PausedReason = "Paused by user";
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(MapToResponse(campaign));
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id)
    {
        var campaign = await db.Campaigns.Include(c => c.Inbox).FirstOrDefaultAsync(c => c.Id == id);
        if (campaign is null)
            return NotFound(new { message = "Campaign not found" });
        if (campaign.Status != CampaignStatus.PAUSED)
            return BadRequest(new { message = "Only PAUSED campaigns can be resumed" });

        campaign.Status = CampaignStatus.RUNNING;
        campaign.PausedAt = null;
        campaign.PausedReason = null;
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(MapToResponse(campaign));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var campaign = await db.Campaigns.Include(c => c.Inbox).FirstOrDefaultAsync(c => c.Id == id);
        if (campaign is null)
            return NotFound(new { message = "Campaign not found" });
        if (campaign.Status is CampaignStatus.COMPLETED or CampaignStatus.FAILED)
            return BadRequest(new { message = "Campaign is already finished" });

        campaign.Status = CampaignStatus.FAILED;
        campaign.PausedReason = "Cancelled by user";
        campaign.CompletedAt = DateTimeOffset.UtcNow;
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(MapToResponse(campaign));
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id)
    {
        var source = await db.Campaigns.AsNoTracking().Include(c => c.Inbox)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (source is null)
            return NotFound(new { message = "Campaign not found" });

        var now = DateTimeOffset.UtcNow;
        var campaign = new Campaign
        {
            Id = Guid.CreateVersion7(),
            InboxId = source.InboxId,
            Name = $"{source.Name} (copia)",
            MessageTemplate = source.MessageTemplate,
            MediaUrl = source.MediaUrl,
            MessagesPerMinute = source.MessagesPerMinute,
            DelayMinMs = source.DelayMinMs,
            DelayMaxMs = source.DelayMaxMs,
            SendWindowStart = source.SendWindowStart,
            SendWindowEnd = source.SendWindowEnd,
            SendWindowTz = source.SendWindowTz,
            Status = CampaignStatus.DRAFT,
            TotalRecipients = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Copy recipients
        var sourceRecipients = await db.CampaignRecipients
            .AsNoTracking()
            .Where(r => r.CampaignId == id)
            .Select(r => new { r.ContactId, r.Phone })
            .ToListAsync();

        var recipients = sourceRecipients.Select(r => new CampaignRecipient
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaign.Id,
            ContactId = r.ContactId,
            Phone = r.Phone,
            Status = "QUEUED",
        }).ToList();

        campaign.TotalRecipients = recipients.Count;
        db.Campaigns.Add(campaign);
        db.CampaignRecipients.AddRange(recipients);
        await db.SaveChangesAsync();

        campaign.Inbox = source.Inbox;
        return CreatedAtAction(nameof(GetById), new { id = campaign.Id }, MapToResponse(campaign));
    }

    [HttpGet("{id:guid}/recipients")]
    public async Task<IActionResult> ListRecipients(Guid id, [FromQuery] string? status = null, [FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 500);

        var campaign = await db.Campaigns.AsNoTracking().AnyAsync(c => c.Id == id);
        if (!campaign)
            return NotFound(new { message = "Campaign not found" });

        var query = db.CampaignRecipients
            .AsNoTracking()
            .Include(r => r.Contact)
            .Where(r => r.CampaignId == id);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status.ToUpperInvariant());

        var recipients = await query
            .OrderBy(r => r.Id)
            .Take(limit)
            .ToListAsync();

        return Ok(recipients.Select(r => new CampaignRecipientResponse(
            r.Id,
            r.ContactId,
            r.Phone,
            r.Contact.Name,
            r.Status,
            r.SentAt,
            r.DeliveredAt,
            r.ReadAt,
            r.FailedAt,
            r.FailureReason)));
    }

    // --- Private helpers ---

    private async Task<List<(Guid Id, string Phone)>> ResolveAudienceAsync(CreateCampaignRequest request)
    {
        if (request.ContactListId.HasValue)
        {
            var rows = await db.ContactListMembers
                .AsNoTracking()
                .Where(m => m.ContactListId == request.ContactListId.Value)
                .Select(m => new { m.Contact.Id, m.Contact.Phone })
                .Where(c => c.Phone != null && c.Phone != "")
                .Distinct()
                .ToListAsync();
            return rows.Select(c => (c.Id, c.Phone)).ToList();
        }

        if (request.SegmentId.HasValue)
        {
            var segment = await db.Segments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SegmentId.Value);
            if (segment is null) return [];

            var rules = ParseSegmentRules(segment.Rules);
            var rows = await ApplySegmentRules(db.Contacts.AsNoTracking(), rules)
                .Where(c => c.Phone != null && c.Phone != "")
                .Select(c => new { c.Id, c.Phone })
                .ToListAsync();
            return rows.Select(c => (c.Id, c.Phone)).ToList();
        }

        if (request.ContactIds is { Count: > 0 })
        {
            var rows = await db.Contacts
                .AsNoTracking()
                .Where(c => request.ContactIds.Contains(c.Id) && c.Phone != null && c.Phone != "")
                .Select(c => new { c.Id, c.Phone })
                .ToListAsync();
            return rows.Select(c => (c.Id, c.Phone)).ToList();
        }

        return [];
    }

    // Duplicated from SegmentController for independence
    private static IQueryable<Contact> ApplySegmentRules(IQueryable<Contact> query, IReadOnlyList<SegmentRuleDto> rules)
    {
        foreach (var rule in rules)
        {
            switch (rule.Field)
            {
                case "locality":
                    var localityValue = rule.Value?.GetString() ?? "";
                    query = rule.Operator switch
                    {
                        "equals" => query.Where(c => c.Locality != null && c.Locality == localityValue),
                        "contains" => query.Where(c => c.Locality != null && EF.Functions.ILike(c.Locality, $"%{localityValue}%")),
                        _ => query
                    };
                    break;

                case "tags":
                    var tags = rule.Value?.ValueKind == System.Text.Json.JsonValueKind.Array
                        ? rule.Value.Value.EnumerateArray().Select(v => v.GetString()!.ToLowerInvariant()).ToList()
                        : [];
                    if (tags.Count > 0)
                    {
                        query = rule.Operator switch
                        {
                            "has_any_of" => query.Where(c => c.Tags.Any(t => tags.Contains(t.Tag))),
                            "has_all_of" => query.Where(c => tags.All(tag => c.Tags.Any(t => t.Tag == tag))),
                            _ => query
                        };
                    }
                    break;

                case "email":
                    query = rule.Operator switch
                    {
                        "is_present" => query.Where(c => c.Email != null && c.Email != ""),
                        "is_absent" => query.Where(c => c.Email == null || c.Email == ""),
                        _ => query
                    };
                    break;
            }
        }

        return query;
    }

    private static IReadOnlyList<SegmentRuleDto> ParseSegmentRules(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<SegmentRuleDto>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }) ?? [];
        }
        catch { return []; }
    }

    private static CampaignResponse MapToResponse(Campaign c) => new(
        c.Id,
        c.InboxId,
        c.Inbox?.Name ?? "",
        c.Name,
        c.Status.ToString(),
        c.MessageTemplate,
        c.MediaUrl,
        c.ScheduledAt,
        c.StartedAt,
        c.CompletedAt,
        c.PausedAt,
        c.PausedReason,
        c.MessagesPerMinute,
        c.TotalRecipients,
        c.SentCount,
        c.DeliveredCount,
        c.ReadCount,
        c.FailedCount,
        c.CreatedAt,
        c.UpdatedAt);
}
