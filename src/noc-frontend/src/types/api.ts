// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

// ── Enums (string unions matching backend C# enums) ──────────────────────

export type ConversationStatus =
  | 'OPEN'
  | 'ASSIGNED'
  | 'BOT_HANDLING'
  | 'PENDING_CUSTOMER'
  | 'PENDING_INTERNAL'
  | 'SNOOZED'
  | 'RESOLVED'
  | 'ARCHIVED';

export type MessageType =
  | 'TEXT'
  | 'IMAGE'
  | 'AUDIO'
  | 'VIDEO'
  | 'DOCUMENT'
  | 'STICKER'
  | 'LOCATION'
  | 'TEMPLATE'
  | 'INTERNAL_NOTE'
  | 'SYSTEM';

export type MessageDirection = 'INBOUND' | 'OUTBOUND';

export type DeliveryStatus =
  | 'PENDING'
  | 'QUEUED'
  | 'SENT'
  | 'DELIVERED'
  | 'READ'
  | 'FAILED'
  | 'RETRY_PENDING';

export type ChannelType = 'WHATSAPP_OFFICIAL' | 'WHATSAPP_UNOFFICIAL';

export type AgentRole = 'ADMIN' | 'SUPERVISOR' | 'AGENT';

export type BanStatus = 'OK' | 'SUSPECTED' | 'BANNED';

export type ProxyProtocol = 'HTTP' | 'HTTPS' | 'SOCKS5';

export type ProxyStatus = 'ACTIVE' | 'ASSIGNED' | 'FAILING' | 'DISABLED';

export type CampaignStatus =
  | 'DRAFT'
  | 'SCHEDULED'
  | 'RUNNING'
  | 'PAUSED'
  | 'COMPLETED'
  | 'FAILED';

// ── Auth ─────────────────────────────────────────────────────────────────

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

export interface RefreshRequest {
  refreshToken: string;
}

export interface Agent {
  id: string;
  name: string;
  email: string;
  role: AgentRole;
}

// ── Conversations ────────────────────────────────────────────────────────

export interface ConversationResponse {
  id: string;
  inboxId: string;
  contactId: string;
  contactPhone: string;
  contactName: string | null;
  assignedTo: string | null;
  status: ConversationStatus;
  subject: string | null;
  lastMessageAt: string | null;
  lastMessagePreview: string | null;
  lastMessageDirection: string | null;
  unreadCount: number;
  firstResponseAt: string | null;
  resolvedAt: string | null;
  snoozedUntil: string | null;
  reopenedCount: number;
  rowVersion: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateConversationRequest {
  contactId: string;
  inboxId: string;
}

export interface AssignConversationRequest {
  agentId: string;
  expectedRowVersion: number;
}

export interface UpdateConversationStatusRequest {
  status: ConversationStatus;
  expectedRowVersion: number;
  snoozedUntil?: string | null;
}

export interface ListConversationsParams {
  inboxId?: string;
  status?: ConversationStatus;
  assignedTo?: string;
  beforeLastMessageAt?: string;
  beforeId?: string;
  limit?: number;
}

// ── Messages ─────────────────────────────────────────────────────────────

export interface MessageResponse {
  id: string;
  conversationId: string;
  externalId: string | null;
  direction: MessageDirection;
  type: MessageType;
  content: string | null;
  mediaUrl: string | null;
  mediaMimeType: string | null;
  mediaFilename: string | null;
  mediaSizeBytes: number | null;
  deliveryStatus: DeliveryStatus | null;
  deliveryUpdatedAt: string | null;
  sentByAgentId: string | null;
  sentByAi: boolean;
  isPrivateNote: boolean;
  providerMetadata: Record<string, unknown>;
  createdAt: string;
}

export interface SendMessageRequest {
  content: string;
  type?: MessageType;
  isPrivateNote?: boolean;
}

export interface ListMessagesParams {
  beforeCreatedAt?: string;
  beforeId?: string;
  limit?: number;
  includePrivateNotes?: boolean;
}

// ── Contacts ─────────────────────────────────────────────────────────────

export interface ContactResponse {
  id: string;
  phone: string;
  name: string | null;
  email: string | null;
  avatarUrl: string | null;
  customAttrs: Record<string, unknown>;
  tags: string[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateContactRequest {
  phone: string;
  name?: string | null;
  email?: string | null;
  avatarUrl?: string | null;
  customAttrs?: Record<string, unknown> | null;
  tags?: string[] | null;
}

export interface UpdateContactRequest {
  name?: string | null;
  email?: string | null;
  avatarUrl?: string | null;
  customAttrs?: Record<string, unknown> | null;
  replaceTags?: boolean;
  tags?: string[] | null;
}

export interface AddTagRequest {
  tag: string;
}

export interface CsvImportResult {
  created: number;
  skippedDuplicate: number;
  skippedInvalid: number;
  totalProcessed: number;
}

// ── Inboxes ──────────────────────────────────────────────────────────────

export interface InboxResponse {
  id: string;
  name: string;
  channelType: ChannelType;
  phoneNumber: string;
  config: Record<string, unknown>;
  configSchemaVer: number;
  isActive: boolean;
  banStatus: BanStatus;
  bannedAt: string | null;
  banReason: string | null;
  evolutionInstanceName: string | null;
  evolutionSessionStatus: string | null;
  evolutionLastHeartbeat: string | null;
  proxyOutboundId: string | null;
  hasAccessToken: boolean;
  hasRefreshToken: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateInboxRequest {
  name: string;
  channelType: ChannelType;
  phoneNumber: string;
  config?: Record<string, unknown> | null;
  accessToken?: string | null;
  refreshToken?: string | null;
  evolutionInstanceName?: string | null;
  autoProvisionEvolution?: boolean;
  autoConnectEvolution?: boolean;
}

export interface UpdateInboxRequest {
  name?: string | null;
  phoneNumber?: string | null;
  config?: Record<string, unknown> | null;
  isActive?: boolean | null;
  banStatus?: BanStatus | null;
  banReason?: string | null;
  accessToken?: string | null;
  refreshToken?: string | null;
  evolutionInstanceName?: string | null;
}

export interface CreateInboxResponse {
  inbox: InboxResponse;
  evolutionProvisioned: boolean;
  evolutionCreatePayload: Record<string, unknown> | null;
  evolutionConnectPayload: Record<string, unknown> | null;
  evolutionError: string | null;
}

export interface EvolutionOperationResponse {
  inbox: InboxResponse;
  operation: string;
  payload: Record<string, unknown>;
}

export interface ProvisionEvolutionRequest {
  autoConnect?: boolean;
}

// ── Proxies ──────────────────────────────────────────────────────────────

export interface ProxyResponse {
  id: string;
  alias: string;
  host: string;
  port: number;
  protocol: ProxyProtocol;
  hasCredentials: boolean;
  status: ProxyStatus;
  lastTestedAt: string | null;
  lastTestOk: boolean | null;
  lastTestLatencyMs: number | null;
  lastError: string | null;
  assignedInboxCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProxyRequest {
  alias: string;
  host: string;
  port: number;
  protocol?: ProxyProtocol;
  username?: string | null;
  password?: string | null;
}

export interface ProxyTestResult {
  ok: boolean;
  latencyMs: number | null;
  error: string | null;
}

// ── Search ───────────────────────────────────────────────────────────────

export interface SearchMessagesParams {
  conversationId?: string;
  inboxId?: string;
  q?: string;
  direction?: MessageDirection;
  type?: MessageType;
  after?: string;
  before?: string;
  cursorCreatedAt?: string;
  cursorId?: string;
  limit?: number;
}

// ── Audit ────────────────────────────────────────────────────────────────

export interface AuditEvent {
  id: string;
  actorId: string | null;
  actorType: string;
  eventType: string;
  entityType: string | null;
  entityId: string | null;
  payload: Record<string, unknown>;
  ipAddress: string | null;
  occurredAt: string;
}

export interface ListAuditParams {
  eventType?: string;
  entityId?: string;
  actorId?: string;
  before?: string;
  limit?: number;
}
