// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Data;
using NOC.Web.Contacts;
using NOC.Web.Segments;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/segments")]
[Authorize]
public class SegmentController(NocDbContext db, AuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 200);

        var segments = await db.Segments
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Take(limit)
            .ToListAsync();

        var results = new List<SegmentResponse>();
        foreach (var s in segments)
        {
            var rules = ParseRules(s.Rules);
            var count = await ApplySegmentRules(db.Contacts.AsNoTracking(), rules).CountAsync();
            results.Add(new SegmentResponse(s.Id, s.Name, s.Description, rules, count, s.CreatedAt, s.UpdatedAt));
        }

        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var segment = await db.Segments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (segment is null)
            return NotFound(new { message = "Segment not found" });

        var rules = ParseRules(segment.Rules);
        var count = await ApplySegmentRules(db.Contacts.AsNoTracking(), rules).CountAsync();

        return Ok(new SegmentResponse(segment.Id, segment.Name, segment.Description, rules, count, segment.CreatedAt, segment.UpdatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSegmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required" });

        var rules = request.Rules ?? [];
        var validationError = ValidateRules(rules);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var now = DateTimeOffset.UtcNow;
        var segment = new Segment
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Rules = SerializeRules(rules),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Segments.Add(segment);
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "SEGMENT_CREATED",
            entityType: "SEGMENT",
            entityId: segment.Id,
            payload: new { segment.Name, ruleCount = rules.Count });

        var count = await ApplySegmentRules(db.Contacts.AsNoTracking(), rules).CountAsync();
        return CreatedAtAction(nameof(GetById), new { id = segment.Id },
            new SegmentResponse(segment.Id, segment.Name, segment.Description, rules, count, segment.CreatedAt, segment.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSegmentRequest request)
    {
        var segment = await db.Segments.FirstOrDefaultAsync(s => s.Id == id);
        if (segment is null)
            return NotFound(new { message = "Segment not found" });

        if (request.Name is not null)
            segment.Name = request.Name.Trim();
        if (request.Description is not null)
            segment.Description = request.Description.Trim();
        if (request.Rules is not null)
        {
            var validationError = ValidateRules(request.Rules);
            if (validationError is not null)
                return BadRequest(new { message = validationError });
            segment.Rules = SerializeRules(request.Rules);
        }

        segment.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "SEGMENT_UPDATED",
            entityType: "SEGMENT",
            entityId: segment.Id,
            payload: new { segment.Name });

        var rules = ParseRules(segment.Rules);
        var count = await ApplySegmentRules(db.Contacts.AsNoTracking(), rules).CountAsync();
        return Ok(new SegmentResponse(segment.Id, segment.Name, segment.Description, rules, count, segment.CreatedAt, segment.UpdatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var segment = await db.Segments.FirstOrDefaultAsync(s => s.Id == id);
        if (segment is null)
            return NotFound(new { message = "Segment not found" });

        db.Segments.Remove(segment);
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "SEGMENT_DELETED",
            entityType: "SEGMENT",
            entityId: id,
            payload: new { segment.Name });

        return NoContent();
    }

    [HttpGet("{id:guid}/contacts")]
    public async Task<IActionResult> PreviewContacts(Guid id, [FromQuery] int limit = 200)
    {
        limit = Math.Clamp(limit, 1, 500);

        var segment = await db.Segments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (segment is null)
            return NotFound(new { message = "Segment not found" });

        var rules = ParseRules(segment.Rules);
        var contacts = await ApplySegmentRules(db.Contacts.AsNoTracking().Include(c => c.Tags), rules)
            .OrderBy(c => c.Name ?? c.Phone)
            .Take(limit)
            .ToListAsync();

        return Ok(contacts.Select(MapContactToResponse));
    }

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
                    var tags = rule.Value?.ValueKind == JsonValueKind.Array
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

    private static string? ValidateRules(IReadOnlyList<SegmentRuleDto> rules)
    {
        var validFields = new Dictionary<string, HashSet<string>>
        {
            ["locality"] = ["equals", "contains"],
            ["tags"] = ["has_any_of", "has_all_of"],
            ["email"] = ["is_present", "is_absent"],
        };

        foreach (var rule in rules)
        {
            if (!validFields.TryGetValue(rule.Field, out var operators))
                return $"Invalid field: {rule.Field}";
            if (!operators.Contains(rule.Operator))
                return $"Invalid operator '{rule.Operator}' for field '{rule.Field}'";
        }

        return null;
    }

    private static IReadOnlyList<SegmentRuleDto> ParseRules(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<SegmentRuleDto>>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string SerializeRules(IReadOnlyList<SegmentRuleDto> rules)
    {
        return JsonSerializer.Serialize(rules,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static ContactResponse MapContactToResponse(Contact contact)
    {
        JsonElement customAttrs;
        try
        {
            using var doc = JsonDocument.Parse(contact.CustomAttrs ?? "{}");
            customAttrs = doc.RootElement.Clone();
        }
        catch
        {
            using var fallback = JsonDocument.Parse("{}");
            customAttrs = fallback.RootElement.Clone();
        }

        return new ContactResponse(
            contact.Id,
            contact.Phone,
            contact.Name,
            contact.Email,
            contact.Locality,
            contact.AvatarUrl,
            customAttrs,
            contact.Tags.OrderBy(t => t.Tag).Select(t => t.Tag).ToArray(),
            contact.CreatedAt,
            contact.UpdatedAt);
    }
}
