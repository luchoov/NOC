// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using NpgsqlTypes;

namespace NOC.Shared.Domain.Entities;

public class Contact
{
    public Guid Id { get; set; }
    public required string Phone { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string CustomAttrs { get; set; } = "{}";

    // Full-text search (generated column in PostgreSQL)
    public NpgsqlTsVector? SearchVector { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ICollection<ContactTag> Tags { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
}
