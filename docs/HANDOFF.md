# NOC — Handoff Document

## Qué es NOC

**Neuryn Omnichannel (NOC)** es una plataforma de mensajería omnicanal single-tenant para Neuryn Software. Tiene dos dominios:

- **Oficial**: Meta/WhatsApp Business API para atención al cliente
- **No Oficial**: Evolution API (ya desplegada en `https://ev.neuryn.tech`) para campañas masivas

La arquitectura completa está documentada en `docs/architecture-v2.md` (1,669 líneas). **Este documento es la fuente de verdad absoluta** — no desviarse sin discusión previa.

---

## Stack Tecnológico

| Capa | Tecnología |
|------|-----------|
| Backend | .NET 10 / ASP.NET Core 10 (monolito modular + workers) |
| Base de datos | PostgreSQL 18 con `uuidv7()` nativo |
| Cache/Bus | Redis 8 (Streams, cache, rate limiting, locks) |
| Media | MinIO (S3-compatible, self-hosted) |
| Frontend | Next.js 16 (TypeScript, React 19) |
| Infra | Docker Compose (9 servicios), deploy via Dokploy |

---

## Estado Actual — Block 1 (Foundation) ✅ COMPLETO

### Lo que se implementó

#### 1. Estructura del Proyecto (6 proyectos .NET + 1 Next.js)
```
src/
  NOC.Shared/          → Librería compartida (entidades, enums, DbContext, outbox, crypto)
  NOC.Web/             → API REST ASP.NET Core (controllers, auth, middleware)
  NOC.Worker.Messaging/    → Worker de mensajería
  NOC.Worker.Campaigns/    → Worker de campañas
  NOC.Worker.Notifications/→ Worker de notificaciones
  NOC.Worker.AI/           → Worker de IA
  noc-frontend/        → Next.js 16 stub
```

#### 2. Schema de Base de Datos (13 tablas)
Todas las entidades están en `src/NOC.Shared/Domain/Entities/`:

| Tabla | Descripción |
|-------|-------------|
| `agents` | Usuarios del sistema (admin, supervisor, agent) |
| `inboxes` | Canales de WhatsApp (oficial/no oficial) |
| `inbox_agents` | Relación M2M inbox↔agent |
| `contacts` | Contactos con búsqueda full-text (tsvector español) |
| `contact_tags` | Tags por contacto |
| `conversations` | Conversaciones con optimistic locking (row_version) |
| `messages` | Mensajes inmutables con keyset pagination |
| `message_status_events` | Historial de delivery (append-only) |
| `audit_events` | Log de auditoría inmutable |
| `outbox_events` | Transactional outbox para Redis Streams |
| `campaigns` | Campañas masivas con throttling |
| `campaign_recipients` | Destinatarios con SKIP LOCKED para batch claiming |
| `proxy_outbounds` | Proxies de salida para Evolution API |

**Configuraciones EF Core**: `src/NOC.Shared/Infrastructure/Data/Configurations/` — 13 archivos con índices críticos (keyset pagination, dedup, partial unique, GIN full-text).

**Convención de nombres**: Se usa `EFCore.NamingConventions` para snake_case automático en todas las tablas y columnas.

**Enums**: Se almacenan como **strings** (`HasConversion<string>()`) en lugar de PG enums nativos. Razón: incompatibilidad entre Npgsql EF Core 10 y la lectura de PG enums como CLR enums. Los valores se guardan en UPPERCASE (`ADMIN`, `RESOLVED`, etc.).

#### 3. Migración de EF Core
- Archivo: `src/NOC.Web/Migrations/20260319053308_InitialCreate.cs`
- Design-time factory: `src/NOC.Web/DesignTimeDbContextFactory.cs` (necesario porque `Program.cs` lanza excepción sin JWT_SECRET)
- Para generar nuevas migraciones: `dotnet ef migrations add NombreMigración --project src/NOC.Web --startup-project src/NOC.Web`

#### 4. Encriptación AES-256-GCM
- `src/NOC.Shared/Infrastructure/Crypto/AesGcmEncryptor.cs`
- Formato: `[12-byte nonce][16-byte tag][ciphertext]` en base64
- Master key desde env var `ENCRYPTION_MASTER_KEY`

#### 5. Transactional Outbox
- `OutboxWriter.cs` — Agrega evento al DbContext (misma transacción, NO hace SaveChanges)
- `OutboxPublisherService.cs` — BackgroundService que pollea cada 500ms, publica a Redis Streams
- `RedisStreamPublisher.cs` — Wrapper de `StreamAddAsync`
- Se ve activo en los logs del contenedor (`SELECT ... FROM outbox_events WHERE NOT published`)

#### 6. JWT Authentication
- `TokenService.cs` — HS256, 15min access token, 64-byte opaque refresh token (hasheado con SHA256 en DB)
- `AuthController.cs`:
  - `POST /api/auth/login` — BCrypt Enhanced verify, devuelve token pair
  - `POST /api/auth/refresh` — Rotación de refresh token
  - `POST /api/auth/logout` — Invalida refresh token
- Claims: `sub` (agent ID), `email`, `role`, `jti`, `name`

#### 7. Audit Log
- `AuditService.cs` — Log explícito con IP, actor, payload
- `AuditController.cs` — `GET /api/audit` (Admin/Supervisor only, keyset pagination)

#### 8. Middleware
- `CorrelationIdMiddleware.cs` — X-Correlation-Id en headers y Serilog LogContext

#### 9. Docker Compose (9 servicios)
Todos funcionando y healthy:
```
postgres (18-alpine)     → :5432
redis (8-alpine)         → :6379
minio (latest)           → :9000/:9001
noc-web                  → :8080
noc-worker-messaging     → (background)
noc-worker-campaigns     → (background)
noc-worker-notifications → (background)
noc-worker-ai            → (background)
noc-frontend             → :3000
```

---

## Cómo Levantar el Proyecto

### Opción 1: Docker Compose (todo containerizado)
```bash
# Copiar .env.example a .env y configurar secretos reales
cp .env.example .env
# Editar .env: JWT_SECRET (min 32 chars), ENCRYPTION_MASTER_KEY (base64 32 bytes)

# Levantar todo
docker compose up --build -d

# Aplicar migraciones (si es la primera vez)
dotnet ef database update --project src/NOC.Web --startup-project src/NOC.Web \
  --connection "Host=localhost;Database=noc;Username=noc_user;Password=changeme_pg_password"

# Seed admin user
docker compose exec -T postgres psql -U noc_user -d noc -c \
  "INSERT INTO agents (id, name, email, password_hash, password_version, role, is_active, created_at, updated_at) \
   VALUES (gen_random_uuid(), 'Admin', 'admin@neuryn.tech', \
   E'\$2a\$12\$1abauXWen6mx7bAq7m99nu94q9LlqsmFrldLfWjffkJzopZ861h.i', \
   1, 'ADMIN', true, now(), now());"
# Password: Admin123! (BCrypt Enhanced hash)
```

### Opción 2: Híbrido (infra en Docker, apps local)
```bash
# Solo infraestructura
docker compose up -d postgres redis minio

# API
dotnet run --project src/NOC.Web

# Frontend
cd src/noc-frontend && npm install && npm run dev

# Workers (cada uno en terminal separada)
dotnet run --project src/NOC.Worker.Messaging
dotnet run --project src/NOC.Worker.Campaigns
dotnet run --project src/NOC.Worker.Notifications
dotnet run --project src/NOC.Worker.AI
```

**Nota**: Para correr local, necesitas configurar env vars o `appsettings.Development.json` con:
- `ConnectionStrings:Postgres` = `Host=localhost;Database=noc;Username=noc_user;Password=changeme_pg_password`
- `ConnectionStrings:Redis` = `localhost:6379`
- `Jwt:Secret` = (min 32 chars)

---

## Cómo Testear

### Health Check
```bash
curl http://localhost:8080/health
# → {"status":"healthy","service":"noc-web"}
```

### Login
```bash
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@neuryn.tech","password":"Admin123!"}'
# → {"accessToken":"eyJ...","refreshToken":"...","expiresAt":"..."}
```

### Endpoint protegido (con token)
```bash
TOKEN="<accessToken del login>"
curl http://localhost:8080/api/audit -H "Authorization: Bearer $TOKEN"
# → [] (vacío pero 200 OK)
```

### Endpoint protegido (sin token)
```bash
curl -v http://localhost:8080/api/audit
# → 401 Unauthorized
```

### Refresh Token
```bash
curl -X POST http://localhost:8080/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refreshToken del login>"}'
# → Nuevo par de tokens
```

### Swagger UI
Abrir en navegador: `http://localhost:8080/swagger`

### Frontend
Abrir en navegador: `http://localhost:3000`

### Base de datos
```bash
docker compose exec -T postgres psql -U noc_user -d noc -c "\dt"    # tablas
docker compose exec -T postgres psql -U noc_user -d noc -c "\di"    # índices
docker compose exec -T postgres psql -U noc_user -d noc -c "\dT+"   # tipos (no hay PG enums, son strings)
```

---

## Qué Sigue — Block 2 (Evolution API Integration)

Referencia: Sección 8 del documento de arquitectura (`docs/architecture-v2.md`).

### Resumen de Block 2

Block 2 conecta NOC con la **Evolution API** (ya desplegada en `https://ev.neuryn.tech`) para el dominio no oficial (campañas masivas). NOC actúa como **cliente HTTP** de Evolution API.

### Tareas de Block 2

#### 2.1 Evolution API Client
- Crear `src/NOC.Shared/Infrastructure/Evolution/EvolutionApiClient.cs`
- HTTP client tipado que se comunica con Evolution API
- Endpoints clave: crear instancia, conectar (QR), enviar mensaje, obtener estado
- Usar `IHttpClientFactory` con resilience (Polly)
- Credenciales: `EVOLUTION_API_URL` y `EVOLUTION_API_KEY` desde env vars

#### 2.2 Webhook Controller
- `src/NOC.Web/Webhooks/EvolutionWebhookController.cs`
- Recibe webhooks de Evolution API (mensajes entrantes, status updates)
- Idempotencia dual: Redis SET NX + PG UNIQUE en `messages.external_id`
- Debe crear contactos, conversaciones y mensajes automáticamente

#### 2.3 Inbox Management (CRUD)
- `src/NOC.Web/Controllers/InboxController.cs`
- CRUD de inboxes (canales de WhatsApp)
- Lifecycle: crear inbox → crear instancia en Evolution → conectar (obtener QR) → estado "connected"
- Encriptar tokens con AES-256-GCM antes de guardar
- Ban tracking (OK → SUSPECTED → BANNED)

#### 2.4 Contact Management (CRUD)
- `src/NOC.Web/Controllers/ContactController.cs`
- CRUD con búsqueda full-text (español)
- Tags management
- Importación/exportación masiva

#### 2.5 Conversation Management
- `src/NOC.Web/Controllers/ConversationController.cs`
- Lista de conversaciones (inbox tray) con los índices ya creados
- Asignación de agentes
- Cambios de estado con optimistic locking (`row_version`)
- Solo una conversación activa por contacto+inbox (partial unique index)

#### 2.6 Messaging
- `src/NOC.Web/Controllers/MessageController.cs`
- Envío de mensajes vía Evolution API
- Keyset pagination (NUNCA usar OFFSET)
- Soporte para tipos: text, image, audio, video, document, sticker, location, template
- Internal notes (no se envían a WhatsApp)

#### 2.7 Worker: Messaging
- Implementar `src/NOC.Worker.Messaging/MessagingWorker.cs`
- Consumir del Redis Stream (`noc:messages`)
- Procesar mensajes entrantes del webhook
- Actualizar delivery status

#### 2.8 Media Storage (MinIO)
- `src/NOC.Shared/Infrastructure/MinIO/MinioStorageService.cs`
- Upload/download de archivos multimedia
- Generar URLs pre-firmadas
- Bucket: `noc-media`

#### 2.9 Campaign Engine
- Implementar `src/NOC.Worker.Campaigns/CampaignWorker.cs`
- SKIP LOCKED para batch claiming de recipients
- Throttling configurable (msgs/min, delay entre mensajes)
- Ventana de envío (hora inicio/fin + timezone)
- Ban automático cuando ratio de fallos > threshold

### Prioridad sugerida para Block 2
1. Evolution API Client (base para todo lo demás)
2. Inbox Management + lifecycle
3. Webhook Controller (recibir mensajes)
4. Contact Management
5. Conversation Management
6. Message Controller (enviar mensajes)
7. Media Storage
8. Messaging Worker
9. Campaign Engine

---

## Decisiones Técnicas Importantes

| Decisión | Detalle | Razón |
|----------|---------|-------|
| Enums como strings | `HasConversion<string>()` en vez de PG enums | Npgsql EF Core 10 no mapea correctamente PG enums ↔ C# enums en runtime |
| Snake case | `EFCore.NamingConventions` con `UseSnakeCaseNamingConvention()` | Columnas en snake_case según doc de arquitectura |
| BCrypt Enhanced | `BCrypt.Net.BCrypt.EnhancedVerify` | Usa SHA384 preprocessing antes de BCrypt — NO compatible con hash BCrypt normal |
| Design-time factory | `DesignTimeDbContextFactory` en NOC.Web | `Program.cs` lanza si falta `JWT_SECRET`, necesario bypass para `dotnet ef` |
| .NET 10 GA | Docker images `10.0` (no `10.0-preview`) | SDK 10.0.101 instalado, imágenes GA disponibles |
| No PG enums nativos | Usamos `varchar` para enums | El approach `HasPostgresEnum<T>()` + `MapEnum<T>()` falla en runtime con EF Core 10 |

---

## Archivos Clave

| Archivo | Propósito |
|---------|-----------|
| `docs/architecture-v2.md` | Documento de arquitectura (fuente de verdad) |
| `.env.example` | Variables de entorno (NO tiene secretos reales) |
| `.env` | Variables reales (gitignored, tiene credenciales Evolution API) |
| `docker-compose.yml` | 9 servicios |
| `src/NOC.Web/Program.cs` | Entry point del API |
| `src/NOC.Shared/Infrastructure/Data/NocDbContext.cs` | DbContext con 13 DbSets y enum conversions |
| `src/NOC.Web/DesignTimeDbContextFactory.cs` | Factory para `dotnet ef` commands |
| `src/NOC.Web/Controllers/AuthController.cs` | Login/refresh/logout |
| `src/NOC.Shared/Infrastructure/Outbox/` | Transactional outbox completo |
| `src/NOC.Shared/Infrastructure/Crypto/AesGcmEncryptor.cs` | Encriptación de secretos |
| `Makefile` | Comandos útiles: `make up`, `make down`, `make migrate`, etc. |

---

## Git

- Branch actual: `claude/eager-hofstadter`
- Base: `main` (solo tiene initial commit)
- 2 commits:
  1. `feat: implement Block 1 foundation` (95 files, scaffolding completo)
  2. `fix: Docker Compose build and runtime` (20 files, fixes para que todo corra)
- PR pendiente de crear

---

## Credenciales de Desarrollo

| Servicio | Usuario | Password |
|----------|---------|----------|
| PostgreSQL | `noc_user` | `changeme_pg_password` |
| MinIO | `minioadmin` | `changeme_minio_password` |
| Admin API | `admin@neuryn.tech` | `Admin123!` |
| Evolution API | — | Key en `.env` (NO en `.env.example`) |

---

## Actualizacion Codex - 2026-03-19

### Contexto de esta actualizacion
- Se leyo completo `docs/HANDOFF.md` y `docs/architecture-v2.md`.
- Se continuo con Block 2 (Evolution API integration) siguiendo la prioridad sugerida.

### Implementado en esta iteracion

#### 1) Evolution API Client (Block 2.1) - COMPLETO
- Cliente tipado en `src/NOC.Shared/Infrastructure/Evolution/`:
  - `IEvolutionApiClient.cs`
  - `EvolutionApiClient.cs`
  - `EvolutionApiDtos.cs`
  - `EvolutionApiOptions.cs`
  - `EvolutionApiException.cs`
  - `EvolutionServiceCollectionExtensions.cs`
- Operaciones incluidas:
  - Crear instancia
  - Conectar instancia (QR flow)
  - Enviar mensaje de texto
  - Consultar estado de instancia
- Resilience:
  - `IHttpClientFactory` + Polly
  - Retry exponencial para errores transitorios/429
  - Circuit breaker (5 fallos, 30s)
- Configuracion:
  - Variables `EVOLUTION_API_URL` y `EVOLUTION_API_KEY`
  - Fallback opcional por seccion `EvolutionApi` en appsettings

#### 2) Registro del cliente y runtime config - COMPLETO
- `src/NOC.Web/Program.cs`: agregado `AddEvolutionApiClient(builder.Configuration)`.
- `src/NOC.Web/appsettings.json` y `appsettings.Development.json`:
  - `EvolutionApi:TimeoutSeconds`
  - `EvolutionApi:ApiKeyHeaderName`
- `docker-compose.yml`:
  - Se inyectan `EVOLUTION_API_URL` y `EVOLUTION_API_KEY` a:
    - `noc-web`
    - `noc-worker-messaging`
    - `noc-worker-campaigns`

#### 3) Inbox Management inicial (Block 2.3 parcial) - COMPLETO (backend API)
- Nuevo controller: `src/NOC.Web/Controllers/InboxController.cs`
- Nuevos DTOs: `src/NOC.Web/Inboxes/InboxDtos.cs`
- Endpoints agregados (`Authorize: ADMIN,SUPERVISOR`):
  - `GET /api/inboxes` (filtros por `channelType`, `isActive`)
  - `GET /api/inboxes/{id}`
  - `POST /api/inboxes`
  - `PUT /api/inboxes/{id}`
  - `DELETE /api/inboxes/{id}`
  - `POST /api/inboxes/{id}/provision-evolution`
  - `POST /api/inboxes/{id}/connect`
  - `GET /api/inboxes/{id}/status?refresh=true|false`
- Reglas aplicadas:
  - Secretos (`AccessToken`, `RefreshToken`) se cifran con `AesGcmEncryptor`.
  - Nunca se exponen secretos en responses (`HasAccessToken`/`HasRefreshToken` solamente).
  - Para inboxes no oficiales, se soporta lifecycle de provision + connect con Evolution.
  - `DELETE` protegido: si hay conversaciones/campanas relacionadas responde `409 Conflict`.
- Auditoria agregada:
  - `INBOX_CREATED`
  - `INBOX_UPDATED`
  - `INBOX_CREDENTIALS_UPDATED`
  - `INBOX_BANNED`
  - `INBOX_DELETED`
  - `INBOX_EVOLUTION_PROVISIONED`
  - `INBOX_EVOLUTION_CONNECT_REQUESTED`

### Dependencias agregadas
- `Microsoft.Extensions.Http.Polly` en `NOC.Shared`.

### Verificacion tecnica
- Build ejecutado: `dotnet build NeurynOmnichannel.sln`
- Resultado: OK (0 warnings, 0 errors)

### Estado de Block 2 luego de esta iteracion
- 2.1 Evolution API Client: **hecho**
- 2.3 Inbox Management: **parcial (backend API base hecha)**
- 2.2 Webhook Controller Evolution: pendiente
- 2.4 Contact CRUD: pendiente
- 2.5 Conversation management: pendiente
- 2.6 Message controller: pendiente
- 2.7 Messaging worker logic real: pendiente
- 2.8 MinIO media service completo: pendiente
- 2.9 Campaign engine real: pendiente

### Recomendacion para siguiente iteracion (Claude/Codex)
1. Implementar `EvolutionWebhookController` con idempotencia dual:
   - Redis `SET NX` (fast path)
   - Unique durable en PostgreSQL (`messages.external_id`)
2. Conectar webhook con creacion/upsert de contact + conversation + message.
3. Publicar evento en outbox hacia stream de mensajeria entrante (`messaging:incoming`) con contrato versionado.
4. Agregar pruebas de integracion para:
   - deduplicacion webhook
   - create/provision/connect inbox lifecycle

### Actualizacion incremental Codex - 2026-03-19 (Webhooks + Contacts)

#### 4) Evolution Webhooks (Block 2.2 parcial) - COMPLETO (ingesta + outbox)
- Nuevo controller: `src/NOC.Web/Controllers/EvolutionWebhookController.cs`
- Nuevos contratos de evento:
  - `src/NOC.Shared/Events/EvolutionMessageWebhookReceivedEvent.cs`
  - `src/NOC.Shared/Events/EvolutionStatusWebhookReceivedEvent.cs`
- Endpoints agregados:
  - `POST /webhooks/evolution/{inboxId}/messages`
  - `POST /webhooks/evolution/{inboxId}/status`
- Comportamiento implementado:
  - Valida que el inbox exista y sea `WHATSAPP_UNOFFICIAL`.
  - Deduplicacion rapida con Redis `SET NX` + TTL.
  - Escribe eventos a outbox (sin logica de dominio pesada):
    - `stream:messaging:incoming`
    - `stream:status:updates`
  - En webhook de status actualiza `inboxes.evolution_session_status` y `evolution_last_heartbeat`.

#### 5) Contact Management (Block 2.4 parcial) - COMPLETO (backend API)
- Nuevo controller: `src/NOC.Web/Controllers/ContactController.cs`
- Nuevos DTOs: `src/NOC.Web/Contacts/ContactDtos.cs`
- Endpoints agregados:
  - `GET /api/contacts`
  - `GET /api/contacts/{id}`
  - `POST /api/contacts`
  - `PUT /api/contacts/{id}`
  - `DELETE /api/contacts/{id}`
  - `POST /api/contacts/{id}/tags`
  - `DELETE /api/contacts/{id}/tags/{tag}`
- Incluye:
  - Busqueda por texto (ILIKE) en phone/name/email.
  - Filtro por tag.
  - Manejo de tags por contacto.
  - Guardado seguro de `custom_attrs` JSON.
  - Auditoria: create/update/delete/tag add/tag remove.

#### Estado actualizado Block 2
- 2.1 Evolution API Client: **hecho**
- 2.2 Webhook Controller Evolution: **parcial (ingesta+dedup+outbox hecho; falta procesamiento worker completo)**
- 2.3 Inbox Management: **parcial (backend API hecha)**
- 2.4 Contact Management: **parcial (CRUD+tags hecho; falta import/export masivo)**
- 2.5 Conversation management: pendiente
- 2.6 Message controller: pendiente
- 2.7 Messaging worker logic real: pendiente
- 2.8 MinIO media service completo: pendiente
- 2.9 Campaign engine real: pendiente

#### Verificacion
- Build repetido luego de estos cambios: `dotnet build NeurynOmnichannel.sln`
- Resultado: OK (0 warnings, 0 errors)

### Actualizacion incremental Codex - 2026-03-19 (Conversations)

#### 6) Conversation Management (Block 2.5 parcial) - COMPLETO (backend API base)
- Nuevo controller: `src/NOC.Web/Controllers/ConversationController.cs`
- Nuevos DTOs: `src/NOC.Web/Conversations/ConversationDtos.cs`
- Endpoints agregados:
  - `GET /api/conversations`
  - `GET /api/conversations/{id}`
  - `POST /api/conversations/{id}/assign`
  - `POST /api/conversations/{id}/status`
- Incluye:
  - Filtros de bandeja por inbox/status/assignedTo.
  - Control de acceso por inbox para agentes (admin/supervisor bypass).
  - Asignacion con optimistic locking (`row_version`) via SQL atomico.
  - Cambio de estado con optimistic locking (`row_version`) via SQL atomico.
  - Respuesta `409 Conflict` en modificacion concurrente.
  - Auditoria para asignaciones y cambios de estado.

#### Estado actualizado Block 2
- 2.1 Evolution API Client: **hecho**
- 2.2 Webhook Controller Evolution: **parcial (ingesta+dedup+outbox hecho; falta procesamiento worker completo)**
- 2.3 Inbox Management: **parcial (backend API hecha)**
- 2.4 Contact Management: **parcial (CRUD+tags hecho; falta import/export masivo)**
- 2.5 Conversation management: **parcial (API base + optimistic locking hecho)**
- 2.6 Message controller: pendiente
- 2.7 Messaging worker logic real: pendiente
- 2.8 MinIO media service completo: pendiente
- 2.9 Campaign engine real: pendiente

#### Verificacion
- Build repetido luego de estos cambios: `dotnet build NeurynOmnichannel.sln`
- Resultado: OK (0 warnings, 0 errors)

### Actualizacion incremental Codex - 2026-03-19 (Messages)

#### 7) Messaging API (Block 2.6 parcial) - COMPLETO (backend API base)
- Nuevo controller: `src/NOC.Web/Controllers/MessageController.cs`
- Nuevos DTOs: `src/NOC.Web/Messages/MessageDtos.cs`
- Endpoints agregados:
  - `GET /api/conversations/{conversationId}/messages`
  - `POST /api/conversations/{conversationId}/messages`
- Incluye:
  - Listado con paginacion keyset por `(created_at, id)` cuando se proveen cursores.
  - Filtro para incluir/excluir notas internas.
  - Envio outbound por Evolution API para inboxes `WHATSAPP_UNOFFICIAL`.
  - Soporte de `INTERNAL_NOTE` (no envia a proveedor).
  - Actualizacion de campos desnormalizados de conversacion (`last_message_*`).
  - Auditoria para envio y notas internas.
- Nota actual:
  - Outbound de canal oficial (`WHATSAPP_OFFICIAL`) devuelve `501 Not Implemented` por ahora.
  - Actualmente se envio solo texto en esta primera version del endpoint.

#### Estado actualizado Block 2
- 2.1 Evolution API Client: **hecho**
- 2.2 Webhook Controller Evolution: **parcial (ingesta+dedup+outbox hecho; falta procesamiento worker completo)**
- 2.3 Inbox Management: **parcial (backend API hecha)**
- 2.4 Contact Management: **parcial (CRUD+tags hecho; falta import/export masivo)**
- 2.5 Conversation management: **parcial (API base + optimistic locking hecho)**
- 2.6 Message controller: **parcial (API base + envio texto unofficial + notas internas)**
- 2.7 Messaging worker logic real: pendiente
- 2.8 MinIO media service completo: pendiente
- 2.9 Campaign engine real: pendiente

#### Verificacion
- Build repetido luego de estos cambios: `dotnet build NeurynOmnichannel.sln`
- Resultado: OK (0 warnings, 0 errors)

### Actualizacion incremental Codex - 2026-03-19 (Messaging Worker)

#### 8) Worker de mensajeria (Block 2.7 parcial) - COMPLETO (pipeline inbound base)
- `src/NOC.Worker.Messaging/Program.cs` actualizado:
  - Registra `NocDbContext` (Postgres)
  - Registra `IConnectionMultiplexer` (Redis)
- `src/NOC.Worker.Messaging/Worker.cs` reescrito (ya no stub):
  - Crea/usa consumer group `messaging-workers` en stream `stream:messaging:incoming`.
  - Consume eventos `EvolutionMessageWebhookReceivedEvent` publicados por outbox.
  - Aplica barrera de idempotencia durable (`messages.external_id` UNIQUE).
  - Upsert de contacto por telefono.
  - Threading policy base:
    - Reusa conversacion activa si existe.
    - Reabre conversacion `RESOLVED` si entra en ventana de reapertura configurable.
    - Crea nueva conversacion si no hay activa/reapertura aplicable.
  - Inserta mensaje inbound y actualiza campos desnormalizados de conversacion.
  - Ack de Redis stream por mensaje procesado.

#### Estado actualizado Block 2
- 2.1 Evolution API Client: **hecho**
- 2.2 Webhook Controller Evolution: **parcial (ingesta+dedup+outbox hecho)**
- 2.3 Inbox Management: **parcial (backend API hecha)**
- 2.4 Contact Management: **parcial (CRUD+tags hecho; falta import/export masivo)**
- 2.5 Conversation management: **parcial (API base + optimistic locking hecho)**
- 2.6 Message controller: **parcial (API base + envio texto unofficial + notas internas)**
- 2.7 Messaging worker logic real: **parcial (pipeline inbound base implementado)**
- 2.8 MinIO media service completo: pendiente
- 2.9 Campaign engine real: pendiente

#### Verificacion
- Build repetido luego de estos cambios: `dotnet build NeurynOmnichannel.sln`
- Resultado: OK (0 warnings, 0 errors)
