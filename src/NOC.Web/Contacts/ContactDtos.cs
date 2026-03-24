// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;

namespace NOC.Web.Contacts;

public sealed record CreateContactRequest(
    string Phone,
    string? Name = null,
    string? Email = null,
    string? Locality = null,
    string? AvatarUrl = null,
    JsonElement? CustomAttrs = null,
    IReadOnlyList<string>? Tags = null);

public sealed record UpdateContactRequest(
    string? Name = null,
    string? Email = null,
    string? Locality = null,
    string? AvatarUrl = null,
    JsonElement? CustomAttrs = null,
    bool ReplaceTags = false,
    IReadOnlyList<string>? Tags = null);

public sealed record AddTagRequest(string Tag);

public sealed record ContactResponse(
    Guid Id,
    string Phone,
    string? Name,
    string? Email,
    string? Locality,
    string? AvatarUrl,
    JsonElement CustomAttrs,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

