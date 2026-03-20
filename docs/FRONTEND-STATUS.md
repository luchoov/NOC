# NOC Frontend — Estado del Proyecto

> Fecha: 2026-03-19
> Branch: `claude/eager-shtern`
> Worktree: `.claude/worktrees/eager-shtern`

---

## 1. Arquitectura General

NOC (Neuryn Omnichannel) usa **dos worktrees** de Git dentro del mismo repositorio:

| Worktree | Branch | Contenido |
|---|---|---|
| `eager-hofstadter` | `claude/eager-hofstadter` | Backend (.NET 10), Docker Compose, infra, Evolution API |
| `eager-shtern` | `claude/eager-shtern` | Frontend (Next.js 16) |

### Backend (desplegado)

El backend corre en **Docker Compose** con los siguientes servicios:

- **NOC.Web** — API REST + SignalR hub (puerto 8080)
- **PostgreSQL 17** — Base de datos principal
- **Evolution API** — Gateway WhatsApp (puerto 8082)
- **Redis** — Cache para Evolution API

Los 12 controllers del backend cubren: Auth, Conversations, Messages, Contacts, Inboxes, Proxies, Search, Audit, Health, y más.

### Frontend (en desarrollo)

El frontend corre localmente con `pnpm dev` en puerto 3000. Next.js rewrites proxean `/api/*` y `/hubs/*` al backend en `:8080`.

```
src/noc-frontend/
├── src/
│   ├── app/           # App Router (route groups: auth, dashboard)
│   ├── components/    # UI components por dominio
│   ├── lib/           # API client, stores, hooks, utils, validations
│   └── types/         # TypeScript types (api.ts)
├── next.config.ts     # Rewrites al backend
├── package.json       # Next.js 16, React 19, pnpm
└── postcss.config.mjs # Tailwind CSS v4
```

---

## 2. Stack Técnico

| Capa | Tecnología | Versión |
|---|---|---|
| Framework | Next.js (App Router) | 16.x |
| UI | React | 19.x |
| Lenguaje | TypeScript (strict) | 5.7 |
| Estilos | Tailwind CSS v4 | 4.2 |
| Estado global | Zustand | 5.x |
| Data fetching | TanStack Query | 5.x |
| Real-time | SignalR (@microsoft/signalr) | 10.x |
| Formularios | React Hook Form + Zod v4 | RHF 7.x, Zod 4.x |
| Fechas | date-fns | 4.x |
| Iconos | lucide-react | 0.577 |
| Notificaciones | sonner | 2.x |
| Package manager | pnpm | — |

**Decisiones de diseño:**
- Dark mode only. Paleta: zinc/slate base, blue/cyan/sky acentos. Sin naranja ni amarillo.
- Tokens JWT (access + refresh) en memoria Zustand solamente. Nunca localStorage/sessionStorage.
- Paginación keyset en todos los listados (no offset).
- Concurrencia optimista via `rowVersion` en conversaciones.
- UI en español.

---

## 3. Lo que está construido

### Sprint 1: Foundation + Auth ✅

**52 archivos creados** — toda la infraestructura base:

- **HTTP Client** (`lib/api/client.ts`) — Wrapper de `fetch` con auto-attach de Bearer token, `X-Correlation-Id`, interceptor 401 (refresh + retry), helpers tipados `get<T>`, `post<T>`, `put<T>`, `del<T>`.

- **API Services** (7 módulos):
  - `auth.ts` — login, refresh, logout
  - `conversations.ts` — list (keyset), get, assign (rowVersion), updateStatus (rowVersion)
  - `messages.ts` — list (keyset), send, getMediaUrl
  - `contacts.ts` — list, get, create, update, delete, addTag, removeTag, importCsv
  - `inboxes.ts` — list, get, create, update, delete, provision, connect, status, assignProxy
  - `proxies.ts` — list, get, create, delete, test, assign, unassign
  - `search.ts` — searchContacts, searchMessages
  - `audit.ts` — listAuditEvents

- **SignalR Client** (`lib/signalr/`) — Singleton `HubConnection` con JWT auth, reconnexión con backoff exponencial (1s→30s). Hooks: `useSignalR()`, `useInboxUpdates(inboxId)`, `useConversationUpdates(conversationId)`.

- **Stores Zustand**:
  - `auth.store.ts` — accessToken, refreshToken, agent info (en memoria)
  - `ui.store.ts` — sidebarCollapsed, selectedInboxId, filtros

- **TanStack Query Provider** — staleTime 30s, retry 1.

- **Zod Schemas** — auth, contact, message, inbox, proxy.

- **Types** (`types/api.ts`) — Todas las interfaces y enums mapeando exactamente los DTOs del backend.

- **Layout + Auth**:
  - Login page con RHF + Zod, redirect a `/inbox`
  - Dashboard layout con auth guard, sidebar, header, SignalR init
  - Sidebar con navegación (Inbox, Contacts, Campaigns, Settings)
  - Reconnection banner para pérdida de conexión SignalR

- **Utilities**: format-date, format-phone, constants (status labels, delivery icons).

### Settings: Inboxes + Proxies + Evolution ✅

- **Inbox Management** (`settings/inboxes/page.tsx`):
  - Grid de InboxCards con badges de estado (sesión Evolution, ban status, canal)
  - Formulario de creación (nombre, canal, teléfono, instancia Evolution)
  - Opciones auto-provision y auto-connect al crear
  - Botones: conectar QR, provisionar, refrescar estado, eliminar

- **Evolution QR Panel** (`evolution-qr-panel.tsx`):
  - Fases: idle → connecting → qr_pending → connected/error
  - Polling de status cada 5 segundos
  - Extrae QR de múltiples formatos de respuesta de Evolution API
  - Fallback a pairing code si QR no disponible

- **Proxy Management** (`settings/proxies/page.tsx`):
  - Layout split-panel: formulario izquierda, lista derecha
  - Crear proxy: alias, host, puerto, protocolo (HTTP/HTTPS/SOCKS5), credenciales opcionales
  - Lista con badges de estado, resultados de test (latencia), inboxes asignadas
  - Acciones: test de conectividad (⚡), asignar a inbox, desasignar, eliminar
  - Dropdown de asignación a inbox

- **Settings Layout** — Sub-navegación con tabs: Bandejas, Proxies, Agentes.

- **Páginas placeholder**: Agentes y Campañas muestran "Próximamente" (backend no tiene controllers para estos).

### Sprint 2: Inbox + Chat ✅

- **Inbox Page** (`inbox/page.tsx`) — Layout tres paneles estilo Chatwoot:
  - **Izquierda**: Lista de conversaciones con selector de inbox, filtros rápidos (Todas/Mías/Sin asignar), filtro de estado, scroll infinito con paginación keyset
  - **Centro**: Vista de chat o empty state
  - **Derecha**: Panel de contacto (colapsable)
  - Integración SignalR: mensajes nuevos, asignaciones, cambios de estado en tiempo real

- **Conversation List Item** — Avatar (inicial), nombre, preview del último mensaje, tiempo relativo, badge de estado, contador de no leídos.

- **Chat View** (`chat-view.tsx`) — Lista de mensajes con paginación keyset hacia arriba (scroll infinito), recepción en tiempo real via SignalR, manejo optimista de envío.

- **Message Bubble** (`message-bubble.tsx`):
  - Outbound: derecha, fondo azul
  - Inbound: izquierda, fondo gris
  - Notas privadas: centradas, borde azul + icono candado
  - Mensajes de sistema: pill centrado
  - Soporte multimedia: imágenes, audio player, documentos, ubicación
  - Status de entrega: ✓ enviado, ✓✓ entregado, azul = leído, rojo = fallido

- **Message Input** (`message-input.tsx`) — Textarea con Enter para enviar, Shift+Enter para nueva línea, toggle nota privada (candado), auto-resize, botón enviar con loading.

- **Chat Header** (`chat-header.tsx`) — Nombre/teléfono del contacto, badge estado, botón "Tomar" (asignar a mí), "Resolver"/"Reabrir" con concurrencia optimista (rowVersion), toggle panel contacto.

- **Contact Panel** (`contact-panel.tsx`) — Detalles del contacto (teléfono, email, tags, atributos custom), metadata de conversación (estado, asignado a, no leídos, primera respuesta).

---

## 4. Cambios realizados en el Backend

Durante el desarrollo del frontend se encontraron gaps en el backend que fueron corregidos:

1. **ProxyController + ProxyDtos** — No existían. Se crearon desde cero con CRUD completo, test de conectividad TCP, asignación/desasignación a inboxes, audit logging, encriptación de passwords.

2. **JsonStringEnumConverter global** — El backend serializaba enums como integers (0, 1, 2). Se agregó `JsonStringEnumConverter` en `Program.cs` para que todos los enums se serialicen como strings (`"HTTP"`, `"ACTIVE"`, etc.).

Estos cambios están en el worktree `eager-hofstadter` y fueron rebuildeados en Docker.

---

## 5. Auditoría Frontend vs Backend

Se ejecutó una auditoría completa comparando el frontend contra los endpoints reales del backend.

### Severidad ALTA (3)

| # | Issue | Detalle |
|---|---|---|
| 1 | **SearchController no existe** | `lib/api/search.ts` llama a `/api/search/contacts` y `/api/search/messages` pero el backend no tiene SearchController. Las búsquedas van a fallar con 404. |
| 2 | **CSV Import endpoint faltante** | `lib/api/contacts.ts` tiene `importCsv()` que llama a `POST /api/contacts/import/csv` pero el ContactsController no tiene ese endpoint. |
| 3 | **SignalR events nunca emitidos** | El frontend escucha `MessageReceived`, `ConversationAssigned`, `ConversationStatusChanged`, etc. via SignalR, pero el backend `NocHub.cs` es un hub vacío — define grupos pero nunca emite eventos desde los controllers. Los mensajes en tiempo real no van a funcionar. |

### Severidad MEDIA (4)

| # | Issue |
|---|---|
| 1 | La API de conversations usa `contactId` pero el frontend asume `contactName` y `contactPhone` directamente en `ConversationResponse` — verificar que el DTO real incluye estos campos. |
| 2 | El proxy `DELETE /api/proxies/{id}` podría requerir `rowVersion` para concurrencia — el frontend no lo envía. |
| 3 | El frontend usa `GET /api/inboxes/{id}/evolution/status` pero el controller real podría tener otro path. |
| 4 | Audit log frontend asume campos `eventType`, `actorId`, `payload` — verificar contra `AuditEventResponse` real. |

### Severidad BAJA (14)

Issues menores como: campos opcionales no manejados, error handling genérico que podría ser más específico, falta de retry en operaciones de Evolution API, etc.

### Recomendaciones prioritarias

1. **Implementar emisión de eventos SignalR** en los controllers del backend (al enviar mensaje, asignar, cambiar estado).
2. **Crear SearchController** o integrar búsqueda en los controllers existentes.
3. **Agregar endpoint de CSV import** en ContactsController.
4. **Verificar DTOs** — comparar campo por campo `ConversationResponse`, `MessageResponse`, `AuditEventResponse` contra lo que el frontend espera.

---

## 6. Problemas conocidos y workarounds

| Problema | Workaround aplicado |
|---|---|
| `z.coerce.number()` de Zod v4 genera tipo `unknown` incompatible con RHF resolver | Cast `resolver: zodResolver(schema) as never` |
| Next.js dev server no resuelve módulos nuevos hasta reiniciar | Borrar `.next/` y reiniciar `pnpm dev` |
| SignalR WebSocket no funciona via Next.js rewrites | El hub se conecta directamente a `:8080` (no via proxy) |

---

## 7. Próximos pasos

### Sprint 3: Contacts (pendiente)

- Tabla de contactos con búsqueda debounced y filtro por tags
- Detalle de contacto editable (campos, tags, atributos custom, historial de conversaciones)
- Dialog de creación de contacto (RHF + Zod)
- Import CSV (cuando el backend tenga el endpoint)

### Sprint 4: Audit Log + Componentes compartidos (pendiente)

- Tabla de audit log con filtros (tipo de evento, actor, fecha)
- Paginación keyset
- Viewer de payload JSON expandible
- Componentes compartidos: DataTable genérico, StatusBadge, EmptyState, ConfirmDialog, ErrorBoundary

### Fixes del backend necesarios

- [ ] Implementar emisión de eventos SignalR en controllers
- [ ] Crear SearchController (o agregar búsqueda a Contacts/Messages controllers)
- [ ] Agregar CSV import endpoint en ContactsController
- [ ] Crear CampaignController (para que la página de Campañas funcione)
- [ ] Crear AgentController (para CRUD de agentes desde Settings)

### Hardening futuro

- [ ] Mover refreshToken a httpOnly cookie (actualmente en memoria Zustand)
- [ ] Agregar tests (Vitest + React Testing Library)
- [ ] Dockerizar el frontend (Dockerfile ya existe con `output: "standalone"`)
- [ ] CI/CD pipeline

---

## 8. Cómo correr el proyecto

```bash
# Backend (ya desplegado en Docker)
cd .claude/worktrees/eager-hofstadter
docker compose up -d

# Frontend (desarrollo local)
cd .claude/worktrees/eager-shtern/src/noc-frontend
pnpm install
pnpm dev
# → http://localhost:3000
```

Variables de entorno (`.env.local`):
```
NEXT_PUBLIC_API_URL=http://localhost:8080
```

---

## 9. Estructura de archivos completa

```
src/noc-frontend/src/
├── app/
│   ├── (auth)/
│   │   ├── layout.tsx              # Layout centrado sin sidebar
│   │   └── login/page.tsx          # Login con RHF + Zod
│   ├── (dashboard)/
│   │   ├── layout.tsx              # Auth guard + sidebar + SignalR
│   │   ├── inbox/
│   │   │   ├── page.tsx            # Tres paneles: lista + chat + contacto
│   │   │   └── [id]/page.tsx       # Redirect a /inbox
│   │   ├── contacts/
│   │   │   ├── page.tsx            # Placeholder
│   │   │   └── [id]/page.tsx       # Placeholder
│   │   ├── campaigns/page.tsx      # Placeholder "Próximamente"
│   │   └── settings/
│   │       ├── layout.tsx          # Sub-nav tabs
│   │       ├── page.tsx            # Redirect a /settings/inboxes
│   │       ├── inboxes/page.tsx    # Gestión de bandejas + Evolution
│   │       ├── proxies/page.tsx    # Split-panel proxies
│   │       └── agents/page.tsx     # Placeholder "Próximamente"
│   ├── layout.tsx                  # Root layout + providers
│   ├── page.tsx                    # Redirect a /inbox
│   └── globals.css                 # Tailwind v4
├── components/
│   ├── chat/
│   │   ├── chat-header.tsx         # Header con acciones
│   │   ├── chat-view.tsx           # Lista de mensajes + scroll
│   │   ├── message-bubble.tsx      # Bubbles + media + delivery status
│   │   └── message-input.tsx       # Textarea + nota privada
│   ├── inbox/
│   │   ├── contact-panel.tsx       # Panel derecho info contacto
│   │   └── conversation-list-item.tsx
│   ├── layout/
│   │   ├── header.tsx
│   │   ├── reconnection-banner.tsx
│   │   └── sidebar.tsx
│   ├── settings/
│   │   ├── evolution-qr-panel.tsx  # QR WhatsApp + polling
│   │   ├── inbox-card.tsx          # Card con badges + acciones
│   │   └── inbox-create-form.tsx   # Modal creación inbox
│   └── shared/
│       └── placeholder-page.tsx
├── lib/
│   ├── api/
│   │   ├── client.ts              # HTTP wrapper + auth interceptor
│   │   ├── auth.ts
│   │   ├── conversations.ts
│   │   ├── messages.ts
│   │   ├── contacts.ts
│   │   ├── inboxes.ts
│   │   ├── proxies.ts
│   │   ├── search.ts
│   │   └── audit.ts
│   ├── hooks/
│   │   └── query-provider.tsx
│   ├── signalr/
│   │   ├── client.ts              # Singleton HubConnection
│   │   └── hooks.ts               # useSignalR, useInboxUpdates, etc.
│   ├── store/
│   │   ├── auth.store.ts          # JWT tokens en memoria
│   │   └── ui.store.ts
│   ├── utils/
│   │   ├── constants.ts           # Labels, colores, iconos
│   │   ├── format-date.ts
│   │   └── format-phone.ts
│   ├── utils.ts                   # cn() helper
│   └── validations/
│       ├── auth.schema.ts
│       ├── contact.schema.ts
│       ├── inbox.schema.ts
│       ├── message.schema.ts
│       └── proxy.schema.ts
└── types/
    └── api.ts                     # Todos los tipos + enums
```
