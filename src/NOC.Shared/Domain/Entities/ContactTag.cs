// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Domain.Entities;

public class ContactTag
{
    public Guid ContactId { get; set; }
    public required string Tag { get; set; }
    public Guid? TaggedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Contact Contact { get; set; } = null!;
    public Agent? TaggedByAgent { get; set; }
}
