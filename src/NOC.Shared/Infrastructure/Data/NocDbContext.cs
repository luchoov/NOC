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
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignRecipient> CampaignRecipients => Set<CampaignRecipient>();
    public DbSet<ProxyOutbound> ProxyOutbounds => Set<ProxyOutbound>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Register PostgreSQL enums
        modelBuilder.HasPostgresEnum<ChannelType>();
        modelBuilder.HasPostgresEnum<BanStatus>();
        modelBuilder.HasPostgresEnum<ConversationStatus>();
        modelBuilder.HasPostgresEnum<MessageDirection>();
        modelBuilder.HasPostgresEnum<MessageType>();
        modelBuilder.HasPostgresEnum<DeliveryStatus>();
        modelBuilder.HasPostgresEnum<AgentRole>();
        modelBuilder.HasPostgresEnum<ProxyProtocol>();
        modelBuilder.HasPostgresEnum<ProxyStatus>();
        modelBuilder.HasPostgresEnum<CampaignStatus>();

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NocDbContext).Assembly);
    }
}
