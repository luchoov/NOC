// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Data;
using NOC.Web.ContactLists;
using NOC.Web.Contacts;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/contact-lists")]
[Authorize]
public class ContactListController(NocDbContext db, AuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 200);

        var rows = await db.ContactLists
            .AsNoTracking()
            .OrderBy(l => l.Name)
            .Take(limit)
            .Select(l => new { l.Id, l.Name, l.Description, MemberCount = l.Members.Count, l.CreatedAt, l.UpdatedAt })
            .ToListAsync();

        return Ok(rows.Select(l => new ContactListResponse(l.Id, l.Name, l.Description, l.MemberCount, l.CreatedAt, l.UpdatedAt)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var row = await db.ContactLists
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new { l.Id, l.Name, l.Description, MemberCount = l.Members.Count, l.CreatedAt, l.UpdatedAt })
            .FirstOrDefaultAsync();

        if (row is null)
            return NotFound(new { message = "List not found" });

        return Ok(new ContactListResponse(row.Id, row.Name, row.Description, row.MemberCount, row.CreatedAt, row.UpdatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContactListRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required" });

        var now = DateTimeOffset.UtcNow;
        var list = new ContactList
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.ContactLists.Add(list);
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "CONTACT_LIST_CREATED",
            entityType: "CONTACT_LIST",
            entityId: list.Id,
            payload: new { list.Name });

        return CreatedAtAction(nameof(GetById), new { id = list.Id },
            new ContactListResponse(list.Id, list.Name, list.Description, 0, list.CreatedAt, list.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactListRequest request)
    {
        var list = await db.ContactLists.FirstOrDefaultAsync(l => l.Id == id);
        if (list is null)
            return NotFound(new { message = "List not found" });

        if (request.Name is not null)
            list.Name = request.Name.Trim();
        if (request.Description is not null)
            list.Description = request.Description.Trim();

        list.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "CONTACT_LIST_UPDATED",
            entityType: "CONTACT_LIST",
            entityId: list.Id,
            payload: new { list.Name });

        var memberCount = await db.ContactListMembers.CountAsync(m => m.ContactListId == id);
        return Ok(new ContactListResponse(list.Id, list.Name, list.Description, memberCount, list.CreatedAt, list.UpdatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var list = await db.ContactLists.FirstOrDefaultAsync(l => l.Id == id);
        if (list is null)
            return NotFound(new { message = "List not found" });

        db.ContactLists.Remove(list);
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "CONTACT_LIST_DELETED",
            entityType: "CONTACT_LIST",
            entityId: id,
            payload: new { list.Name });

        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> ListMembers(Guid id, [FromQuery] int limit = 200)
    {
        limit = Math.Clamp(limit, 1, 500);

        if (!await db.ContactLists.AnyAsync(l => l.Id == id))
            return NotFound(new { message = "List not found" });

        var contacts = await db.ContactListMembers
            .AsNoTracking()
            .Where(m => m.ContactListId == id)
            .Include(m => m.Contact)
                .ThenInclude(c => c.Tags)
            .OrderBy(m => m.AddedAt)
            .Take(limit)
            .Select(m => m.Contact)
            .ToListAsync();

        return Ok(contacts.Select(MapContactToResponse));
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMembers(Guid id, [FromBody] AddMembersRequest request)
    {
        if (!await db.ContactLists.AnyAsync(l => l.Id == id))
            return NotFound(new { message = "List not found" });

        if (request.ContactIds is not { Count: > 0 })
            return BadRequest(new { message = "ContactIds is required" });

        var existing = await db.ContactListMembers
            .Where(m => m.ContactListId == id && request.ContactIds.Contains(m.ContactId))
            .Select(m => m.ContactId)
            .ToListAsync();

        var newIds = request.ContactIds.Except(existing).ToList();
        var now = DateTimeOffset.UtcNow;

        foreach (var contactId in newIds)
        {
            db.ContactListMembers.Add(new ContactListMember
            {
                ContactListId = id,
                ContactId = contactId,
                AddedAt = now,
            });
        }

        if (newIds.Count > 0)
        {
            var list = await db.ContactLists.FirstAsync(l => l.Id == id);
            list.UpdatedAt = now;
            await db.SaveChangesAsync();

            await auditService.LogAsync(
                "CONTACT_LIST_MEMBERS_ADDED",
                entityType: "CONTACT_LIST",
                entityId: id,
                payload: new { addedCount = newIds.Count, contactIds = newIds });
        }

        var memberCount = await db.ContactListMembers.CountAsync(m => m.ContactListId == id);
        return Ok(new { added = newIds.Count, memberCount });
    }

    [HttpPost("{id:guid}/members/remove")]
    public async Task<IActionResult> RemoveMembers(Guid id, [FromBody] RemoveMembersRequest request)
    {
        if (!await db.ContactLists.AnyAsync(l => l.Id == id))
            return NotFound(new { message = "List not found" });

        if (request.ContactIds is not { Count: > 0 })
            return BadRequest(new { message = "ContactIds is required" });

        var members = await db.ContactListMembers
            .Where(m => m.ContactListId == id && request.ContactIds.Contains(m.ContactId))
            .ToListAsync();

        if (members.Count > 0)
        {
            db.ContactListMembers.RemoveRange(members);
            var list = await db.ContactLists.FirstAsync(l => l.Id == id);
            list.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            await auditService.LogAsync(
                "CONTACT_LIST_MEMBERS_REMOVED",
                entityType: "CONTACT_LIST",
                entityId: id,
                payload: new { removedCount = members.Count });
        }

        var memberCount = await db.ContactListMembers.CountAsync(m => m.ContactListId == id);
        return Ok(new { removed = members.Count, memberCount });
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
