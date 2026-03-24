// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;

namespace NOC.Web.Segments;

public sealed record SegmentRuleDto(
    string Field,
    string Operator,
    JsonElement? Value = null);

public sealed record CreateSegmentRequest(
    string Name,
    string? Description = null,
    IReadOnlyList<SegmentRuleDto>? Rules = null);

public sealed record UpdateSegmentRequest(
    string? Name = null,
    string? Description = null,
    IReadOnlyList<SegmentRuleDto>? Rules = null);

public sealed record SegmentResponse(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<SegmentRuleDto> Rules,
    int MatchingContactCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
