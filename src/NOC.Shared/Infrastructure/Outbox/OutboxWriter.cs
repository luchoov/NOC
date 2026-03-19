// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Infrastructure.Data;

namespace NOC.Shared.Infrastructure.Outbox;

/// <summary>
/// Enqueues an event into the outbox table within the current transaction.
/// The caller must call SaveChangesAsync to commit both the domain change and the outbox event atomically.
/// </summary>
public class OutboxWriter(NocDbContext db)
{
    public void Enqueue<T>(string stream, T @event, Guid? correlationId = null, Guid? causationId = null) where T : class
    {
        db.OutboxEvents.Add(new OutboxEvent
        {
            Stream = stream,
            EventType = typeof(T).Name,
            Payload = JsonSerializer.Serialize(@event),
            CorrelationId = correlationId,
            CausationId = causationId,
        });
    }
}
