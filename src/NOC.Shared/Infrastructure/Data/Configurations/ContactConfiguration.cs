// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");

        builder.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(e => e.Phone).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.CustomAttrs).HasColumnType("jsonb").HasDefaultValueSql("'{}'");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

        // Generated tsvector column for full-text search
        builder.Property(e => e.SearchVector)
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "to_tsvector('spanish', coalesce(name, '') || ' ' || phone || ' ' || coalesce(email, ''))",
                stored: true);

        builder.HasIndex(e => e.Phone).IsUnique();
        builder.HasIndex(e => e.SearchVector).HasMethod("GIN");
    }
}
