# Neuryn Omnichannel (NOC)

Plataforma de mensajería centralizada, single-tenant, diseñada para gestionar comunicaciones WhatsApp a través de Evolution API. Cada instancia se despliega de forma independiente por cliente/proyecto.

## Stack

| Capa | Tecnología |
|------|-----------|
| Backend API | .NET 10 (ASP.NET Core), C# |
| Frontend | Next.js 16, React 19, TypeScript strict |
| Base de datos | PostgreSQL 18 (UUIDv7, JSONB) |
| Cache / Event Bus | Redis 8 Streams |
| Media Storage | MinIO (S3-compatible) |
| Proveedor WhatsApp | Evolution API v2 |
| Contenedores | Docker Compose |
| Deploy | Dokploy (previsto) |

## Arquitectura

Modular monolith con workers especializados. Un dominio compartido (`NOC.Shared`), un esquema de base de datos, pero procesos separados por responsabilidad:

```
┌────────────────────────────────────────────────────────────────┐
│                     NEURYN OMNICHANNEL                          │
│                                                                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐ │
│  │ Evolution API │  │ Frontend     │  │ Webhooks (ngrok)     │ │
│  │ (WhatsApp)    │  │ (Next.js 16) │  │                      │ │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬───────────┘ │
│         │                 │                      │             │
│  ┌──────▼─────────────────▼──────────────────────▼───────────┐ │
│  │              NOC.Web (ASP.NET Core 10)                     │ │
│  │   REST API · SignalR · Webhooks · JWT Auth · :8080         │ │
│  └──────────────────────────┬────────────────────────────────┘ │
│                             │                                   │
│  ┌──────────────────────────▼────────────────────────────────┐ │
│  │   PostgreSQL 18            Redis 8 Streams                 │ │
│  │   (data + outbox)          (events + cache + locks)        │ │
│  └──────┬─────────────────────────────────┬──────────────────┘ │
│         │                                 │                     │
│  ┌──────▼──────┐ ┌──────────┐ ┌──────────▼──┐ ┌────────────┐ │
│  │ Worker      │ │ Worker   │ │ Worker      │ │ Worker     │ │
│  │ Messaging   │ │ Campaigns│ │ Notifications│ │ AI (stub)  │ │
│  └─────────────┘ └──────────┘ └─────────────┘ └────────────┘ │
│                                                                │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ MinIO (media storage)                                      │ │
│  └────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
```

## Servicios Docker

| Servicio | Puerto | Descripción |
|----------|--------|-------------|
| `postgres` | 5432 | Base de datos principal |
| `redis` | 6379 | Event bus (Streams), cache, locks |
| `minio` | 9000/9001 | Almacenamiento de media (S3) |
| `noc-web` | 8080 | API REST + SignalR + Webhooks |
| `noc-frontend` | 3000 | UI Next.js |
| `noc-worker-messaging` | — | Procesa mensajes inbound/outbound |
| `noc-worker-campaigns` | — | Ejecuta campañas masivas |
| `noc-worker-notifications` | — | Alertas (Slack, email, SignalR) |
| `noc-worker-ai` | — | Stub IA (responde pass_to_agent) |

## Setup rápido

### Prerrequisitos

- Docker Desktop
- Node.js 22+ y pnpm (para desarrollo local del frontend)
- Evolution API desplegada externamente
- ngrok o túnel similar para exponer webhooks

### 1. Clonar y configurar

```bash
git clone https://github.com/luchoov/NOC.git
cd NOC
cp .env.example .env
```

Editar `.env` con los valores reales:

```env
# Obligatorios
POSTGRES_PASSWORD=tu_password_seguro
JWT_SECRET=clave_min_32_caracteres
ENCRYPTION_MASTER_KEY=$(openssl rand -base64 32)
EVOLUTION_API_URL=https://tu-evolution-api.com
EVOLUTION_API_KEY=tu_api_key

# Para webhooks (Evolution necesita llegar al backend)
NOC_PUBLIC_BASE_URL=https://tu-ngrok-url.ngrok-free.dev
```

### 2. Levantar servicios

```bash
docker compose up -d --build
```

Verificar que todo esté corriendo:

```bash
docker compose ps
curl http://localhost:8080/health
```

### 3. Frontend en desarrollo local (opcional)

Si preferís correr el frontend fuera de Docker para desarrollo:

```bash
cd src/noc-frontend
pnpm install
pnpm dev
```

El frontend proxea `/api/*` a `localhost:8080` vía `next.config.ts` rewrites.

### 4. Exponer webhooks

Evolution API necesita enviar webhooks al backend. Usar ngrok:

```bash
ngrok http 8080
```

Actualizar `NOC_PUBLIC_BASE_URL` en `.env` con la URL de ngrok y reiniciar:

```bash
docker compose up -d noc-web
```

## Estructura del proyecto

```
NOC/
├── docker-compose.yml
├── .env.example
├── neuryn-omnichannel-arquitectura-v2.md    # Documento de arquitectura (autoritativo)
│
├── src/
│   ├── NOC.Shared/                          # Dominio compartido
│   │   ├── Domain/
│   │   │   ├── Entities/                    # Agent, Contact, Conversation, Inbox, Message, ProxyOutbound...
│   │   │   └── Enums/                       # ChannelType, ConversationStatus, ProxyProtocol...
│   │   ├── Events/                          # Contratos de eventos (records)
│   │   └── Infrastructure/
│   │       ├── Crypto/AesGcmEncryptor.cs    # Cifrado AES-256-GCM para secretos
│   │       ├── Data/                        # DbContext, EF configurations, migrations
│   │       ├── Evolution/                   # Cliente Evolution API con soporte proxy
│   │       ├── Outbox/                      # Transactional Outbox Pattern
│   │       └── Redis/                       # Redis Streams publisher
│   │
│   ├── NOC.Web/                             # API principal
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs            # Login JWT, refresh token
│   │   │   ├── InboxController.cs           # CRUD inboxes + Evolution lifecycle
│   │   │   ├── ConversationController.cs    # Bandeja, asignación, estados
│   │   │   ├── MessageController.cs         # Historial + envío (con resolución @lid)
│   │   │   ├── ContactController.cs         # Mini-CRM: CRUD, tags, attrs
│   │   │   ├── ProxyController.cs           # Proxies outbound: CRUD, test, assign
│   │   │   ├── EvolutionWebhookController.cs# Webhooks de Evolution
│   │   │   └── AuditController.cs           # Log de auditoría
│   │   └── Program.cs                       # DI, middleware, JWT config, CORS
│   │
│   ├── NOC.Worker.Messaging/                # Worker: mensajes inbound/outbound
│   ├── NOC.Worker.Campaigns/                # Worker: campañas masivas
│   ├── NOC.Worker.Notifications/            # Worker: alertas
│   ├── NOC.Worker.AI/                       # Worker: stub IA
│   │
│   └── noc-frontend/                        # Frontend Next.js 16
│       ├── src/
│       │   ├── app/
│       │   │   ├── (auth)/login/            # Login page
│       │   │   └── (dashboard)/
│       │   │       ├── inbox/               # Chat 3 paneles (Chatwoot-style)
│       │   │       ├── contacts/            # Lista + detalle de contactos
│       │   │       ├── campaigns/           # Placeholder
│       │   │       └── settings/
│       │   │           ├── inboxes/         # Gestión bandejas + Evolution QR
│       │   │           └── proxies/         # Gestión proxies outbound
│       │   ├── components/
│       │   │   ├── chat/                    # ChatView, MessageBubble, MessageInput, ChatHeader
│       │   │   ├── inbox/                   # ConversationListItem, ContactPanel
│       │   │   ├── contacts/               # ContactListItem, ContactCreateModal
│       │   │   ├── settings/               # InboxCard, InboxCreateForm, EvolutionQrPanel
│       │   │   └── layout/                 # Sidebar, Header, ReconnectionBanner
│       │   ├── lib/
│       │   │   ├── api/                     # 7 módulos API (auth, inboxes, conversations, messages, contacts, proxies, audit)
│       │   │   ├── signalr/                # Cliente SignalR + hooks real-time
│       │   │   ├── store/                  # Zustand (auth, UI)
│       │   │   ├── validations/            # Schemas Zod v4
│       │   │   └── utils/                  # formatDate, formatPhone, constants
│       │   └── types/api.ts                # TypeScript types (mirrors backend DTOs)
│       └── Dockerfile                       # Multi-stage build
```

## Frontend — Estado actual

### Stack frontend

Next.js 16 | React 19 | TypeScript strict | Tailwind CSS v4 | Zustand 5 | TanStack Query 5 | SignalR 10 | React Hook Form + Zod v4 | date-fns | lucide-react | sonner

### Módulos implementados

| Módulo | Estado | Descripción |
|--------|--------|-------------|
| Auth (Login + JWT) | Completo | Login, token refresh automático, auth guard |
| Dashboard Layout | Completo | Sidebar, header, dark mode, responsive |
| Inbox / Chat | Completo | 3 paneles: lista conversaciones, chat, panel contacto |
| Mensajes | Completo | Bubbles con media, delivery status, notas privadas, paginación keyset |
| SignalR Real-time | Completo | Mensajes en tiempo real, asignación, estados (graceful fallback) |
| Settings: Bandejas | Completo | CRUD inboxes, Evolution provision/connect, QR scanning, status polling |
| Settings: Proxies | Completo | Split-panel: crear, test conectividad, asignar a inbox |
| Contactos | Completo | Lista, detalle, crear, tags, custom attributes |
| Campañas | Placeholder | Pendiente Sprint 4+ |

### Patrones clave del frontend

- **JWT en memoria** (Zustand) — nunca localStorage
- **Keyset pagination** en conversaciones y mensajes
- **Optimistic concurrency** via `rowVersion` (409 Conflict handling)
- **API rewrites** en `next.config.ts`: `/api/*` → `localhost:8080`
- **SignalR directo** a `:8080` (WebSocket no puede ir por rewrites)

## Backend — Funcionalidades implementadas

### Auth
- Login JWT (access + refresh token)
- Compatibilidad con hashes legacy
- Roles: ADMIN, SUPERVISOR, AGENT

### Inboxes + Evolution API
- CRUD de bandejas WhatsApp
- Lifecycle completo: crear instancia → provisionar → conectar → QR → escanear → connected
- Configuración automática de webhooks con `NOC_PUBLIC_BASE_URL`
- Diagnóstico de webhook en status refresh
- Soporte proxy por inbox para llamadas a Evolution

### Mensajería
- Recepción inbound via webhooks Evolution (root-level payload, `remoteJid`, `pushName`)
- Envío outbound via Evolution API con resolución de destinatarios `@lid`
- Fix de duplicación de contactos/conversaciones por `@lid` (resolve via Evolution contacts)
- Worker de mensajería con consumer groups Redis Streams

### Proxies Outbound
- CRUD con cifrado de credenciales (AES-256-GCM)
- Test de conectividad HTTP real (contra httpbin.org)
- Asignación/desasignación a inboxes
- Estados: ACTIVE → ASSIGNED → FAILING → DISABLED

### Contactos
- CRUD con búsqueda full-text (tsvector)
- Tags y custom attributes (JSONB)
- Constraint unique por teléfono

## Desarrollo

### Proceso de trabajo

El desarrollo se realizó con **Claude Code** (Claude Opus) y **Codex** trabajando en conjunto:

- **Claude Code**: Construyó el frontend completo (Sprints 1-3), la integración con el backend, y la infraestructura Docker
- **Codex**: Realizó fixes críticos del backend — webhooks Evolution, worker de mensajería inbound, resolución `@lid`, compatibilidad de auth, y validación end-to-end con mensajes reales

### Branches

- `main` — Branch principal con todo el código consolidado
- `claude/eager-hofstadter` — (histórica) Backend original
- `claude/eager-shtern` — (histórica) Frontend original

Ambas branches históricas ya fueron mergeadas a `main`.

### Variables de entorno

Ver `.env.example` para la lista completa. Las críticas:

| Variable | Descripción |
|----------|-------------|
| `JWT_SECRET` | Clave para firmar JWT (min 32 chars) |
| `ENCRYPTION_MASTER_KEY` | Base64 32-byte key para AES-256-GCM |
| `EVOLUTION_API_URL` | URL de la instancia Evolution API |
| `EVOLUTION_API_KEY` | API key de Evolution |
| `NOC_PUBLIC_BASE_URL` | URL pública del backend (para webhooks) |

## Próximos pasos

1. **Persistir identidad externa de contactos** — Guardar `remoteJid`, `lid`, `senderPn` en tabla de aliases en vez de resolver por heurística
2. **Deduplicación automática de contactos** — Herramienta admin para mergear duplicados (hoy es manual SQL)
3. **Campañas masivas** — UI + integración con worker de campañas
4. **Tests automatizados** — Cobertura para webhook inbound, resolución `@lid`, envío outbound
5. **Deploy a Dokploy** — Configuración de producción

## Documentación

- [`neuryn-omnichannel-arquitectura-v2.md`](neuryn-omnichannel-arquitectura-v2.md) — Documento de arquitectura autoritativo. Todas las decisiones técnicas están justificadas ahí. No implementar algo diferente sin actualizar el doc.
