// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Data;
using NOC.Web.Contacts;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/contacts")]
[Authorize]
public class ContactController(NocDbContext db, AuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] string? tag = null,
        [FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 200);

        var query = db.Contacts
            .AsNoTracking()
            .Include(c => c.Tags)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.Phone, pattern) ||
                (c.Name != null && EF.Functions.ILike(c.Name, pattern)) ||
                (c.Email != null && EF.Functions.ILike(c.Email, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            query = query.Where(c => c.Tags.Any(t => t.Tag == normalizedTag));
        }

        var contacts = await query
            .OrderBy(c => c.Name ?? c.Phone)
            .ThenBy(c => c.Id)
            .Take(limit)
            .ToListAsync();

        return Ok(contacts.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var contact = await db.Contacts
            .AsNoTracking()
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contact is null)
            return NotFound(new { message = "Contact not found" });

        return Ok(MapToResponse(contact));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContactRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "Phone is required" });

        var normalizedPhone = request.Phone.Trim();

        if (await db.Contacts.AnyAsync(c => c.Phone == normalizedPhone))
            return Conflict(new { message = "A contact with the same phone already exists." });

        var now = DateTimeOffset.UtcNow;
        var contact = new Contact
        {
            Id = Guid.CreateVersion7(),
            Phone = normalizedPhone,
            Name = request.Name?.Trim(),
            Email = request.Email?.Trim(),
            AvatarUrl = request.AvatarUrl?.Trim(),
            CustomAttrs = NormalizeJson(request.CustomAttrs),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Contacts.Add(contact);

        if (request.Tags is { Count: > 0 })
            AddOrReplaceTags(contact, request.Tags, replaceExisting: true);

        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "CONTACT_CREATED",
            entityType: "CONTACT",
            entityId: contact.Id,
            payload: new { contact.Phone, contact.Name, tags = contact.Tags.Select(t => t.Tag).ToArray() });

        var response = await db.Contacts
            .AsNoTracking()
            .Include(c => c.Tags)
            .FirstAsync(c => c.Id == contact.Id);

        return CreatedAtAction(nameof(GetById), new { id = contact.Id }, MapToResponse(response));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactRequest request)
    {
        var contact = await db.Contacts
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contact is null)
            return NotFound(new { message = "Contact not found" });

        if (request.Name is not null)
            contact.Name = request.Name.Trim();
        if (request.Email is not null)
            contact.Email = request.Email.Trim();
        if (request.AvatarUrl is not null)
            contact.AvatarUrl = request.AvatarUrl.Trim();
        if (request.CustomAttrs.HasValue)
            contact.CustomAttrs = NormalizeJson(request.CustomAttrs);

        if (request.ReplaceTags || request.Tags is { Count: > 0 })
            AddOrReplaceTags(contact, request.Tags ?? [], replaceExisting: request.ReplaceTags);

        contact.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "CONTACT_UPDATED",
            entityType: "CONTACT",
            entityId: contact.Id,
            payload: new { contact.Name, contact.Email, tags = contact.Tags.Select(t => t.Tag).ToArray() });

        return Ok(MapToResponse(contact));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool force = false)
    {
        var contact = await db.Contacts
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contact is null)
            return NotFound(new { message = "Contact not found" });

        var conversations = await db.Conversations
            .Where(c => c.ContactId == id)
            .ToListAsync();

        if (conversations.Count > 0 && !force)
            return Conflict(new { message = "Cannot delete a contact with conversations. Use ?force=true to delete contact and all conversations." });

        if (conversations.Count > 0)
        {
            // Messages cascade via EF config on Conversation delete
            db.Conversations.RemoveRange(conversations);
        }

        db.Contacts.Remove(contact);
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "CONTACT_DELETED",
            entityType: "CONTACT",
            entityId: id,
            payload: new { contact.Phone, contact.Name, force, conversationsDeleted = conversations.Count });

        return NoContent();
    }

    [HttpPost("{id:guid}/tags")]
    public async Task<IActionResult> AddTag(Guid id, [FromBody] AddTagRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Tag))
            return BadRequest(new { message = "Tag is required" });

        var contact = await db.Contacts
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contact is null)
            return NotFound(new { message = "Contact not found" });

        var normalizedTag = NormalizeTag(request.Tag);
        if (contact.Tags.All(t => t.Tag != normalizedTag))
        {
            contact.Tags.Add(new ContactTag
            {
                ContactId = contact.Id,
                Tag = normalizedTag,
                TaggedBy = GetCurrentAgentId(),
                CreatedAt = DateTimeOffset.UtcNow,
            });

            contact.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            await auditService.LogAsync(
                "CONTACT_TAG_ADDED",
                entityType: "CONTACT",
                entityId: contact.Id,
                payload: new { tag = normalizedTag });
        }

        return Ok(MapToResponse(contact));
    }

    [HttpDelete("{id:guid}/tags/{tag}")]
    public async Task<IActionResult> RemoveTag(Guid id, string tag)
    {
        var contact = await db.Contacts
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contact is null)
            return NotFound(new { message = "Contact not found" });

        var normalizedTag = NormalizeTag(tag);
        var tagEntity = contact.Tags.FirstOrDefault(t => t.Tag == normalizedTag);
        if (tagEntity is null)
            return NotFound(new { message = "Tag not found for this contact." });

        db.ContactTags.Remove(tagEntity);
        contact.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "CONTACT_TAG_REMOVED",
            entityType: "CONTACT",
            entityId: contact.Id,
            payload: new { tag = normalizedTag });

        return Ok(MapToResponse(contact));
    }

    private Guid? GetCurrentAgentId()
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var agentId) ? agentId : null;
    }

    private static void AddOrReplaceTags(Contact contact, IReadOnlyList<string> tags, bool replaceExisting)
    {
        if (replaceExisting)
            contact.Tags.Clear();

        var normalizedTags = tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(NormalizeTag)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var normalizedTag in normalizedTags)
        {
            if (contact.Tags.Any(existing => existing.Tag == normalizedTag))
                continue;

            contact.Tags.Add(new ContactTag
            {
                ContactId = contact.Id,
                Tag = normalizedTag,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private static string NormalizeTag(string rawTag)
    {
        return rawTag.Trim().ToLowerInvariant();
    }

    private static string NormalizeJson(JsonElement? json)
    {
        if (!json.HasValue)
            return "{}";
        if (json.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return "{}";

        return json.Value.GetRawText();
    }

    private static JsonElement ParseJsonOrDefault(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ParseJsonOrDefault("{}");

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var fallback = JsonDocument.Parse("{}");
            return fallback.RootElement.Clone();
        }
    }

    private static ContactResponse MapToResponse(Contact contact)
    {
        return new ContactResponse(
            contact.Id,
            contact.Phone,
            contact.Name,
            contact.Email,
            contact.AvatarUrl,
            ParseJsonOrDefault(contact.CustomAttrs),
            contact.Tags.OrderBy(t => t.Tag).Select(t => t.Tag).ToArray(),
            contact.CreatedAt,
            contact.UpdatedAt);
    }
}

