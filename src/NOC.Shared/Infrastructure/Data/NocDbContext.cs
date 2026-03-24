// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;

namespace NOC.Shared.Infrastructure.Data;

public class NocDbContext(DbContextOptions<NocDbContext> options) : DbContext(options)
{
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Inbox> Inboxes => Set<Inbox>();
    public DbSet<InboxAgent> InboxAgents => Set<InboxAgent>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactTag> ContactTags => Set<ContactTag>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageStatusEvent> MessageStatusEvents => Set<MessageStatusEvent>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<ContactList> ContactLists => Set<ContactList>();
    public DbSet<ContactListMember> ContactListMembers => Set<ContactListMember>();
    public DbSet<Segment> Segments => Set<Segment>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignRecipient> CampaignRecipients => Set<CampaignRecipient>();
    public DbSet<ProxyOutbound> ProxyOutbounds => Set<ProxyOutbound>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NocDbContext).Assembly);

        // Store all enums as strings in the database
        modelBuilder.Entity<Inbox>(e =>
        {
            e.Property(x => x.ChannelType).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.BanStatus).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<Conversation>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        });
        modelBuilder.Entity<Message>(e =>
        {
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DeliveryStatus).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<MessageStatusEvent>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<Agent>(e =>
        {
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<Campaign>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<ProxyOutbound>(e =>
        {
            e.Property(x => x.Protocol).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}
