// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Domain.Entities;

public class Segment
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string Rules { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
