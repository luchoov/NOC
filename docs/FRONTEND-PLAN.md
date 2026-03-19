# NOC Frontend — Plan de Implementación Completo para Codex

> **Fecha:** 2026-03-19
> **Destino:** `src/noc-frontend/` (construir desde stub)
> **Stack:** Next.js 16, React 19, TypeScript strict, Tailwind CSS v4, shadcn/ui, TanStack Query v5, Zustand, SignalR, React Hook Form + Zod, date-fns
> **Tema:** Dark mode default. Paleta: zinc/slate + violet accent.
> **Licencia:** AGPL-3.0 — header en TODOS los archivos `.ts` y `.tsx`:
> ```
> // Copyright (c) Neuryn Software
> // SPDX-License-Identifier: AGPL-3.0-or-later
> ```

---

## Decisiones Tomadas

1. **Bootstrap:** Copiar stub de `eager-hofstadter/src/noc-frontend/` (tiene package.json, tsconfig.json, next.config.ts, Dockerfile, layout.tsx, page.tsx con headers AGPL correctos)
2. **Auth tokens:** Ambos (accessToken + refreshToken) en Zustand (memoria). NUNCA localStorage/sessionStorage. Tab close = logout.
3. **Campaigns + Agents:** Solo páginas placeholder "Próximamente" (no existen endpoints en el backend todavía)
4. **HTTP client:** `fetch` nativo (no Axios) para evitar dependencias extra
5. **No existe `/mnt/skills/`** en este entorno Windows

---

## Estructura de Carpetas Final

```
src/noc-frontend/
├── package.json
├── tsconfig.json
├── next.config.ts
├── postcss.config.mjs
├── components.json                 ← shadcn/ui config
├── Dockerfile
├── .env.example
├── src/
│   ├── app/
│   │   ├── globals.css             ← Tailwind v4 + dark theme CSS vars
│   │   ├── layout.tsx              ← Root layout (ThemeProvider, QueryProvider)
│   │   ├── middleware.ts           ← Route protection
│   │   ├── (auth)/
│   │   │   ├── layout.tsx          ← Centered, no sidebar
│   │   │   └── login/
│   │   │       └── page.tsx        ← Login form
│   │   └── (dashboard)/
│   │       ├── layout.tsx          ← Auth guard + Sidebar + Header + SignalR init
│   │       ├── inbox/
│   │       │   ├── page.tsx        ← Conversation list + chat (3-panel)
│   │       │   └── [id]/
│   │       │       └── page.tsx    ← Conversation detail (redirect or sub-view)
│   │       ├── contacts/
│   │       │   ├── page.tsx        ← Contact list + search + CSV
│   │       │   └── [id]/
│   │       │       └── page.tsx    ← Contact detail
│   │       ├── campaigns/
│   │       │   └── page.tsx        ← Placeholder "Próximamente"
│   │       └── settings/
│   │           ├── page.tsx        ← Settings index (redirect to inboxes)
│   │           ├── inboxes/
│   │           │   └── page.tsx    ← Inbox management
│   │           ├── proxies/
│   │           │   └── page.tsx    ← "Salidas Técnicas"
│   │           └── agents/
│   │               └── page.tsx    ← Placeholder "Próximamente"
│   ├── components/
│   │   ├── ui/                     ← shadcn/ui (auto-generated)
│   │   ├── layout/
│   │   │   ├── sidebar.tsx
│   │   │   ├── header.tsx
│   │   │   └── reconnection-banner.tsx
│   │   ├── inbox/
│   │   │   ├── conversation-list.tsx
│   │   │   ├── conversation-list-item.tsx
│   │   │   ├── conversation-filters.tsx
│   │   │   └── conversation-detail-panel.tsx
│   │   ├── chat/
│   │   │   ├── chat-view.tsx
│   │   │   ├── message-bubble.tsx
│   │   │   ├── message-input.tsx
│   │   │   ├── chat-header.tsx
│   │   │   └── media-preview.tsx
│   │   ├── contacts/
│   │   │   ├── contacts-table.tsx
│   │   │   ├── contact-create-dialog.tsx
│   │   │   ├── contact-detail.tsx
│   │   │   └── csv-import-dialog.tsx
│   │   ├── settings/
│   │   │   ├── inbox-list.tsx
│   │   │   ├── inbox-detail.tsx
│   │   │   ├── proxy-panel.tsx
│   │   │   └── audit-log.tsx
│   │   └── shared/
│   │       ├── status-badge.tsx
│   │       ├── empty-state.tsx
│   │       ├── loading-skeleton.tsx
│   │       ├── confirm-dialog.tsx
│   │       └── placeholder-page.tsx
│   ├── lib/
│   │   ├── api/
│   │   │   ├── client.ts           ← fetch wrapper con JWT interceptor
│   │   │   ├── auth.ts
│   │   │   ├── conversations.ts
│   │   │   ├── messages.ts
│   │   │   ├── contacts.ts
│   │   │   ├── inboxes.ts
│   │   │   ├── proxies.ts
│   │   │   ├── search.ts
│   │   │   └── audit.ts
│   │   ├── signalr/
│   │   │   ├── client.ts           ← Singleton HubConnection
│   │   │   └── hooks.ts            ← useSignalR, useInboxUpdates, useConversationUpdates
│   │   ├── store/
│   │   │   ├── auth.store.ts
│   │   │   └── ui.store.ts
│   │   ├── hooks/
│   │   │   ├── query-provider.tsx
│   │   │   ├── use-auth-guard.ts
│   │   │   ├── use-conversations.ts
│   │   │   ├── use-messages.ts
│   │   │   ├── use-send-message.ts
│   │   │   ├── use-assign-conversation.ts
│   │   │   └── use-update-status.ts
│   │   ├── utils/
│   │   │   ├── format-date.ts
│   │   │   ├── format-phone.ts
│   │   │   └── constants.ts
│   │   └── validations/
│   │       ├── auth.schema.ts
│   │       ├── contact.schema.ts
│   │       ├── message.schema.ts
│   │       ├── inbox.schema.ts
│   │       └── proxy.schema.ts
│   └── types/
│       └── api.ts                  ← Todos los tipos TypeScript
```

---

## Sprint 1: Foundation + Auth

### Paso 1.0 — Copiar stub y crear estructura

```bash
# Desde el root del repo, copiar el stub existente
cp -r .claude/worktrees/eager-hofstadter/src/noc-frontend/ src/noc-frontend/

# Crear directorios
cd src/noc-frontend
mkdir -p src/{components/{ui,layout,inbox,chat,contacts,settings,shared},lib/{api,signalr,store,hooks,utils,validations},types,app/{\"(auth)\"/login,\"(dashboard)\"/{inbox/\"[id]\",contacts/\"[id]\",campaigns,settings/{inboxes,proxies,agents}}}}
```

### Paso 1.1 — Instalar dependencias

```bash
cd src/noc-frontend

npm install \
  tailwindcss@latest @tailwindcss/postcss postcss \
  @tanstack/react-query@^5 \
  zustand \
  @microsoft/signalr \
  react-hook-form @hookform/resolvers zod \
  date-fns \
  lucide-react \
  class-variance-authority clsx tailwind-merge \
  sonner \
  next-themes

npm install -D \
  eslint @eslint/eslintrc eslint-config-next \
  prettier prettier-plugin-tailwindcss
```

### Paso 1.2 — Tailwind CSS v4

**`postcss.config.mjs`:**
```js
export default {
  plugins: {
    '@tailwindcss/postcss': {},
  },
};
```

**`src/app/globals.css`:**
```css
@import "tailwindcss";

@theme {
  --color-background: var(--zinc-950);
  --color-foreground: var(--zinc-50);
  --color-card: var(--zinc-900);
  --color-card-foreground: var(--zinc-50);
  --color-popover: var(--zinc-900);
  --color-popover-foreground: var(--zinc-50);
  --color-primary: var(--violet-600);
  --color-primary-foreground: var(--zinc-50);
  --color-secondary: var(--zinc-800);
  --color-secondary-foreground: var(--zinc-50);
  --color-muted: var(--zinc-800);
  --color-muted-foreground: var(--zinc-400);
  --color-accent: var(--violet-600);
  --color-accent-foreground: var(--zinc-50);
  --color-destructive: var(--red-600);
  --color-border: var(--zinc-800);
  --color-input: var(--zinc-800);
  --color-ring: var(--violet-600);
  --radius: 0.5rem;
}

body {
  @apply bg-background text-foreground;
}
```

### Paso 1.3 — shadcn/ui

```bash
npx shadcn@latest init
# Seleccionar: zinc, dark mode default

# Sprint 1 components
npx shadcn@latest add button input label card avatar badge dropdown-menu sonner separator skeleton

# Sprint 2 components (instalar junto)
npx shadcn@latest add scroll-area sheet tooltip popover select command dialog tabs textarea

# Sprint 3 components
npx shadcn@latest add table checkbox form progress alert

# Sprint 4 components
npx shadcn@latest add switch accordion
```

### Paso 1.4 — TypeScript Types (`src/types/api.ts`)

```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

// ===== ENUMS (string unions matching backend C# enums exactly) =====

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

// ===== AUTH =====

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string; // ISO 8601 DateTimeOffset
}

export interface RefreshRequest {
  refreshToken: string;
}

// ===== AGENT (from JWT claims) =====

export interface Agent {
  id: string;
  name: string;
  email: string;
  role: AgentRole;
}

// ===== CONVERSATIONS =====

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

// ===== MESSAGES =====

export interface MessageResponse {
  id: string;
  conversationId: string;
  externalId: string | null;
  direction: MessageDirection;
  type: MessageType;
  content: string | null;
  mediaUrl: string | null;
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
  type?: MessageType; // default TEXT
  isPrivateNote?: boolean; // default false
}

export interface ListMessagesParams {
  beforeCreatedAt?: string;
  beforeId?: string;
  limit?: number;
  includePrivateNotes?: boolean;
}

// ===== CONTACTS =====

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

export interface ListContactsParams {
  search?: string;
  tag?: string;
  limit?: number;
}

export interface CsvImportResult {
  created: number;
  skippedDuplicate: number;
  skippedInvalid: number;
  totalProcessed: number;
}

// ===== INBOXES =====

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
  evolutionSessionStatus: string | null; // 'CONNECTED' | 'DISCONNECTED' | 'QR_PENDING'
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
  autoProvisionEvolution?: boolean; // default true
  autoConnectEvolution?: boolean; // default true
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
  autoConnect?: boolean; // default true
}

// ===== PROXIES =====

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
  protocol?: ProxyProtocol; // default HTTP
  username?: string | null;
  password?: string | null;
}

export interface ProxyTestResult {
  ok: boolean;
  latencyMs: number | null;
  error: string | null;
}

// ===== SEARCH =====

export interface SearchContactsResult {
  query: string;
  count: number;
  results: ContactResponse[];
}

export interface SearchMessagesResult {
  count: number;
  results: MessageResponse[];
  nextCursor: { createdAt: string; id: string } | null;
}

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

// ===== AUDIT =====

export interface AuditEvent {
  id: string;
  actorId: string | null;
  actorType: string; // 'AGENT' | 'SYSTEM' | 'AI'
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
```

### Paso 1.5 — HTTP Client (`src/lib/api/client.ts`)

```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { useAuthStore } from '@/lib/store/auth.store';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

interface ApiError {
  status: number;
  message: string;
  detail?: string;
}

class ApiClient {
  private baseUrl: string;
  private isRefreshing = false;
  private refreshPromise: Promise<boolean> | null = null;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  private getHeaders(): HeadersInit {
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
      'X-Correlation-Id': crypto.randomUUID(),
    };
    const token = useAuthStore.getState().accessToken;
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    return headers;
  }

  private async handleRefresh(): Promise<boolean> {
    if (this.isRefreshing && this.refreshPromise) {
      return this.refreshPromise;
    }
    this.isRefreshing = true;
    this.refreshPromise = (async () => {
      try {
        const { refreshToken } = useAuthStore.getState();
        if (!refreshToken) return false;
        const res = await fetch(`${this.baseUrl}/api/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken }),
        });
        if (!res.ok) return false;
        const data = await res.json();
        useAuthStore.getState().setTokens(data.accessToken, data.refreshToken, data.expiresAt);
        return true;
      } catch {
        return false;
      } finally {
        this.isRefreshing = false;
        this.refreshPromise = null;
      }
    })();
    return this.refreshPromise;
  }

  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const url = `${this.baseUrl}${path}`;
    let res = await fetch(url, {
      method,
      headers: this.getHeaders(),
      body: body ? JSON.stringify(body) : undefined,
    });

    // Auto-refresh on 401
    if (res.status === 401) {
      const refreshed = await this.handleRefresh();
      if (refreshed) {
        res = await fetch(url, {
          method,
          headers: this.getHeaders(),
          body: body ? JSON.stringify(body) : undefined,
        });
      } else {
        useAuthStore.getState().clearAuth();
        window.location.href = '/login';
        throw { status: 401, message: 'Session expired' } as ApiError;
      }
    }

    if (!res.ok) {
      const error: ApiError = {
        status: res.status,
        message: res.statusText,
      };
      try {
        const detail = await res.json();
        error.detail = detail.detail || detail.message || JSON.stringify(detail);
      } catch { /* ignore parse errors */ }
      throw error;
    }

    // 204 No Content
    if (res.status === 204) return undefined as T;
    return res.json();
  }

  async get<T>(path: string): Promise<T> {
    return this.request<T>('GET', path);
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('POST', path, body);
  }

  async put<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('PUT', path, body);
  }

  async del<T>(path: string): Promise<T> {
    return this.request<T>('DELETE', path);
  }

  // Para upload de archivos (CSV import)
  async upload<T>(path: string, file: File, fieldName = 'file'): Promise<T> {
    const formData = new FormData();
    formData.append(fieldName, file);
    const headers: HeadersInit = {
      'X-Correlation-Id': crypto.randomUUID(),
    };
    const token = useAuthStore.getState().accessToken;
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    // NO Content-Type header - browser sets it with boundary
    const res = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers,
      body: formData,
    });
    if (!res.ok) {
      throw { status: res.status, message: res.statusText } as ApiError;
    }
    return res.json();
  }
}

export const apiClient = new ApiClient(API_BASE_URL);

// Helper para construir query strings
export function buildQueryString(params: Record<string, unknown>): string {
  const searchParams = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      searchParams.append(key, String(value));
    }
  }
  const qs = searchParams.toString();
  return qs ? `?${qs}` : '';
}
```

### Paso 1.6 — API Modules

Cada archivo exporta funciones tipadas que usan `apiClient`. Ejemplo para **auth**:

**`src/lib/api/auth.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { apiClient } from './client';
import type { LoginRequest, LoginResponse, RefreshRequest } from '@/types/api';

export async function login(data: LoginRequest): Promise<LoginResponse> {
  return apiClient.post<LoginResponse>('/api/auth/login', data);
}

export async function refresh(data: RefreshRequest): Promise<LoginResponse> {
  return apiClient.post<LoginResponse>('/api/auth/refresh', data);
}

export async function logout(): Promise<void> {
  return apiClient.post('/api/auth/logout');
}
```

**`src/lib/api/conversations.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { apiClient, buildQueryString } from './client';
import type {
  ConversationResponse,
  ListConversationsParams,
  AssignConversationRequest,
  UpdateConversationStatusRequest,
} from '@/types/api';

export async function listConversations(params: ListConversationsParams): Promise<ConversationResponse[]> {
  return apiClient.get<ConversationResponse[]>(`/api/conversations${buildQueryString(params)}`);
}

export async function getConversation(id: string): Promise<ConversationResponse> {
  return apiClient.get<ConversationResponse>(`/api/conversations/${id}`);
}

export async function assignConversation(id: string, data: AssignConversationRequest): Promise<ConversationResponse> {
  return apiClient.post<ConversationResponse>(`/api/conversations/${id}/assign`, data);
}

export async function updateConversationStatus(id: string, data: UpdateConversationStatusRequest): Promise<ConversationResponse> {
  return apiClient.post<ConversationResponse>(`/api/conversations/${id}/status`, data);
}
```

**`src/lib/api/messages.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { apiClient, buildQueryString } from './client';
import type { MessageResponse, SendMessageRequest, ListMessagesParams } from '@/types/api';

export async function listMessages(conversationId: string, params: ListMessagesParams): Promise<MessageResponse[]> {
  return apiClient.get<MessageResponse[]>(`/api/conversations/${conversationId}/messages${buildQueryString(params)}`);
}

export async function sendMessage(conversationId: string, data: SendMessageRequest): Promise<MessageResponse> {
  return apiClient.post<MessageResponse>(`/api/conversations/${conversationId}/messages`, data);
}

export function getMediaUrl(messageId: string): string {
  const base = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
  return `${base}/api/messages/${messageId}/media`;
}
```

**`src/lib/api/contacts.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { apiClient, buildQueryString } from './client';
import type {
  ContactResponse, CreateContactRequest, UpdateContactRequest,
  AddTagRequest, ListContactsParams, CsvImportResult,
} from '@/types/api';

export async function listContacts(params: ListContactsParams): Promise<ContactResponse[]> {
  return apiClient.get<ContactResponse[]>(`/api/contacts${buildQueryString(params)}`);
}

export async function getContact(id: string): Promise<ContactResponse> {
  return apiClient.get<ContactResponse>(`/api/contacts/${id}`);
}

export async function createContact(data: CreateContactRequest): Promise<ContactResponse> {
  return apiClient.post<ContactResponse>('/api/contacts', data);
}

export async function updateContact(id: string, data: UpdateContactRequest): Promise<ContactResponse> {
  return apiClient.put<ContactResponse>(`/api/contacts/${id}`, data);
}

export async function deleteContact(id: string): Promise<void> {
  return apiClient.del(`/api/contacts/${id}`);
}

export async function addTag(id: string, data: AddTagRequest): Promise<ContactResponse> {
  return apiClient.post<ContactResponse>(`/api/contacts/${id}/tags`, data);
}

export async function removeTag(id: string, tag: string): Promise<ContactResponse> {
  return apiClient.del<ContactResponse>(`/api/contacts/${id}/tags/${encodeURIComponent(tag)}`);
}

export async function importCsv(file: File): Promise<CsvImportResult> {
  return apiClient.upload<CsvImportResult>('/api/contacts/import/csv', file);
}
```

**`src/lib/api/inboxes.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { apiClient, buildQueryString } from './client';
import type {
  InboxResponse, CreateInboxRequest, CreateInboxResponse,
  UpdateInboxRequest, ProvisionEvolutionRequest, EvolutionOperationResponse,
} from '@/types/api';

export async function listInboxes(params?: { channelType?: string; isActive?: boolean; limit?: number }): Promise<InboxResponse[]> {
  return apiClient.get<InboxResponse[]>(`/api/inboxes${buildQueryString(params || {})}`);
}

export async function getInbox(id: string): Promise<InboxResponse> {
  return apiClient.get<InboxResponse>(`/api/inboxes/${id}`);
}

export async function createInbox(data: CreateInboxRequest): Promise<CreateInboxResponse> {
  return apiClient.post<CreateInboxResponse>('/api/inboxes', data);
}

export async function updateInbox(id: string, data: UpdateInboxRequest): Promise<InboxResponse> {
  return apiClient.put<InboxResponse>(`/api/inboxes/${id}`, data);
}

export async function deleteInbox(id: string): Promise<void> {
  return apiClient.del(`/api/inboxes/${id}`);
}

export async function provisionEvolution(id: string, data?: ProvisionEvolutionRequest): Promise<EvolutionOperationResponse> {
  return apiClient.post<EvolutionOperationResponse>(`/api/inboxes/${id}/provision-evolution`, data);
}

export async function connectEvolution(id: string): Promise<EvolutionOperationResponse> {
  return apiClient.post<EvolutionOperationResponse>(`/api/inboxes/${id}/connect`);
}

export async function getEvolutionStatus(id: string, refresh?: boolean): Promise<EvolutionOperationResponse> {
  return apiClient.get<EvolutionOperationResponse>(`/api/inboxes/${id}/status${refresh ? '?refresh=true' : ''}`);
}
```

**`src/lib/api/proxies.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { apiClient } from './client';
import type { ProxyResponse, CreateProxyRequest, ProxyTestResult } from '@/types/api';

export async function listProxies(): Promise<ProxyResponse[]> {
  return apiClient.get<ProxyResponse[]>('/api/proxies');
}

export async function getProxy(id: string): Promise<ProxyResponse> {
  return apiClient.get<ProxyResponse>(`/api/proxies/${id}`);
}

export async function createProxy(data: CreateProxyRequest): Promise<ProxyResponse> {
  return apiClient.post<ProxyResponse>('/api/proxies', data);
}

export async function deleteProxy(id: string): Promise<void> {
  return apiClient.del(`/api/proxies/${id}`);
}

export async function testProxy(id: string): Promise<ProxyTestResult> {
  return apiClient.post<ProxyTestResult>(`/api/proxies/${id}/test`);
}

export async function assignToInbox(proxyId: string, inboxId: string): Promise<{ message: string }> {
  return apiClient.post(`/api/proxies/${proxyId}/assign/${inboxId}`);
}

export async function unassignFromInbox(proxyId: string, inboxId: string): Promise<{ message: string }> {
  return apiClient.del(`/api/proxies/${proxyId}/assign/${inboxId}`);
}
```

**`src/lib/api/search.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { apiClient, buildQueryString } from './client';
import type { SearchContactsResult, SearchMessagesResult, SearchMessagesParams } from '@/types/api';

export async function searchContacts(q: string, tag?: string, limit?: number): Promise<SearchContactsResult> {
  return apiClient.get<SearchContactsResult>(`/api/search/contacts${buildQueryString({ q, tag, limit })}`);
}

export async function searchMessages(params: SearchMessagesParams): Promise<SearchMessagesResult> {
  return apiClient.get<SearchMessagesResult>(`/api/search/messages${buildQueryString(params)}`);
}
```

**`src/lib/api/audit.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { apiClient, buildQueryString } from './client';
import type { AuditEvent, ListAuditParams } from '@/types/api';

export async function listAuditEvents(params: ListAuditParams): Promise<AuditEvent[]> {
  return apiClient.get<AuditEvent[]>(`/api/audit${buildQueryString(params)}`);
}
```

### Paso 1.7 — SignalR Client

**`src/lib/signalr/client.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import * as signalR from '@microsoft/signalr';
import { useAuthStore } from '@/lib/store/auth.store';

const SIGNALR_URL = process.env.NEXT_PUBLIC_SIGNALR_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

let connection: signalR.HubConnection | null = null;

export function getSignalRConnection(): signalR.HubConnection {
  if (connection) return connection;

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${SIGNALR_URL}/hubs/noc`, {
      accessTokenFactory: () => useAuthStore.getState().accessToken || '',
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        // Exponential backoff: 1s, 2s, 4s, 8s, 16s, 30s cap
        const delay = Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
        return delay;
      },
    })
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  return connection;
}

export async function startConnection(): Promise<void> {
  const conn = getSignalRConnection();
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    await conn.start();
  }
}

export async function stopConnection(): Promise<void> {
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    await connection.stop();
  }
}

export async function joinInbox(inboxId: string): Promise<void> {
  const conn = getSignalRConnection();
  await conn.invoke('JoinInbox', inboxId);
}

export async function leaveInbox(inboxId: string): Promise<void> {
  const conn = getSignalRConnection();
  await conn.invoke('LeaveInbox', inboxId);
}

export async function joinConversation(conversationId: string): Promise<void> {
  const conn = getSignalRConnection();
  await conn.invoke('JoinConversation', conversationId);
}

export async function leaveConversation(conversationId: string): Promise<void> {
  const conn = getSignalRConnection();
  await conn.invoke('LeaveConversation', conversationId);
}
```

**`src/lib/signalr/hooks.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { useEffect, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { getSignalRConnection, startConnection, stopConnection, joinInbox, leaveInbox, joinConversation, leaveConversation } from './client';
import type { MessageResponse } from '@/types/api';

export type ConnectionStatus = 'connected' | 'disconnected' | 'reconnecting';

export function useSignalR() {
  const [status, setStatus] = useState<ConnectionStatus>('disconnected');

  useEffect(() => {
    const conn = getSignalRConnection();

    conn.onreconnecting(() => setStatus('reconnecting'));
    conn.onreconnected(() => setStatus('connected'));
    conn.onclose(() => setStatus('disconnected'));

    startConnection().then(() => setStatus('connected')).catch(() => setStatus('disconnected'));

    return () => {
      stopConnection();
    };
  }, []);

  return { status };
}

export function useInboxUpdates(inboxId: string | null, callbacks: {
  onMessageReceived?: (conversationId: string, message: MessageResponse) => void;
  onConversationAssigned?: (conversationId: string, agentId: string) => void;
  onConversationStatusChanged?: (conversationId: string, newStatus: string) => void;
  onInboxBanSuspected?: (inboxId: string, inboxName: string) => void;
  onSessionDisconnected?: (inboxId: string, instanceName: string) => void;
}) {
  useEffect(() => {
    if (!inboxId) return;
    const conn = getSignalRConnection();

    joinInbox(inboxId);

    if (callbacks.onMessageReceived) conn.on('MessageReceived', callbacks.onMessageReceived);
    if (callbacks.onConversationAssigned) conn.on('ConversationAssigned', callbacks.onConversationAssigned);
    if (callbacks.onConversationStatusChanged) conn.on('ConversationStatusChanged', callbacks.onConversationStatusChanged);
    if (callbacks.onInboxBanSuspected) conn.on('InboxBanSuspected', callbacks.onInboxBanSuspected);
    if (callbacks.onSessionDisconnected) conn.on('SessionDisconnected', callbacks.onSessionDisconnected);

    return () => {
      leaveInbox(inboxId);
      conn.off('MessageReceived');
      conn.off('ConversationAssigned');
      conn.off('ConversationStatusChanged');
      conn.off('InboxBanSuspected');
      conn.off('SessionDisconnected');
    };
  }, [inboxId]);
}

export function useConversationUpdates(conversationId: string | null, callbacks: {
  onMessageReceived?: (conversationId: string, message: MessageResponse) => void;
}) {
  useEffect(() => {
    if (!conversationId) return;
    const conn = getSignalRConnection();

    joinConversation(conversationId);

    if (callbacks.onMessageReceived) conn.on('MessageReceived', callbacks.onMessageReceived);

    return () => {
      leaveConversation(conversationId);
      conn.off('MessageReceived');
    };
  }, [conversationId]);
}
```

### Paso 1.8 — Zustand Stores

**`src/lib/store/auth.store.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { create } from 'zustand';
import type { Agent } from '@/types/api';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  expiresAt: string | null;
  agent: Agent | null;
  isAuthenticated: boolean;
  setTokens: (accessToken: string, refreshToken: string, expiresAt: string) => void;
  setAgent: (agent: Agent) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  refreshToken: null,
  expiresAt: null,
  agent: null,
  isAuthenticated: false,

  setTokens: (accessToken, refreshToken, expiresAt) =>
    set({ accessToken, refreshToken, expiresAt, isAuthenticated: true }),

  setAgent: (agent) => set({ agent }),

  clearAuth: () =>
    set({
      accessToken: null,
      refreshToken: null,
      expiresAt: null,
      agent: null,
      isAuthenticated: false,
    }),
}));
```

**`src/lib/store/ui.store.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { create } from 'zustand';
import type { ConversationStatus } from '@/types/api';

interface UIState {
  sidebarCollapsed: boolean;
  selectedInboxId: string | null;
  conversationFilter: 'all' | 'unassigned' | 'mine';
  statusFilter: ConversationStatus | null;
  toggleSidebar: () => void;
  setSelectedInboxId: (id: string | null) => void;
  setConversationFilter: (filter: 'all' | 'unassigned' | 'mine') => void;
  setStatusFilter: (status: ConversationStatus | null) => void;
}

export const useUIStore = create<UIState>((set) => ({
  sidebarCollapsed: false,
  selectedInboxId: null,
  conversationFilter: 'all',
  statusFilter: null,

  toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
  setSelectedInboxId: (id) => set({ selectedInboxId: id }),
  setConversationFilter: (filter) => set({ conversationFilter: filter }),
  setStatusFilter: (status) => set({ statusFilter: status }),
}));
```

### Paso 1.9 — TanStack Query Provider

**`src/lib/hooks/query-provider.tsx`:**
```tsx
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState } from 'react';

export function QueryProvider({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            retry: 1,
            refetchOnWindowFocus: true,
          },
        },
      })
  );

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
```

### Paso 1.10 — Zod Schemas

**`src/lib/validations/auth.schema.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod';

export const loginSchema = z.object({
  email: z.string().email('Email inválido'),
  password: z.string().min(1, 'La contraseña es requerida'),
});

export type LoginFormData = z.infer<typeof loginSchema>;
```

**`src/lib/validations/contact.schema.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod';

export const createContactSchema = z.object({
  phone: z.string().min(8, 'Teléfono inválido').max(20),
  name: z.string().max(150).optional(),
  email: z.string().email().max(200).optional().or(z.literal('')),
  avatarUrl: z.string().url().optional().or(z.literal('')),
  tags: z.array(z.string().max(50)).optional(),
});

export const updateContactSchema = z.object({
  name: z.string().max(150).optional(),
  email: z.string().email().max(200).optional().or(z.literal('')),
  avatarUrl: z.string().url().optional().or(z.literal('')),
});

export type CreateContactFormData = z.infer<typeof createContactSchema>;
export type UpdateContactFormData = z.infer<typeof updateContactSchema>;
```

**`src/lib/validations/message.schema.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod';

export const sendMessageSchema = z.object({
  content: z.string().min(1, 'El mensaje no puede estar vacío').max(4096),
  isPrivateNote: z.boolean().default(false),
});

export type SendMessageFormData = z.infer<typeof sendMessageSchema>;
```

**`src/lib/validations/inbox.schema.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod';

export const createInboxSchema = z.object({
  name: z.string().min(1).max(100),
  channelType: z.enum(['WHATSAPP_OFFICIAL', 'WHATSAPP_UNOFFICIAL']),
  phoneNumber: z.string().min(8).max(20),
  evolutionInstanceName: z.string().max(100).optional(),
  autoProvisionEvolution: z.boolean().default(true),
  autoConnectEvolution: z.boolean().default(true),
});

export type CreateInboxFormData = z.infer<typeof createInboxSchema>;
```

**`src/lib/validations/proxy.schema.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod';

export const createProxySchema = z.object({
  alias: z.string().min(1).max(100),
  host: z.string().min(1),
  port: z.coerce.number().int().min(1).max(65535),
  protocol: z.enum(['HTTP', 'HTTPS', 'SOCKS5']).default('HTTP'),
  username: z.string().optional(),
  password: z.string().optional(),
});

export type CreateProxyFormData = z.infer<typeof createProxySchema>;
```

### Paso 1.11 — Utilities

**`src/lib/utils/format-date.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { formatDistanceToNow, format, parseISO } from 'date-fns';
import { es } from 'date-fns/locale';

export function timeAgo(dateStr: string): string {
  return formatDistanceToNow(parseISO(dateStr), { addSuffix: true, locale: es });
}

export function formatFull(dateStr: string): string {
  return format(parseISO(dateStr), "d 'de' MMMM yyyy, HH:mm", { locale: es });
}

export function formatShort(dateStr: string): string {
  return format(parseISO(dateStr), 'dd/MM/yy HH:mm');
}
```

**`src/lib/utils/format-phone.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

export function formatPhone(phone: string): string {
  // Simple formatting: +XX XXX XXX XXXX
  const cleaned = phone.replace(/\D/g, '');
  if (cleaned.length > 10) {
    const cc = cleaned.slice(0, cleaned.length - 10);
    const rest = cleaned.slice(-10);
    return `+${cc} ${rest.slice(0, 3)} ${rest.slice(3, 6)} ${rest.slice(6)}`;
  }
  return phone;
}
```

**`src/lib/utils/constants.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import type { ConversationStatus, CampaignStatus, BanStatus, ProxyStatus, DeliveryStatus } from '@/types/api';

export const CONVERSATION_STATUS_CONFIG: Record<ConversationStatus, { label: string; color: string }> = {
  OPEN: { label: 'Abierta', color: 'bg-green-600' },
  ASSIGNED: { label: 'Asignada', color: 'bg-blue-600' },
  BOT_HANDLING: { label: 'Bot', color: 'bg-purple-600' },
  PENDING_CUSTOMER: { label: 'Espera cliente', color: 'bg-yellow-600' },
  PENDING_INTERNAL: { label: 'Espera interna', color: 'bg-orange-600' },
  SNOOZED: { label: 'Pospuesta', color: 'bg-zinc-600' },
  RESOLVED: { label: 'Resuelta', color: 'bg-emerald-600' },
  ARCHIVED: { label: 'Archivada', color: 'bg-zinc-700' },
};

export const BAN_STATUS_CONFIG: Record<BanStatus, { label: string; color: string }> = {
  OK: { label: 'OK', color: 'bg-green-600' },
  SUSPECTED: { label: 'Sospecha ban', color: 'bg-yellow-600' },
  BANNED: { label: 'Baneado', color: 'bg-red-600' },
};

export const PROXY_STATUS_CONFIG: Record<ProxyStatus, { label: string; color: string }> = {
  ACTIVE: { label: 'Activo', color: 'bg-green-600' },
  ASSIGNED: { label: 'Asignado', color: 'bg-blue-600' },
  FAILING: { label: 'Fallando', color: 'bg-red-600' },
  DISABLED: { label: 'Deshabilitado', color: 'bg-zinc-600' },
};

export const DELIVERY_STATUS_CONFIG: Record<DeliveryStatus, { label: string; icon: string }> = {
  PENDING: { label: 'Pendiente', icon: '⏳' },
  QUEUED: { label: 'En cola', icon: '📤' },
  SENT: { label: 'Enviado', icon: '✓' },
  DELIVERED: { label: 'Entregado', icon: '✓✓' },
  READ: { label: 'Leído', icon: '✓✓' }, // blue ticks via CSS
  FAILED: { label: 'Fallido', icon: '✗' },
  RETRY_PENDING: { label: 'Reintentando', icon: '🔄' },
};
```

### Paso 1.12 — Root Layout (actualizar existente)

**`src/app/layout.tsx`:**
```tsx
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import { ThemeProvider } from 'next-themes';
import { Toaster } from 'sonner';
import { QueryProvider } from '@/lib/hooks/query-provider';
import './globals.css';

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: 'NOC — Neuryn Omnichannel',
  description: 'Plataforma de mensajería omnicanal',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="es" suppressHydrationWarning>
      <body className={inter.className}>
        <ThemeProvider attribute="class" defaultTheme="dark" enableSystem={false}>
          <QueryProvider>
            {children}
            <Toaster richColors position="top-right" />
          </QueryProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
```

### Paso 1.13 — Auth Layout + Login Page

**`src/app/(auth)/layout.tsx`:**
```tsx
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-zinc-950">
      <div className="w-full max-w-md px-4">{children}</div>
    </div>
  );
}
```

**`src/app/(auth)/login/page.tsx`:**
```tsx
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { loginSchema, type LoginFormData } from '@/lib/validations/auth.schema';
import { login } from '@/lib/api/auth';
import { useAuthStore } from '@/lib/store/auth.store';

export default function LoginPage() {
  const router = useRouter();
  const { setTokens, setAgent } = useAuthStore();
  const [isLoading, setIsLoading] = useState(false);

  const { register, handleSubmit, formState: { errors } } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
  });

  async function onSubmit(data: LoginFormData) {
    setIsLoading(true);
    try {
      const response = await login(data);
      setTokens(response.accessToken, response.refreshToken, response.expiresAt);

      // Decode JWT to get agent info (sub, name, email, role from claims)
      const payload = JSON.parse(atob(response.accessToken.split('.')[1]));
      setAgent({
        id: payload.sub || payload.nameid,
        name: payload.name || payload.email,
        email: payload.email,
        role: payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
      });

      router.push('/inbox');
    } catch (error: unknown) {
      const err = error as { status?: number; detail?: string };
      if (err.status === 401) {
        toast.error('Credenciales inválidas');
      } else {
        toast.error('Error al conectar con el servidor');
      }
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <Card className="border-zinc-800 bg-zinc-900">
      <CardHeader className="text-center">
        <CardTitle className="text-2xl font-bold text-zinc-50">
          NOC — Neuryn Omnichannel
        </CardTitle>
        <p className="text-sm text-zinc-400">Ingresá tus credenciales</p>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="email">Email</Label>
            <Input
              id="email"
              type="email"
              placeholder="tu@email.com"
              {...register('email')}
              className="bg-zinc-800 border-zinc-700"
            />
            {errors.email && <p className="text-sm text-red-400">{errors.email.message}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="password">Contraseña</Label>
            <Input
              id="password"
              type="password"
              {...register('password')}
              className="bg-zinc-800 border-zinc-700"
            />
            {errors.password && <p className="text-sm text-red-400">{errors.password.message}</p>}
          </div>
          <Button type="submit" className="w-full" disabled={isLoading}>
            {isLoading ? 'Ingresando...' : 'Ingresar'}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
```

### Paso 1.14 — Dashboard Layout + Sidebar + Header

**`src/app/(dashboard)/layout.tsx`:**
```tsx
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuthStore } from '@/lib/store/auth.store';
import { useSignalR } from '@/lib/signalr/hooks';
import { Sidebar } from '@/components/layout/sidebar';
import { Header } from '@/components/layout/header';
import { ReconnectionBanner } from '@/components/layout/reconnection-banner';
import { useUIStore } from '@/lib/store/ui.store';

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const { isAuthenticated } = useAuthStore();
  const { sidebarCollapsed } = useUIStore();
  const { status } = useSignalR();

  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/login');
    }
  }, [isAuthenticated, router]);

  if (!isAuthenticated) return null;

  return (
    <div className="flex h-screen bg-zinc-950">
      <Sidebar />
      <div className={`flex flex-1 flex-col overflow-hidden transition-all ${sidebarCollapsed ? 'ml-16' : 'ml-64'}`}>
        {status === 'reconnecting' && <ReconnectionBanner />}
        <Header />
        <main className="flex-1 overflow-auto">{children}</main>
      </div>
    </div>
  );
}
```

**`src/components/layout/sidebar.tsx`:**
```tsx
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { Inbox, Users, Megaphone, Settings, LogOut, ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { useAuthStore } from '@/lib/store/auth.store';
import { useUIStore } from '@/lib/store/ui.store';
import { logout } from '@/lib/api/auth';
import { cn } from '@/lib/utils';

const navItems = [
  { href: '/inbox', label: 'Bandeja', icon: Inbox },
  { href: '/contacts', label: 'Contactos', icon: Users },
  { href: '/campaigns', label: 'Campañas', icon: Megaphone },
  { href: '/settings', label: 'Configuración', icon: Settings },
];

export function Sidebar() {
  const pathname = usePathname();
  const router = useRouter();
  const { agent, clearAuth } = useAuthStore();
  const { sidebarCollapsed, toggleSidebar } = useUIStore();

  async function handleLogout() {
    try { await logout(); } catch { /* ignore */ }
    clearAuth();
    router.push('/login');
  }

  return (
    <aside className={cn(
      'fixed left-0 top-0 z-40 flex h-screen flex-col border-r border-zinc-800 bg-zinc-900 transition-all',
      sidebarCollapsed ? 'w-16' : 'w-64'
    )}>
      {/* Logo */}
      <div className="flex h-14 items-center justify-between border-b border-zinc-800 px-4">
        {!sidebarCollapsed && <span className="text-lg font-bold text-violet-400">NOC</span>}
        <Button variant="ghost" size="icon" onClick={toggleSidebar} className="h-8 w-8">
          {sidebarCollapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
        </Button>
      </div>

      {/* Navigation */}
      <nav className="flex-1 space-y-1 px-2 py-4">
        {navItems.map((item) => {
          const isActive = pathname.startsWith(item.href);
          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                'flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors',
                isActive
                  ? 'bg-violet-600/20 text-violet-400'
                  : 'text-zinc-400 hover:bg-zinc-800 hover:text-zinc-200'
              )}
            >
              <item.icon className="h-5 w-5 shrink-0" />
              {!sidebarCollapsed && <span>{item.label}</span>}
            </Link>
          );
        })}
      </nav>

      {/* Agent info + Logout */}
      <div className="border-t border-zinc-800 p-4">
        <div className="flex items-center gap-3">
          <Avatar className="h-8 w-8">
            <AvatarFallback className="bg-violet-600 text-xs">
              {agent?.name?.slice(0, 2).toUpperCase() || '??'}
            </AvatarFallback>
          </Avatar>
          {!sidebarCollapsed && (
            <div className="flex-1 overflow-hidden">
              <p className="truncate text-sm text-zinc-200">{agent?.name}</p>
              <Badge variant="outline" className="text-xs">{agent?.role}</Badge>
            </div>
          )}
        </div>
        <Button
          variant="ghost"
          size={sidebarCollapsed ? 'icon' : 'sm'}
          onClick={handleLogout}
          className="mt-2 w-full text-zinc-400 hover:text-red-400"
        >
          <LogOut className="h-4 w-4" />
          {!sidebarCollapsed && <span className="ml-2">Cerrar sesión</span>}
        </Button>
      </div>
    </aside>
  );
}
```

**`src/components/layout/header.tsx`:**
```tsx
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { usePathname } from 'next/navigation';

const PAGE_TITLES: Record<string, string> = {
  '/inbox': 'Bandeja de entrada',
  '/contacts': 'Contactos',
  '/campaigns': 'Campañas',
  '/settings': 'Configuración',
  '/settings/inboxes': 'Inboxes',
  '/settings/proxies': 'Salidas Técnicas',
  '/settings/agents': 'Agentes',
};

export function Header() {
  const pathname = usePathname();
  const title = PAGE_TITLES[pathname] || 'NOC';

  return (
    <header className="flex h-14 items-center border-b border-zinc-800 bg-zinc-900 px-6">
      <h1 className="text-lg font-semibold text-zinc-100">{title}</h1>
    </header>
  );
}
```

**`src/components/layout/reconnection-banner.tsx`:**
```tsx
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

export function ReconnectionBanner() {
  return (
    <div className="flex items-center justify-center bg-yellow-600 px-4 py-1 text-sm text-zinc-900">
      Reconectando al servidor en tiempo real...
    </div>
  );
}
```

### Paso 1.15 — .env.example

```
NEXT_PUBLIC_API_URL=http://localhost:8080
NEXT_PUBLIC_SIGNALR_URL=http://localhost:8080
```

---

## Sprint 2: Inbox + Chat

### Componentes principales

Los hooks de data fetching usan TanStack Query `useInfiniteQuery` para keyset pagination.

**`src/lib/hooks/use-conversations.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { useInfiniteQuery } from '@tanstack/react-query';
import { listConversations } from '@/lib/api/conversations';
import type { ListConversationsParams, ConversationResponse } from '@/types/api';

export function useConversations(params: Omit<ListConversationsParams, 'beforeLastMessageAt' | 'beforeId'>) {
  return useInfiniteQuery({
    queryKey: ['conversations', params],
    queryFn: ({ pageParam }) =>
      listConversations({
        ...params,
        beforeLastMessageAt: pageParam?.beforeLastMessageAt,
        beforeId: pageParam?.beforeId,
        limit: 20,
      }),
    initialPageParam: undefined as { beforeLastMessageAt?: string; beforeId?: string } | undefined,
    getNextPageParam: (lastPage: ConversationResponse[]) => {
      if (lastPage.length < 20) return undefined;
      const last = lastPage[lastPage.length - 1];
      return { beforeLastMessageAt: last.lastMessageAt ?? undefined, beforeId: last.id };
    },
  });
}
```

**`src/lib/hooks/use-messages.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { useInfiniteQuery } from '@tanstack/react-query';
import { listMessages } from '@/lib/api/messages';
import type { ListMessagesParams, MessageResponse } from '@/types/api';

export function useMessages(conversationId: string) {
  return useInfiniteQuery({
    queryKey: ['messages', conversationId],
    queryFn: ({ pageParam }) =>
      listMessages(conversationId, {
        beforeCreatedAt: pageParam?.beforeCreatedAt,
        beforeId: pageParam?.beforeId,
        limit: 50,
        includePrivateNotes: true,
      }),
    initialPageParam: undefined as { beforeCreatedAt?: string; beforeId?: string } | undefined,
    getNextPageParam: (lastPage: MessageResponse[]) => {
      if (lastPage.length < 50) return undefined;
      const last = lastPage[lastPage.length - 1];
      return { beforeCreatedAt: last.createdAt, beforeId: last.id };
    },
  });
}
```

**`src/lib/hooks/use-send-message.ts`:**
```typescript
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { sendMessage } from '@/lib/api/messages';
import type { SendMessageRequest } from '@/types/api';

export function useSendMessage(conversationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: SendMessageRequest) => sendMessage(conversationId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['messages', conversationId] });
      queryClient.invalidateQueries({ queryKey: ['conversations'] });
    },
  });
}
```

### Páginas

**`src/app/(dashboard)/inbox/page.tsx`** — Three-panel layout:
- Left: `<ConversationList />` — filterable by inbox, status, assignment
- Center: `<ChatView />` for selected conversation (or empty state)
- Right: `<ConversationDetailPanel />` collapsible

**`src/app/(dashboard)/inbox/[id]/page.tsx`** — Route que establece la conversación activa, puede redirigir al layout de inbox con la conversación seleccionada.

### Componentes de Inbox
- `components/inbox/conversation-list.tsx` — infinite scroll list usando `useConversations`
- `components/inbox/conversation-list-item.tsx` — avatar, nombre, preview, timestamp, unread badge, status badge
- `components/inbox/conversation-filters.tsx` — inbox dropdown, status filter, assignment filter
- `components/inbox/conversation-detail-panel.tsx` — contact info, tags, assignment, status change con `rowVersion`

### Componentes de Chat
- `components/chat/chat-view.tsx` — message list con scroll infinito hacia arriba, auto-scroll al bottom en nuevos mensajes
- `components/chat/message-bubble.tsx`:
  - INBOUND: alineado izquierda, fondo zinc-800
  - OUTBOUND: alineado derecha, fondo violet-600/20
  - INTERNAL_NOTE: fondo yellow-900/30, ícono de candado
  - SYSTEM: centrado, texto zinc-500
  - Media: preview de imagen, player de audio, link de descarga para documentos
  - Delivery status: checkmarks (✓ sent, ✓✓ delivered, ✓✓ azul read)
- `components/chat/message-input.tsx` — textarea, Enter=send, Shift+Enter=newline, toggle nota privada
- `components/chat/chat-header.tsx` — nombre contacto, teléfono, status badge, botones: tomar, resolver, asignar
- `components/chat/media-preview.tsx` — preview para IMAGE, AUDIO, VIDEO, DOCUMENT

### Real-time
- Usar `useConversationUpdates(conversationId)` en ChatView para recibir mensajes nuevos
- Usar `useInboxUpdates(inboxId)` en ConversationList para actualizar badges y reordenar
- Al recibir `MessageReceived`: invalidar cache de TanStack Query para la conversación afectada

---

## Sprint 3: Contacts

### Páginas
**`src/app/(dashboard)/contacts/page.tsx`** — tabla + búsqueda + filtros + CSV import/export
**`src/app/(dashboard)/contacts/[id]/page.tsx`** — detalle editable

### Componentes
- `components/contacts/contacts-table.tsx` — tabla con búsqueda debounced (500ms), filtro por tag, paginación
- `components/contacts/contact-create-dialog.tsx` — dialog con form RHF + Zod
- `components/contacts/contact-detail.tsx` — campos editables, tags (add/remove chips), custom attrs JSON editor, historial de conversaciones
- `components/contacts/csv-import-dialog.tsx` — drag-drop zone, preview 5 rows, upload, resultado

### Campaigns (placeholder)
**`src/app/(dashboard)/campaigns/page.tsx`:**
```tsx
// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { PlaceholderPage } from '@/components/shared/placeholder-page';

export default function CampaignsPage() {
  return <PlaceholderPage title="Campañas" description="Esta funcionalidad estará disponible próximamente." />;
}
```

---

## Sprint 4: Settings

### Inboxes (`/settings/inboxes`)
- Lista de inboxes con badges de estado (Evolution session, ban status)
- Dialog para crear inbox (form diferente según OFFICIAL vs UNOFFICIAL)
- Panel de detalle: editar, provision Evolution, conectar, ver QR, asignar proxy

### Proxies (`/settings/proxies`)
- **Panel izquierdo:** formulario de alta (alias, host, port, protocolo, usuario, contraseña)
- **Panel derecho:** lista de proxies con badge de estado, botón "Probar" (muestra latencia inline), botón "Eliminar" (disabled si assignedInboxCount > 0)

### Agents (`/settings/agents`) — placeholder "Próximamente"

### Audit Log (integrado en settings o como sub-tab)
- Tabla: timestamp, actor, event type, entity, payload expandible
- Filtros por tipo, actor, fecha
- Keyset pagination con cursor `before`

### Shared Components
- `components/shared/status-badge.tsx` — badge genérico con color mapping
- `components/shared/empty-state.tsx` — ilustración + mensaje cuando no hay datos
- `components/shared/loading-skeleton.tsx` — skeletons por tipo de página
- `components/shared/confirm-dialog.tsx` — "¿Estás seguro?" reutilizable
- `components/shared/placeholder-page.tsx` — página "Próximamente" genérica

---

## Verificación por Sprint

### Sprint 1
1. `cd src/noc-frontend && npm run dev` → app inicia en :3000
2. `http://localhost:3000` → redirect a `/login`
3. Login con credenciales de admin → redirect a `/inbox`, sidebar visible
4. DevTools: no tokens en localStorage, header `Authorization: Bearer` en requests
5. WebSocket a `/hubs/noc` conectado (Network tab)
6. `npm run build` → build exitoso sin errores TypeScript

### Sprint 2
1. `/inbox` muestra lista de conversaciones (o empty state)
2. Click en conversación → chat carga con scroll infinito
3. Enviar mensaje → aparece inmediato, delivery status se actualiza
4. Dos tabs abiertos → mensaje enviado en uno aparece en el otro via SignalR
5. Asignar conversación → handle 409 si otro agente la tomó primero

### Sprint 3
1. `/contacts` muestra tabla con búsqueda funcionando
2. CSV import muestra resumen (created/skipped/invalid)
3. `/campaigns` muestra placeholder

### Sprint 4
1. `/settings/proxies` muestra panel dividido, botón "Probar" retorna latencia
2. `/settings/inboxes` muestra lista con estado de Evolution
3. Audit log carga y filtra correctamente
4. `/settings/agents` muestra placeholder

---

## Notas Importantes para el Implementador

1. **AGPL header** en TODOS los `.ts` y `.tsx`:
   ```
   // Copyright (c) Neuryn Software
   // SPDX-License-Identifier: AGPL-3.0-or-later
   ```

2. **TypeScript estricto**: `strict: true` ya está en tsconfig. No usar `any`.

3. **Keyset pagination siempre**: Nunca OFFSET/page. Usar `beforeCreatedAt + beforeId` para mensajes, `beforeLastMessageAt + beforeId` para conversaciones.

4. **Backend C# serializa en camelCase** por default (System.Text.Json). Los tipos TypeScript ya reflejan esto.

5. **Enums son strings**: El backend serializa enums como strings (`"OPEN"`, no `0`). Los tipos TypeScript usan string unions.

6. **Concurrencia optimística**: Las operaciones de asignar/cambiar estado de conversación requieren `expectedRowVersion`. Si el backend devuelve 409, mostrar toast y refrescar datos.

7. **SignalR events**: Los nombres de eventos (`MessageReceived`, etc.) vienen del Worker.Notifications que publica via hub. Verificar los nombres exactos contra el código del hub/worker.

8. **No existe endpoint de agentes CRUD**: El login crea la sesión pero no hay `/api/agents` para listar/crear agentes. La info del agente logueado viene del JWT claims.

9. **No existe endpoint de campañas**: Solo la tabla campaigns existe en DB. No hay controller REST.

10. **Media**: Las URLs de media apuntan a MinIO via el endpoint `/api/messages/{id}/media` que hace redirect a una presigned URL.
