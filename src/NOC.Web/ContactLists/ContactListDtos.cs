// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Web.ContactLists;

public sealed record CreateContactListRequest(
    string Name,
    string? Description = null);

public sealed record UpdateContactListRequest(
    string? Name = null,
    string? Description = null);

public sealed record AddMembersRequest(
    IReadOnlyList<Guid> ContactIds);

public sealed record RemoveMembersRequest(
    IReadOnlyList<Guid> ContactIds);

public sealed record ContactListResponse(
    Guid Id,
    string Name,
    string? Description,
    int MemberCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
