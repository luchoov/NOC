// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class ContactListMemberConfiguration : IEntityTypeConfiguration<ContactListMember>
{
    public void Configure(EntityTypeBuilder<ContactListMember> builder)
    {
        builder.ToTable("contact_list_members");

        builder.HasKey(e => new { e.ContactListId, e.ContactId });
        builder.Property(e => e.AddedAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.ContactList)
            .WithMany(l => l.Members)
            .HasForeignKey(e => e.ContactListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Contact)
            .WithMany(c => c.ListMemberships)
            .HasForeignKey(e => e.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
