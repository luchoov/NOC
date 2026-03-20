// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Infrastructure.Data;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "ADMIN,SUPERVISOR")]
public class AuditController(NocDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAuditEvents(
        [FromQuery] string? eventType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] Guid? actorId = null,
        [FromQuery] DateTimeOffset? before = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 100);

        var query = db.AuditEvents.AsQueryable();

        if (eventType is not null)
            query = query.Where(e => e.EventType == eventType);

        if (entityId is not null)
            query = query.Where(e => e.EntityId == entityId);

        if (actorId is not null)
            query = query.Where(e => e.ActorId == actorId);

        if (before is not null)
            query = query.Where(e => e.OccurredAt < before);

        var events = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Take(limit)
            .Select(e => new
            {
                e.Id,
                e.ActorId,
                e.ActorType,
                e.EventType,
                e.EntityType,
                e.EntityId,
                e.Payload,
                e.OccurredAt,
            })
            .ToListAsync();

        return Ok(events);
    }
}
