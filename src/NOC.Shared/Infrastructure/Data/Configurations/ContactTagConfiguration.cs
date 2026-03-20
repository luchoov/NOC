// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class ContactTagConfiguration : IEntityTypeConfiguration<ContactTag>
{
    public void Configure(EntityTypeBuilder<ContactTag> builder)
    {
        builder.ToTable("contact_tags");

        builder.HasKey(e => new { e.ContactId, e.Tag });
        builder.Property(e => e.Tag).HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.Contact)
            .WithMany(c => c.Tags)
            .HasForeignKey(e => e.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.TaggedByAgent)
            .WithMany()
            .HasForeignKey(e => e.TaggedBy);

        builder.HasIndex(e => e.Tag);
    }
}
