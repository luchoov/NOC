// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Domain.Entities;

public class ContactListMember
{
    public Guid ContactListId { get; set; }
    public Guid ContactId { get; set; }
    public DateTimeOffset AddedAt { get; set; }

    // Navigation
    public ContactList ContactList { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
}
