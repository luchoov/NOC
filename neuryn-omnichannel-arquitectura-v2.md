# Neuryn Omnichannel — Documento de Arquitectura v2.0
**Versión:** 2.2
**Fecha:** Marzo 2026
**Empresa:** Neuryn Software
**Stack:** C# .NET 10 · PostgreSQL 18 · Redis 8 · MinIO · Next.js 16 · Docker / Dokploy

> **Sobre este documento:** Es la base formal para el desarrollo con Claude Code y Codex.
> Cada decisión arquitectónica tiene su justificación. No implementar algo diferente sin actualizar este doc.

---

## Estado de Implementación (Marzo 2026)

> Esta sección refleja qué partes del sistema están implementadas y funcionando.

### Backend (implementado con Claude Code + Codex)

| Componente | Estado | Notas |
|-----------|--------|-------|
| NOC.Web (API REST) | Operativo | Auth, Inboxes, Conversations, Messages, Contacts, Proxies, Audit, Webhooks |
| NOC.Worker.Messaging | Operativo | Inbound/outbound, resolución @lid, dedup conversaciones |
| NOC.Worker.Campaigns | Scaffold | Estructura lista, lógica de ejecución pendiente |
| NOC.Worker.Notifications | Scaffold | Estructura lista, integración Slack/email pendiente |
| NOC.Worker.AI | Stub | Responde pass_to_agent |
| Evolution API Client | Operativo | Provision, connect, QR, status, webhook config, send, findContacts, proxy support |
| Transactional Outbox | Operativo | Outbox writer + publisher via Redis Streams |
| Proxy Outbound | Operativo | CRUD, test HTTP, asignación a inboxes, cifrado credenciales |
| PostgreSQL Migrations | Aplicadas | Schema completo con UUIDv7, enums, índices |

### Frontend (implementado con Claude Code)

| Módulo | Estado |
|--------|--------|
| Auth (Login + JWT refresh) | Completo |
| Dashboard Layout (sidebar, header, dark mode) | Completo |
| Inbox/Chat (3 paneles, SignalR real-time) | Completo |
| Settings: Bandejas (Evolution lifecycle + QR) | Completo |
| Settings: Proxies (split-panel, test, assign) | Completo |
| Contactos (lista, detalle, crear, tags) | Completo |
| Campañas | Placeholder |

### Infraestructura

| Componente | Estado |
|-----------|--------|
| Docker Compose (9 servicios) | Operativo |
| Frontend Dockerfile (multi-stage) | Operativo |
| .env.example documentado | Completo |
| NOC_PUBLIC_BASE_URL para webhooks | Configurado |

### Pendientes conocidos

1. Persistir identidad externa de contactos (tabla de aliases vs resolución por heurística)
2. Deduplicación automática de contactos (hoy es manual SQL)
3. UI de campañas masivas
4. Tests automatizados (webhook inbound, @lid, outbound)
5. Deploy a Dokploy

---

## Tabla de Contenidos

1. [Visión General](#1-visión-general)
2. [Principios de Diseño](#2-principios-de-diseño)
3. [Arquitectura: Modular Monolith + Workers](#3-arquitectura-modular-monolith--workers)
4. [Módulos y Responsabilidades](#4-módulos-y-responsabilidades)
5. [Modelo de Datos](#5-modelo-de-datos)
6. [Modelo de Concurrencia](#6-modelo-de-concurrencia)
7. [Transactional Outbox Pattern](#7-transactional-outbox-pattern)
8. [Motor de Campañas Masivas](#8-motor-de-campañas-masivas)
9. [Flujos de Negocio Críticos](#9-flujos-de-negocio-críticos)
10. [Evolution API — Session Lifecycle](#10-evolution-api--session-lifecycle)
11. [Media Storage (MinIO)](#11-media-storage-minio)
12. [Preparación para IA](#12-preparación-para-ia)
13. [Observabilidad](#13-observabilidad)
14. [Seguridad](#14-seguridad)
15. [Infraestructura y Deploy](#15-infraestructura-y-deploy)
16. [Estrategia de Implementación por Fases](#16-estrategia-de-implementación-por-fases)
17. [Decisiones Técnicas (ADRs)](#17-decisiones-técnicas-adrs)

---

## 1. Visión General

### Qué es Neuryn Omnichannel (NOC)

NOC es un **módulo de mensajería centralizado, single-tenant, instalable por proyecto** que funciona como motor de comunicación transversal de Neuryn Software. Cada cliente o proyecto recibe su propia instancia desplegada de forma completamente independiente.

No es SaaS. No hay multi-tenancy. El aislamiento es por instancia.

### Los dos dominios del sistema

| Dimensión | Canal Oficial (Meta API) | Canal No Oficial (Evolution API) |
|-----------|--------------------------|----------------------------------|
| Propósito | Atención al cliente, soporte, ventas | Campañas masivas, marketing activo |
| Modelo | Conversacional bidireccional | Broadcast con tracking de entrega |
| Riesgo | Bajo (cuenta verificada) | Alto (número descartable) |
| Sesión | Stateless por mensaje | WebSocket persistente por número |
| IA preparada | Sí | No (fuera de scope) |
| Regulación | Alta (opt-in, plantillas aprobadas) | Ninguna formal |

Estos dos dominios **no comparten abstracciones de negocio**. Comparten infraestructura (DB, Redis, MinIO), pero tienen tablas operativas, workers, streams y políticas de error independientes. Mezclarlos sería un error de diseño grave.

### Target de uso

NOC está diseñado para instancias de **volumen moderado**: equipos de 2-20 agentes, campañas de hasta 50.000 destinatarios, 5-10 inboxes simultáneos. Si un cliente supera este orden de magnitud, el documento deberá revisarse.

---

## 2. Principios de Diseño

**1. Single-tenant by design.**  
Una DB, una instancia Redis, un conjunto de contenedores por cliente. Sin row-level security multi-tenant, sin esquemas compartidos. El aislamiento es físico.

**2. Modular monolith + workers especializados.**  
Un único dominio compartido (`NOC.Shared`), un único esquema de DB, pero procesos separados por responsabilidad. No microservicios reales: no hay bases de datos por servicio, no hay contratos de API entre servicios internos. La separación es de proceso/deployment, no de dominio.

**3. Outbox-first para eventos.**  
Ningún evento se publica al bus sin antes persistirse en la misma transacción de negocio. El outbox es la única garantía de consistencia entre DB y Redis Streams.

**4. Idempotencia en dos capas.**  
Primera barrera rápida en Redis (SET NX). Segunda barrera durable en PostgreSQL (UNIQUE constraints). Ambas son necesarias; ninguna es suficiente sola.

**5. Concurrencia controlada, no ilimitada.**  
El paralelismo tiene límites explícitos por inbox y por tipo de operación. No se crean Tasks sin bound. Se usa backpressure con `Channel<T>` de .NET.

**6. Redis es dependencia central — diseñada como tal.**  
Redis asume múltiples roles (caché, streams, rate limiting, locks, coordinación). Esto es una decisión explícita, no un accidente. La consecuencia es que Redis requiere persistencia AOF habilitada y monitoreo activo. Si Redis cae, partes del sistema se degradan de forma controlada — no colapsan silenciosamente.

**7. `messages` es la tabla más peligrosa.**  
Crece más rápido que todo lo demás. Desde el día uno: keyset pagination, índices cuidados, política de retención y archivado definida, sin queries de agregación sobre ella en runtime crítico.

**8. AI-ready sin AI.**  
Los contratos de eventos para IA existen desde Fase 1. El AI Hook Service es un stub que consume y responde "pass_to_agent". Reemplazarlo no debe requerir cambios en otros servicios.

**9. Observabilidad no es opcional.**  
Logs estructurados, métricas, correlation IDs y alertas son parte del sistema desde el principio, no una capa que se agrega después.

**10. Seguridad básica desde Fase 1.**  
Audit log, cifrado de secretos de proveedores, expiración de tokens y separación de permisos son hygiene, no lujo enterprise. MFA y KMS son Fase 2.

---

## 3. Arquitectura: Modular Monolith + Workers

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        NEURYN OMNICHANNEL                               │
│                                                                         │
│  ┌──────────────────┐   ┌───────────────────┐   ┌──────────────────┐  │
│  │   Meta API       │   │  Evolution API    │   │  Frontend        │  │
│  │  (Oficial)       │   │  (No Oficial)     │   │  (Next.js 16)    │  │
│  └────────┬─────────┘   └─────────┬─────────┘   └────────┬─────────┘  │
│           │                       │                      │             │
│  ┌────────▼───────────────────────▼──────────────────────▼───────────┐ │
│  │                    NOC.Web (ASP.NET Core 10)                       │ │
│  │   REST API · SignalR (realtime) · Webhooks · Auth (JWT)            │ │
│  │   Webhook handlers: validar firma → escribir outbox → 200 OK       │ │
│  └────────────────────────────┬──────────────────────────────────────┘ │
│                               │                                         │
│  ┌────────────────────────────▼──────────────────────────────────────┐ │
│  │                    PostgreSQL 18                                   │ │
│  │   Outbox table ← fuente de verdad para eventos                    │ │
│  └──────┬───────────────────────────────────────┬────────────────────┘ │
│         │ Outbox Relay                           │                      │
│  ┌──────▼──────────────────────────────────────▼────────────────────┐ │
│  │                    Redis 8 Streams (Event Bus)                    │ │
│  │   stream:messaging:incoming  ·  stream:campaigns:events           │ │
│  │   stream:ai:requests         ·  stream:ai:responses               │ │
│  │   stream:status:updates      ·  stream:dlq:{domain}               │ │
│  └──────┬──────────────────────────────────────┬─────────────────────┘ │
│         │                                       │                       │
│  ┌──────▼──────────┐  ┌──────────────────┐  ┌──▼───────────────────┐  │
│  │ NOC.Worker      │  │ NOC.Worker       │  │ NOC.Worker           │  │
│  │ .Messaging      │  │ .Campaigns       │  │ .Notifications       │  │
│  └─────────────────┘  └──────────────────┘  └──────────────────────┘  │
│                                                                         │
│  ┌──────────────────────────────────┐  ┌────────────────────────────┐  │
│  │ NOC.Worker.AI (stub → real)      │  │ MinIO (media storage)      │  │
│  └──────────────────────────────────┘  └────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

### Regla crítica: webhooks son ultralivianos

```
Webhook entra
  → Validar firma HMAC (rechazar si inválida)
  → Deserializar payload mínimo
  → Deduplicar (Redis SET NX)
  → Escribir en outbox table (misma transacción, sin lógica de dominio)
  → Responder 200 OK a Meta/Evolution
  → FIN — todo lo demás es async
```

El handler de webhook nunca llama a Meta API, nunca hace joins complejos, nunca publica directamente al bus. Solo escribe en el outbox.

---

## 4. Módulos y Responsabilidades

### NOC.Web
- REST API para frontend y sistemas externos
- Autenticación JWT (access token 15min + refresh token rotable 7 días)
- SignalR hub para push en tiempo real al frontend
- Recepción y validación de webhooks (Meta y Evolution)
- Escritura en Outbox (nunca publicación directa al bus)

### NOC.Worker.Messaging
- Consume `stream:messaging:incoming` (consumer group)
- Gestiona ciclo de vida de conversaciones
- Aplica threading policy (cuándo reabrir vs crear nueva)
- Llama a Meta API para envíos salientes
- Publica en `stream:ai:requests`
- Consume `stream:ai:responses`
- Actualiza `conversations` y `messages`

### NOC.Worker.Campaigns
- Scheduler: busca campañas con `scheduled_at <= now()` cada 30s
- Executor: batch claiming con leases, throttle por inbox, delays aleatorios
- Consume `stream:status:updates` para tracking de entrega
- Detecta ban y publica en `stream:alerts`
- Actualiza contadores de campaña con reconciliación periódica

### NOC.Worker.Notifications
- Consume alertas de todos los streams
- Envía push via SignalR al frontend
- Envía alertas externas (Slack webhook, email) para eventos críticos
- Eventos críticos: ban detectado, campaña fallida, DLQ con mensajes, lag alto

### NOC.Worker.AI (stub en Fase 1)
- Consume `stream:ai:requests`
- Responde siempre `{ "action": "pass_to_agent" }` sin procesar
- Publica en `stream:ai:responses`
- En Fase 4: reemplazar implementación sin tocar contratos de streams

### NOC.Shared
- Entidades de dominio (EF Core 10 entities)
- Contratos de eventos (records de C#)
- Infraestructura compartida: acceso a Redis, helpers de outbox, abstracciones de media
- Nunca debe contener lógica de negocio específica de un worker

---

## 5. Modelo de Datos

### 5.1 Inboxes

```sql
CREATE TYPE channel_type AS ENUM ('WHATSAPP_OFFICIAL', 'WHATSAPP_UNOFFICIAL');
CREATE TYPE ban_status   AS ENUM ('OK', 'SUSPECTED', 'BANNED');

-- PostgreSQL 18: uuidv7() genera UUIDs ordenados por timestamp.
-- Mejor performance en índices B-tree que UUIDv4 (gen_random_uuid()).
-- Usar uuidv7() en TODAS las tablas del sistema.
CREATE TABLE inboxes (
    id                  UUID PRIMARY KEY DEFAULT uuidv7(),
    name                VARCHAR(100) NOT NULL,
    channel_type        channel_type NOT NULL,
    phone_number        VARCHAR(20) NOT NULL,
    
    -- Config no-sensible (throttle, settings de UI, etc.)
    config              JSONB NOT NULL DEFAULT '{}',
    config_schema_ver   SMALLINT NOT NULL DEFAULT 1,
    
    -- Secretos cifrados a nivel app (AES-256-GCM, key en env var)
    -- Nunca exponer estos campos en la API REST
    encrypted_access_token      TEXT,
    encrypted_refresh_token     TEXT,
    secret_version              SMALLINT DEFAULT 1,
    
    -- Estado
    is_active           BOOLEAN DEFAULT true,
    ban_status          ban_status DEFAULT 'OK',
    banned_at           TIMESTAMPTZ,
    ban_reason          TEXT,
    
    -- Evolution session (para canales no oficiales)
    evolution_instance_name     VARCHAR(100),
    evolution_session_status    VARCHAR(30),  -- CONNECTED|DISCONNECTED|QR_PENDING
    evolution_last_heartbeat    TIMESTAMPTZ,
    
    created_at          TIMESTAMPTZ DEFAULT now(),
    updated_at          TIMESTAMPTZ DEFAULT now()
);
```

### 5.2 Contacts (Mini-CRM)

```sql
CREATE TABLE contacts (
    id              UUID PRIMARY KEY DEFAULT uuidv7(),
    phone           VARCHAR(20) NOT NULL,
    name            VARCHAR(150),
    email           VARCHAR(200),
    avatar_url      TEXT,
    custom_attrs    JSONB DEFAULT '{}',
    
    -- Para búsqueda
    search_vector   TSVECTOR GENERATED ALWAYS AS (
        to_tsvector('spanish', coalesce(name, '') || ' ' || phone || ' ' || coalesce(email, ''))
    ) STORED,
    
    created_at      TIMESTAMPTZ DEFAULT now(),
    updated_at      TIMESTAMPTZ DEFAULT now(),
    
    CONSTRAINT uq_contact_phone UNIQUE (phone)
);

-- Nota: phone UNIQUE es correcto para WhatsApp (el número ES la identidad).
-- Si a futuro se necesitan múltiples identidades por contacto,
-- crear contact_identities(contact_id, type, value) como migración.

CREATE INDEX idx_contacts_search ON contacts USING GIN (search_vector);
CREATE INDEX idx_contacts_phone  ON contacts (phone);

CREATE TABLE contact_tags (
    contact_id  UUID REFERENCES contacts(id) ON DELETE CASCADE,
    tag         VARCHAR(50) NOT NULL,
    tagged_by   UUID REFERENCES agents(id),
    created_at  TIMESTAMPTZ DEFAULT now(),
    PRIMARY KEY (contact_id, tag)
);

CREATE INDEX idx_contact_tags_tag ON contact_tags(tag);
```

### 5.3 Conversations

```sql
CREATE TYPE conversation_status AS ENUM (
    'OPEN',           -- En bandeja compartida, sin agente
    'ASSIGNED',       -- Asignada a un agente específico
    'BOT_HANDLING',   -- Siendo gestionada por IA (Fase 4)
    'PENDING_CUSTOMER', -- Esperando respuesta del cliente
    'PENDING_INTERNAL', -- Esperando acción interna
    'SNOOZED',        -- Pospuesta hasta timestamp
    'RESOLVED',       -- Cerrada
    'ARCHIVED'        -- Archivada, fuera de operación activa
);

CREATE TABLE conversations (
    id              UUID PRIMARY KEY DEFAULT uuidv7(),
    inbox_id        UUID NOT NULL REFERENCES inboxes(id),
    contact_id      UUID NOT NULL REFERENCES contacts(id),
    assigned_to     UUID REFERENCES agents(id),
    status          conversation_status DEFAULT 'OPEN',
    subject         VARCHAR(200),
    
    -- Threading: para evitar crear conversaciones duplicadas
    -- Una sola conversación activa por contact+inbox
    -- Ver sección 9.1 para la política completa
    
    -- Desnormalización para rendimiento de bandeja (actualizar en cada mensaje)
    last_message_at         TIMESTAMPTZ,
    last_message_preview    VARCHAR(200),
    last_message_direction  VARCHAR(10),  -- 'INBOUND' | 'OUTBOUND'
    last_inbound_at         TIMESTAMPTZ,
    last_outbound_at        TIMESTAMPTZ,
    unread_count            INT DEFAULT 0,
    
    -- Métricas de operación
    first_response_at       TIMESTAMPTZ,  -- Primera respuesta del agente
    resolved_at             TIMESTAMPTZ,
    snoozed_until           TIMESTAMPTZ,
    reopened_count          SMALLINT DEFAULT 0,
    
    -- Control de concurrencia (optimistic locking)
    row_version             INT NOT NULL DEFAULT 0,
    
    -- IA
    ai_handled              BOOLEAN DEFAULT false,
    ai_escalated_at         TIMESTAMPTZ,
    
    -- Auditoría
    closed_by               UUID REFERENCES agents(id),
    created_at              TIMESTAMPTZ DEFAULT now(),
    updated_at              TIMESTAMPTZ DEFAULT now()
);

-- Índice para bandeja de entrada (el más importante)
CREATE INDEX idx_conv_inbox_status_last
    ON conversations(inbox_id, status, last_message_at DESC);

-- Para agente específico
CREATE INDEX idx_conv_assigned_status
    ON conversations(assigned_to, status, last_message_at DESC)
    WHERE assigned_to IS NOT NULL;

CREATE INDEX idx_conv_contact
    ON conversations(contact_id, status);

-- Solo una conversación activa por contact+inbox
CREATE UNIQUE INDEX uq_conv_active_per_contact_inbox
    ON conversations(contact_id, inbox_id)
    WHERE status NOT IN ('RESOLVED', 'ARCHIVED');
```

### 5.4 Messages

```sql
CREATE TYPE message_direction AS ENUM ('INBOUND', 'OUTBOUND');
CREATE TYPE message_type      AS ENUM (
    'TEXT', 'IMAGE', 'AUDIO', 'VIDEO', 'DOCUMENT',
    'STICKER', 'LOCATION', 'TEMPLATE', 'INTERNAL_NOTE', 'SYSTEM'
);
CREATE TYPE delivery_status AS ENUM (
    'PENDING',           -- Creado, aún no enviado al proveedor
    'QUEUED',            -- En cola del proveedor
    'SENT',              -- Confirmado por proveedor
    'DELIVERED',         -- Entregado al dispositivo
    'READ',              -- Leído por el destinatario
    'FAILED',            -- Error definitivo
    'RETRY_PENDING'      -- Reintento programado
);

CREATE TABLE messages (
    id                  UUID PRIMARY KEY DEFAULT uuidv7(),
    conversation_id     UUID NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    
    -- ID del proveedor (Meta message ID / Evolution message ID)
    -- UNIQUE para deduplicación de webhooks
    external_id         VARCHAR(150),
    
    direction           message_direction NOT NULL,
    type                message_type NOT NULL,
    
    -- Contenido
    content             TEXT,
    
    -- Media: la URL apunta a MinIO, no al proveedor externo
    media_url           TEXT,
    media_mime_type     VARCHAR(100),
    media_size_bytes    BIGINT,
    media_filename      VARCHAR(255),
    
    -- Templates (canales oficiales)
    template_name       VARCHAR(100),
    template_params     JSONB,
    
    -- Estado de entrega (solo mensajes OUTBOUND)
    delivery_status     delivery_status,
    delivery_updated_at TIMESTAMPTZ,
    
    -- Quién envió (para OUTBOUND)
    sent_by_agent_id    UUID REFERENCES agents(id),
    sent_by_ai          BOOLEAN DEFAULT false,
    
    -- Nota interna (invisible al cliente)
    is_private_note     BOOLEAN DEFAULT false,
    
    -- Metadatos del proveedor (raw, para debugging)
    provider_metadata   JSONB DEFAULT '{}',
    
    created_at          TIMESTAMPTZ DEFAULT now()
    -- Sin updated_at: los mensajes son inmutables. Los cambios de estado
    -- se registran en message_status_events.
);

-- Índice principal para historial de conversación (keyset pagination)
CREATE INDEX idx_msg_conversation_keyset
    ON messages(conversation_id, created_at DESC, id DESC);

-- Deduplicación de webhooks
CREATE UNIQUE INDEX uq_msg_external_id
    ON messages(external_id)
    WHERE external_id IS NOT NULL;

-- Para tracking de estado outbound
CREATE INDEX idx_msg_delivery_status
    ON messages(delivery_status, created_at DESC)
    WHERE direction = 'OUTBOUND' AND delivery_status IN ('PENDING', 'QUEUED', 'RETRY_PENDING');


-- Historial de estados de entrega (trazabilidad completa)
CREATE TABLE message_status_events (
    id              UUID PRIMARY KEY DEFAULT uuidv7(),
    message_id      UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    status          delivery_status NOT NULL,
    provider_code   VARCHAR(50),    -- código de error del proveedor si aplica
    detail          TEXT,
    occurred_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_msg_status_events ON message_status_events(message_id, occurred_at DESC);
```

**Política de retención de messages:**
- Mensajes de texto: retener 24 meses en tabla caliente, archivar a tabla fría
- Media en MinIO: retener 12 meses, luego política configurable por instancia
- `provider_metadata`: limpiar a `{}` después de 30 días (datos de debugging, no de negocio)

**Paginación obligatoria con keyset:**
```sql
-- NUNCA usar OFFSET para historiales de conversación
-- SIEMPRE keyset:
SELECT * FROM messages
WHERE conversation_id = $1
  AND (created_at, id) < ($2_last_created_at, $2_last_id)
ORDER BY created_at DESC, id DESC
LIMIT 50;
```

### 5.5 Agents

```sql
CREATE TYPE agent_role AS ENUM ('ADMIN', 'SUPERVISOR', 'AGENT');

CREATE TABLE agents (
    id                      UUID PRIMARY KEY DEFAULT uuidv7(),
    name                    VARCHAR(150) NOT NULL,
    email                   VARCHAR(200) NOT NULL UNIQUE,
    password_hash           VARCHAR(256) NOT NULL,  -- bcrypt, cost 12
    password_version        SMALLINT DEFAULT 1,
    role                    agent_role DEFAULT 'AGENT',
    is_active               BOOLEAN DEFAULT true,
    disabled_reason         TEXT,
    last_login_at           TIMESTAMPTZ,
    password_updated_at     TIMESTAMPTZ DEFAULT now(),
    created_at              TIMESTAMPTZ DEFAULT now(),
    updated_at              TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE inbox_agents (
    inbox_id    UUID REFERENCES inboxes(id) ON DELETE CASCADE,
    agent_id    UUID REFERENCES agents(id) ON DELETE CASCADE,
    PRIMARY KEY (inbox_id, agent_id)
);
```

### 5.6 Audit Log

```sql
-- Registro inmutable de acciones importantes
-- No actualizar ni borrar registros de esta tabla
CREATE TABLE audit_events (
    id              UUID PRIMARY KEY DEFAULT uuidv7(),
    actor_id        UUID REFERENCES agents(id),  -- NULL si es acción del sistema
    actor_type      VARCHAR(20) NOT NULL,  -- 'AGENT' | 'SYSTEM' | 'AI'
    event_type      VARCHAR(80) NOT NULL,  -- 'CONVERSATION_ASSIGNED', 'INBOX_BANNED', etc.
    entity_type     VARCHAR(50),
    entity_id       UUID,
    payload         JSONB DEFAULT '{}',    -- antes/después del cambio
    ip_address      INET,
    occurred_at     TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX idx_audit_actor    ON audit_events(actor_id, occurred_at DESC);
CREATE INDEX idx_audit_entity   ON audit_events(entity_type, entity_id, occurred_at DESC);
CREATE INDEX idx_audit_type     ON audit_events(event_type, occurred_at DESC);

-- Eventos que SIEMPRE deben auditarse:
-- AGENT_LOGIN, AGENT_LOGIN_FAILED, CREDENTIALS_CHANGED
-- CONVERSATION_ASSIGNED, CONVERSATION_RESOLVED, CONVERSATION_REOPENED
-- INBOX_CREATED, INBOX_BANNED, INBOX_CREDENTIALS_UPDATED
-- CAMPAIGN_CREATED, CAMPAIGN_STARTED, CAMPAIGN_PAUSED, CAMPAIGN_CANCELLED
-- CONTACT_EXPORTED, BULK_IMPORT_EXECUTED
```

### 5.7 Outbox Table

```sql
-- Ver sección 7 para el patrón completo
CREATE TABLE outbox_events (
    id              UUID PRIMARY KEY DEFAULT uuidv7(),
    stream          VARCHAR(100) NOT NULL,   -- nombre del Redis Stream destino
    event_type      VARCHAR(80) NOT NULL,
    event_version   SMALLINT NOT NULL DEFAULT 1,
    payload         JSONB NOT NULL,
    correlation_id  UUID,
    causation_id    UUID,                    -- ID del evento que causó éste
    
    -- Estado de publicación
    published       BOOLEAN DEFAULT false,
    published_at    TIMESTAMPTZ,
    retry_count     SMALLINT DEFAULT 0,
    last_error      TEXT,
    
    created_at      TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX idx_outbox_unpublished
    ON outbox_events(created_at ASC)
    WHERE published = false;
```

---

## 6. Modelo de Concurrencia

Este es uno de los puntos más críticos y menos documentados en sistemas similares.

### 6.1 Pipeline de mensajes entrantes con Channel<T>

```csharp
// En NOC.Worker.Messaging
// Un pipeline por consumer del Redis Stream

public class MessageIngestPipeline
{
    // Capacidad bounded — si el writer es más rápido que el reader,
    // el writer bloquea. Esto es backpressure explícito.
    private readonly Channel<IncomingMessageEvent> _channel =
        Channel.CreateBounded<IncomingMessageEvent>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

    // N consumers paralelos por inbox
    // Default: 4 consumers (configurable por instancia)
    private readonly int _consumerCount = 4;
}
```

### 6.2 Asignación concurrente de conversaciones

El race condition más crítico: dos agentes reclaman la misma conversación simultáneamente.

```sql
-- Asignación atómica con optimistic locking
UPDATE conversations
SET
    assigned_to = @agentId,
    status      = 'ASSIGNED',
    row_version = row_version + 1,
    updated_at  = now()
WHERE
    id          = @conversationId
    AND row_version = @expectedVersion   -- falla si alguien modificó antes
    AND (
        assigned_to IS NULL              -- no asignada: cualquier agente puede reclamar
        OR @requestorRole IN ('ADMIN', 'SUPERVISOR')  -- admins pueden reasignar
    );

-- Si rows_affected = 0: concurrent modification → devolver 409 Conflict al frontend
```

### 6.3 Campaign recipients: batch claiming con leases

```sql
-- Worker toma un batch de recipients sin bloquear la tabla
-- SKIP LOCKED es crítico: permite múltiples workers sin deadlocks
UPDATE campaign_recipients
SET
    status      = 'CLAIMED',
    claimed_at  = now(),
    claimed_by  = @workerId,
    lease_expires_at = now() + INTERVAL '5 minutes'
WHERE id IN (
    SELECT id FROM campaign_recipients
    WHERE
        campaign_id = @campaignId
        AND status  = 'QUEUED'
    ORDER BY id
    LIMIT @batchSize
    FOR UPDATE SKIP LOCKED
)
RETURNING id, contact_id, phone;
```

```sql
-- Job de recovery: libera leases expirados (corre cada minuto)
UPDATE campaign_recipients
SET
    status       = 'QUEUED',
    claimed_at   = NULL,
    claimed_by   = NULL,
    lease_expires_at = NULL
WHERE
    status = 'CLAIMED'
    AND lease_expires_at < now();
```

### 6.4 Rate limiting por inbox en Redis

```csharp
// Ventana deslizante por inbox — evita superar messages_per_minute
public async Task<bool> TryAcquireSlotAsync(string inboxId, int maxPerMinute)
{
    var key = $"ratelimit:campaign:{inboxId}";
    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var windowStart = now - 60_000;

    var pipe = _redis.CreateBatch();
    var removeOld = pipe.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);
    var count     = pipe.SortedSetLengthAsync(key);
    pipe.Execute();

    await Task.WhenAll(removeOld, count);

    if (await count >= maxPerMinute) return false;

    await _redis.SortedSetAddAsync(key, Guid.NewGuid().ToString(), now);
    await _redis.KeyExpireAsync(key, TimeSpan.FromSeconds(70));
    return true;
}
```

### 6.5 Consumer Groups de Redis Streams

```
stream:messaging:incoming
  └── consumer-group: messaging-workers
        ├── consumer: worker-messaging-1
        ├── consumer: worker-messaging-2
        └── consumer: worker-messaging-3

stream:campaigns:events
  └── consumer-group: campaign-workers
        └── consumer: worker-campaigns-1

stream:dlq:messaging     ← Dead Letter Queue
stream:dlq:campaigns     ← Dead Letter Queue
```

Cada consumer procesa mensajes con ACK explícito. Si un consumer muere sin ACK, el mensaje queda en PEL (Pending Entry List) y puede ser reclamado por otro consumer tras un timeout.

---

## 7. Transactional Outbox Pattern

Este patrón garantiza que **un evento siempre se publique si y solo si la transacción de negocio fue exitosa**.

### El problema que resuelve

```
❌ Sin outbox (frágil):
1. Persisto conversación en DB ✓
2. Publico evento en Redis Stream ✗ (Redis caído)
→ La conversación existe pero el Worker.AI nunca la procesa

❌ Sin outbox (al revés, también frágil):
1. Publico evento en Redis Stream ✓
2. Persisto conversación en DB ✗ (constraint violation)
→ El worker procesa un evento que no tiene datos en DB
```

### La solución

```
✓ Con outbox:
1. BEGIN TRANSACTION
2. Persisto conversación en DB
3. Persisto evento en outbox_events (mismo commit)
4. COMMIT

→ Si falla: ninguno de los dos se guarda
→ Si ok: ambos se guardan atómicamente

5. Outbox Relay (proceso background) lee outbox_events WHERE published = false
6. Publica en Redis Stream
7. Marca published = true
```

### Implementación del Outbox Relay

```csharp
public class OutboxRelayService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await ProcessBatchAsync(ct);
            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        // Tomar batch de eventos no publicados
        var events = await _db.OutboxEvents
            .Where(e => !e.Published && e.RetryCount < 5)
            .OrderBy(e => e.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        foreach (var evt in events)
        {
            try
            {
                await _redis.StreamAddAsync(evt.Stream, BuildStreamEntry(evt));
                evt.Published   = true;
                evt.PublishedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                evt.RetryCount++;
                evt.LastError = ex.Message;
                // Exponential backoff implícito por el delay del loop
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
```

### Contrato de eventos (todos los eventos del bus)

```csharp
// Todos los eventos deben incluir estos campos
public record NocEvent
{
    public required string   EventId        { get; init; } = Guid.NewGuid().ToString();
    public required string   EventType      { get; init; }
    public required int      EventVersion   { get; init; } = 1;
    public required DateTime OccurredAt     { get; init; } = DateTime.UtcNow;
    public required string   CorrelationId  { get; init; }
    public string?           CausationId    { get; init; }
}

// Ejemplo
public record MessageReceivedEvent : NocEvent
{
    public required string   ConversationId { get; init; }
    public required string   InboxId        { get; init; }
    public required string   ContactId      { get; init; }
    public required string   MessageId      { get; init; }
    public required string   Content        { get; init; }
    public string?           MediaUrl       { get; init; }
    public required List<ConversationTurn> History { get; init; }
}
```

---

## 8. Motor de Campañas Masivas

### 8.1 State machine de recipients

```
QUEUED → CLAIMED → SENT → DELIVERED → READ
                        ↘ FAILED
                   ↗ QUEUED (recovery de leases expirados)
```

### 8.2 State machine de campaña

```
DRAFT → SCHEDULED → RUNNING → COMPLETED
                  ↕          ↘ FAILED
               PAUSED (manual o por ban detectado)
```

### 8.3 Executor con batch claiming

```
CampaignExecutor (por campaña RUNNING):

1. CLAIM batch (50 recipients) con SKIP LOCKED
2. Para cada recipient en paralelo (máx 3 concurrent por inbox):
   a. Verificar rate limit via Redis (ventana deslizante)
      → Si límite alcanzado: exponential backoff, luego retry
   b. Calcular delay aleatorio (delay_min_ms + random(delta))
   c. sleep(delay)
   d. Llamar Evolution API para enviar
   e. Si ok → marcar SENT, guardar external_id
   f. Si error → categorizar:
      INVALID_NUMBER → FAILED directo (no es señal de ban)
      TIMEOUT        → RETRY_PENDING (reencolar)
      SESSION_DEAD   → pausar campaña, verificar sesión Evolution
      RATE_LIMITED   → backoff, retry
      ERROR_500+     → contabilizar para detección de ban
3. Al terminar el batch → CLAIM siguiente
4. Si no quedan recipients QUEUED ni CLAIMED → COMPLETED
```

### 8.4 Detección de ban mejorada

```
Por cada fallo de envío de tipo SEND_ERROR (no INVALID_NUMBER, no TIMEOUT):

1. Categorizar error (código de Evolution API)
2. Si es categoría POTENTIAL_BAN:
   - ZINCRBY ban:signals:{inboxId} 1 con TTL 10min
   - Calcular ratio: fallos / intentos totales en ventana
   - Si ratio > 0.4 Y fallos absolutos > 5:
     → Sospecha de ban → pausar campaña → alerta
3. Chequear heartbeat de sesión Evolution:
   - Si último heartbeat > 5 min → sesión muerta → diferente problema
4. Si se confirma ban:
   - UPDATE inboxes SET ban_status = 'SUSPECTED'
   - Pausar TODAS las campañas activas del inbox
   - Audit event: INBOX_BAN_SUSPECTED
   - Publicar alerta en stream:alerts
```

### 8.5 Reconciliación de contadores

```sql
-- Job nocturno (o manual) para realinear contadores desnormalizados
UPDATE campaigns c
SET
    sent_count      = (SELECT COUNT(*) FROM campaign_recipients WHERE campaign_id = c.id AND status != 'QUEUED' AND status != 'CLAIMED'),
    delivered_count = (SELECT COUNT(*) FROM campaign_recipients WHERE campaign_id = c.id AND status = 'DELIVERED'),
    read_count      = (SELECT COUNT(*) FROM campaign_recipients WHERE campaign_id = c.id AND status = 'READ'),
    failed_count    = (SELECT COUNT(*) FROM campaign_recipients WHERE campaign_id = c.id AND status = 'FAILED')
WHERE c.status IN ('RUNNING', 'COMPLETED');
```

### 8.6 Tabla de campañas (actualizada)

```sql
CREATE TABLE campaigns (
    id                  UUID PRIMARY KEY DEFAULT uuidv7(),
    inbox_id            UUID NOT NULL REFERENCES inboxes(id),
    name                VARCHAR(200) NOT NULL,
    status              campaign_status DEFAULT 'DRAFT',
    message_template    TEXT NOT NULL,
    media_url           TEXT,           -- URL de MinIO si hay adjunto
    
    -- Scheduling
    scheduled_at        TIMESTAMPTZ,
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    paused_at           TIMESTAMPTZ,
    paused_reason       TEXT,           -- 'MANUAL' | 'BAN_SUSPECTED' | 'SESSION_DEAD'
    
    -- Throttle config (puede sobreescribir defaults de instancia)
    messages_per_minute INT DEFAULT 10,
    delay_min_ms        INT DEFAULT 2000,
    delay_max_ms        INT DEFAULT 8000,
    
    -- Time window (opcional: solo enviar en ciertos horarios)
    send_window_start   TIME,           -- e.g. 09:00
    send_window_end     TIME,           -- e.g. 20:00
    send_window_tz      VARCHAR(50),    -- e.g. 'America/Buenos_Aires'
    
    -- Contadores desnormalizados (reconciliar periódicamente)
    total_recipients    INT DEFAULT 0,
    sent_count          INT DEFAULT 0,
    delivered_count     INT DEFAULT 0,
    read_count          INT DEFAULT 0,
    failed_count        INT DEFAULT 0,
    
    created_by          UUID REFERENCES agents(id),
    created_at          TIMESTAMPTZ DEFAULT now(),
    updated_at          TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE campaign_recipients (
    id               UUID PRIMARY KEY DEFAULT uuidv7(),
    campaign_id      UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    contact_id       UUID NOT NULL REFERENCES contacts(id),
    phone            VARCHAR(20) NOT NULL,  -- snapshot al momento de crear
    
    status           VARCHAR(20) DEFAULT 'QUEUED',
    -- QUEUED | CLAIMED | SENT | DELIVERED | READ | FAILED | RETRY_PENDING
    
    -- Lease para claiming concurrente
    claimed_at       TIMESTAMPTZ,
    claimed_by       VARCHAR(100),       -- ID del worker
    lease_expires_at TIMESTAMPTZ,
    
    -- Tracking
    external_id      VARCHAR(150),       -- ID del mensaje en Evolution
    sent_at          TIMESTAMPTZ,
    delivered_at     TIMESTAMPTZ,
    read_at          TIMESTAMPTZ,
    failed_at        TIMESTAMPTZ,
    failure_reason   TEXT,
    retry_count      SMALLINT DEFAULT 0,
    
    UNIQUE (campaign_id, contact_id)
);

CREATE INDEX idx_camp_recv_queued
    ON campaign_recipients(campaign_id, id)
    WHERE status = 'QUEUED';

CREATE INDEX idx_camp_recv_claimed_expiry
    ON campaign_recipients(lease_expires_at)
    WHERE status = 'CLAIMED';

CREATE INDEX idx_camp_recv_ext_id
    ON campaign_recipients(external_id)
    WHERE external_id IS NOT NULL;
```

---

## 9. Flujos de Negocio Críticos

### 9.1 Threading Policy — cuándo reabrir vs crear nueva conversación

```
Mensaje inbound de contacto X en inbox Y:

1. Buscar conversación activa (NOT IN RESOLVED, ARCHIVED) para contact+inbox
   → Existe: agregar mensaje a esa conversación
              Si estaba RESOLVED → no debería existir (ver UNIQUE INDEX)

2. No existe conversación activa:
   a. Buscar la última conversación RESOLVED para ese contact+inbox
   b. ¿Fue resuelta hace menos de REOPEN_WINDOW_HOURS? (default: 24h)
      SÍ → Reabrir esa conversación (status = OPEN, reopened_count++)
            Insertar system message: "Conversación reabierta"
      NO  → Crear nueva conversación
   c. Si no hay conversación previa → Crear nueva conversación

REOPEN_WINDOW_HOURS configurable por inbox (env var, default 24).
```

### 9.2 Flujo completo de mensaje entrante

```
1. Meta API → POST /webhooks/meta/{inboxId}

2. NOC.Web handler (< 100ms, sin lógica de dominio):
   a. Validar HMAC-SHA256 (X-Hub-Signature-256)
   b. Deduplicar: Redis SET NX "dedup:incoming:{externalId}" EX 86400
      → Si ya existe: 200 OK sin procesar
   c. BEGIN TRANSACTION
      - Insertar en outbox_events (stream: "messaging:incoming", payload mínimo)
      COMMIT
   d. 200 OK a Meta (crítico: Meta reintentará si recibe otro código o timeout)

3. Outbox Relay (en background):
   - Publica evento en stream:messaging:incoming

4. NOC.Worker.Messaging consume el evento:
   a. Buscar/crear Contact por phone (upsert)
   b. Aplicar threading policy (9.1)
   c. BEGIN TRANSACTION
      - Insertar Message
      - Actualizar campos desnormalizados de Conversation (last_message_at, preview, etc.)
      - Insertar en outbox_events (stream: "ai:requests")
      COMMIT
   d. SignalR: push nuevo mensaje a agentes del inbox

5. Outbox Relay publica en stream:ai:requests

6. NOC.Worker.AI (stub):
   - Consume evento
   - Publica en stream:ai:responses: { action: "pass_to_agent" }

7. NOC.Worker.Messaging consume stream:ai:responses:
   - Si "pass_to_agent": notifica bandeja compartida via SignalR
   - Si "auto_respond" (Fase 4): envía respuesta via Meta API
```

### 9.3 Asignación de conversación

```
POST /api/conversations/{id}/assign { agentId }

1. Verificar que el agente solicitante tiene acceso al inbox
2. UPDATE conversations (con optimistic locking, ver sección 6.2)
   → 409 si modified concurrently → frontend muestra "alguien tomó este chat primero"
3. Insertar audit_event: CONVERSATION_ASSIGNED
4. SignalR: notificar al nuevo agente, al agente anterior (si había), a supervisores
```

---

## 10. Evolution API — Session Lifecycle

Este es el mayor riesgo operativo del canal no oficial. Evolution mantiene una sesión WhatsApp Web por número.

### Estados de sesión

```
QR_PENDING → CONNECTED → DISCONNECTED → QR_PENDING (ciclo)
                       ↘ BANNED (permanente)
```

### Heartbeat monitor

```csharp
// En NOC.Worker.Messaging (o worker dedicado a sesiones)
// Corre cada 60 segundos por inbox no oficial activo

public class EvolutionSessionMonitor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var unofficialInboxes = await GetActiveUnofficialInboxesAsync();

            foreach (var inbox in unofficialInboxes)
            {
                var status = await _evolutionClient.GetInstanceStatusAsync(inbox.EvolutionInstanceName);

                await UpdateSessionStatusAsync(inbox.Id, status);

                if (status == "DISCONNECTED" && inbox.EvolutionSessionStatus == "CONNECTED")
                {
                    // Transición a caído: alertar
                    await PublishAlertAsync(AlertType.SessionDisconnected, inbox.Id);
                    await PauseActiveCampaignsAsync(inbox.Id, "SESSION_DEAD");
                }

                if (status == "CONNECTED" && inbox.EvolutionSessionStatus == "DISCONNECTED")
                {
                    // Reconexión: notificar y permitir reanudar campañas
                    await PublishAlertAsync(AlertType.SessionReconnected, inbox.Id);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
    }
}
```

### Webhook de estado de Evolution

```
POST /webhooks/evolution/{inboxId}/status
Body: { "instance": "...", "status": "open"|"close"|"connecting" }

→ Actualizar inboxes.evolution_session_status
→ Actualizar inboxes.evolution_last_heartbeat
→ Si "close" → misma lógica de pausa que el monitor
```

---

## 11. Media Storage (MinIO)

### Por qué MinIO

Self-hosted, S3-compatible, un contenedor adicional en el compose. Los mensajes con media de los proveedores (Meta/Evolution) apuntan a URLs efímeras que expiran. NOC descarga y re-almacena en MinIO para garantizar acceso permanente.

### Estructura de buckets

```
noc-media/
  messages/{year}/{month}/{messageId}/{filename}   ← media de conversaciones
  campaigns/{campaignId}/{filename}                 ← media de campañas
  avatars/{contactId}/avatar.{ext}                  ← avatares de contactos
  exports/{agentId}/{timestamp}/{filename}           ← exportaciones temporales (TTL 24h)
```

### Flujo de ingesta de media

```
1. Webhook trae URL externa de media (Meta URL o Evolution URL)
2. Handler webhook guarda solo la URL externa en outbox payload
3. Worker.Messaging procesa:
   a. Descarga media de URL externa
   b. Sube a MinIO
   c. Guarda URL de MinIO en messages.media_url
   d. La URL externa queda en provider_metadata (para debugging, no para acceso)
```

### URLs firmadas para el frontend

```csharp
// El frontend nunca accede a MinIO directamente
// NOC.Web genera URLs firmadas con TTL corto

GET /api/messages/{messageId}/media
→ Generar presigned URL (TTL: 1 hora)
→ Redirect 302 a la URL firmada
```

### Contenedor MinIO en compose

```yaml
minio:
  image: minio/minio:latest
  command: server /data --console-address ":9001"
  environment:
    MINIO_ROOT_USER: ${MINIO_ROOT_USER}
    MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
  volumes:
    - minio_data:/data
  ports:
    - "9000:9000"  # API
    - "9001:9001"  # Console (solo en dev, cerrar en producción)
```

---

## 12. Preparación para IA

### Contrato del evento de request (stream:ai:requests)

```json
{
  "eventId": "uuid",
  "eventType": "MESSAGE_RECEIVED",
  "eventVersion": 1,
  "occurredAt": "2026-03-18T22:00:00Z",
  "correlationId": "uuid",
  "conversationId": "uuid",
  "inboxId": "uuid",
  "contactId": "uuid",
  "messageId": "uuid",
  "content": "Hola, quiero información sobre el producto",
  "mediaUrl": null,
  "history": [
    { "role": "user", "content": "...", "timestamp": "..." },
    { "role": "assistant", "content": "...", "timestamp": "..." }
  ]
}
```

### Contrato del evento de response (stream:ai:responses)

```json
{
  "eventId": "uuid",
  "eventType": "AI_DECISION",
  "eventVersion": 1,
  "correlationId": "uuid",
  "conversationId": "uuid",
  "action": "pass_to_agent",
  "responseText": null,
  "agentContext": null,
  "confidence": null,
  "model": "stub-v1",
  "tokensUsed": 0
}
```

Los valores posibles de `action`:
- `auto_respond` — el AI responde, no se notifica al agente
- `pass_to_agent` — pasa a bandeja, sin contexto adicional
- `pass_to_agent_with_context` — pasa a bandeja con resumen para el agente

---

## 13. Observabilidad

### Stack mínimo requerido

```yaml
# Agregar al docker-compose
prometheus:
  image: prom/prometheus:latest
  volumes:
    - ./config/prometheus.yml:/etc/prometheus/prometheus.yml

grafana:
  image: grafana/grafana:latest
  environment:
    - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_PASSWORD}
```

### Logs estructurados (Serilog)

```csharp
// Todos los logs deben incluir estos campos de correlación
Log.Information(
    "Message processed {EventType} {ConversationId} {InboxId} {DurationMs}",
    eventType, conversationId, inboxId, sw.ElapsedMilliseconds
);

// Formato JSON para producción, texto para dev
// Enrich con: MachineName, ThreadId, CorrelationId del request
```

### Métricas críticas a exponer (endpoint /metrics para Prometheus)

```
# Ingesta
noc_webhooks_received_total{inbox_id, channel_type}
noc_webhooks_duplicates_total{inbox_id}
noc_webhook_processing_ms{quantile}

# Mensajería
noc_messages_processed_total{direction, type, inbox_id}
noc_messages_failed_total{inbox_id, reason}
noc_conversation_assignment_conflicts_total

# Redis Streams
noc_stream_lag{stream_name, consumer_group}   ← CRÍTICO: alertar si > 1000
noc_outbox_unpublished_count                  ← alertar si > 500
noc_dlq_messages_total{stream}                ← alertar si > 0

# Campañas
noc_campaign_throughput_msgs_per_minute{campaign_id}
noc_campaign_fail_ratio{inbox_id}             ← alertar si > 0.3
noc_ban_suspicion_score{inbox_id}

# Proveedores
noc_provider_send_latency_ms{provider, inbox_id, quantile}
noc_provider_errors_total{provider, error_code}
noc_evolution_session_status{inbox_id}        ← 1=connected, 0=disconnected

# Sistema
noc_db_pool_available
noc_redis_connected
noc_minio_reachable
```

### Alertas obligatorias

| Condición | Severidad | Acción |
|-----------|-----------|--------|
| `stream_lag > 1000` | Warning | Revisar workers |
| `stream_lag > 5000` | Critical | Escalar, posible caída de worker |
| `outbox_unpublished > 500` | Warning | Redis con problemas |
| `dlq_messages > 0` | Warning | Revisar mensajes fallidos |
| `campaign_fail_ratio > 0.3` | Warning | Posible ban |
| `evolution_session_status = 0 > 5min` | Critical | Sesión caída |
| `ban_suspicion_score alta` | Critical | Pausar campaña, alertar admin |
| `redis_connected = false` | Critical | Sistema degradado |
| `db_pool_available < 5` | Critical | Pool exhausto |

---

## 14. Seguridad

### Obligatorio en Fase 1

**Autenticación:**
- Access token JWT: TTL 15 minutos
- Refresh token: TTL 7 días, rotable (invalida el anterior al usar)
- Revocación de tokens: tabla `revoked_tokens` o Redis SET con TTL
- Contraseñas: bcrypt cost 12, no MD5/SHA

**Secretos de proveedores:**
- `encrypted_access_token` e `encrypted_refresh_token` en inboxes: AES-256-GCM
- Master key en variable de entorno (nunca en DB ni código)
- Rotación de key: campo `secret_version` permite re-encrypt incremental

**Webhooks:**
- Validar firma HMAC en cada webhook (rechazar si inválida, loggear intento)
- Timestamp tolerance: rechazar webhooks con timestamp > 5 minutos de diferencia
- Rate limiting por IP en endpoints de webhook

**API:**
- Rate limiting en todos los endpoints REST (configurable por rol)
- Payload size cap: 10MB máximo en webhooks, 1MB en API REST
- Sanitizar logs: nunca loggear payload completo de webhook (puede tener PII)

### Fase 2

- MFA para roles Admin y Supervisor
- IP allowlist configurable para acceso al panel
- Rotación de credenciales de proveedores sin downtime
- Endpoint de audit log solo accesible por Admin

---

## 15. Infraestructura y Deploy

### docker-compose.yml

```yaml
services:

  noc-web:
    build:
      context: ./src
      dockerfile: NOC.Web/Dockerfile
    ports: ["8080:8080"]
    environment:
      - ConnectionStrings__Postgres=Host=postgres;Database=noc;...
      - ConnectionStrings__Redis=redis:6379
      - ConnectionStrings__MinIO=http://minio:9000
      - Jwt__Secret=${JWT_SECRET}
      - Jwt__AccessTokenMinutes=15
      - Jwt__RefreshTokenDays=7
      - Encryption__MasterKey=${ENCRYPTION_MASTER_KEY}
    depends_on: [postgres, redis, minio]
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s

  noc-worker-messaging:
    build:
      context: ./src
      dockerfile: NOC.Worker.Messaging/Dockerfile
    environment:
      - ConnectionStrings__Postgres=Host=postgres;...
      - ConnectionStrings__Redis=redis:6379
      - ConnectionStrings__MinIO=http://minio:9000
      - Workers__Messaging__ConsumerCount=4
      - Workers__Messaging__ReopenWindowHours=24
    depends_on: [postgres, redis, minio]

  noc-worker-campaigns:
    build:
      context: ./src
      dockerfile: NOC.Worker.Campaigns/Dockerfile
    environment:
      - ConnectionStrings__Postgres=Host=postgres;...
      - ConnectionStrings__Redis=redis:6379
      - Workers__Campaigns__BatchSize=50
      - Workers__Campaigns__LeaseMinutes=5
      - Workers__Campaigns__DefaultMsgsPerMinute=10
      - Workers__Campaigns__BanThresholdRatio=0.4
      - Workers__Campaigns__BanMinAbsoluteFailures=5
    depends_on: [postgres, redis]

  noc-worker-notifications:
    build:
      context: ./src
      dockerfile: NOC.Worker.Notifications/Dockerfile
    environment:
      - ConnectionStrings__Postgres=Host=postgres;...
      - ConnectionStrings__Redis=redis:6379
      - Alerts__SlackWebhookUrl=${SLACK_WEBHOOK_URL}
      - Alerts__AdminEmail=${ADMIN_EMAIL}

  noc-worker-ai:
    build:
      context: ./src
      dockerfile: NOC.Worker.AI/Dockerfile
    environment:
      - ConnectionStrings__Redis=redis:6379
      # Fase 4: agregar LLM keys aquí

  postgres:
    image: postgres:18-alpine
    environment:
      - POSTGRES_DB=noc
      - POSTGRES_USER=${DB_USER}
      - POSTGRES_PASSWORD=${DB_PASS}
    volumes: [postgres_data:/var/lib/postgresql/data]
    command: >
      postgres
      -c max_connections=100
      -c shared_buffers=256MB
      -c effective_cache_size=1GB

  redis:
    image: redis:8-alpine
    # AOF obligatorio: garantiza durabilidad de Streams y outbox coordination
    command: redis-server --appendonly yes --appendfsync everysec
    volumes: [redis_data:/data]

  minio:
    image: minio/minio:latest
    command: server /data
    environment:
      - MINIO_ROOT_USER=${MINIO_ROOT_USER}
      - MINIO_ROOT_PASSWORD=${MINIO_ROOT_PASSWORD}
    volumes: [minio_data:/data]
    ports: ["9000:9000"]

volumes:
  postgres_data:
  redis_data:
  minio_data:
```

### Estructura del repositorio

```
neuryn-omnichannel/
├── src/
│   ├── NOC.Shared/
│   │   ├── Domain/          ← Entidades EF Core, enums, value objects
│   │   ├── Events/          ← Contratos de eventos (records)
│   │   └── Infrastructure/  ← Redis helpers, MinIO client, Outbox helpers
│   ├── NOC.Web/             ← ASP.NET Core 10, SignalR, REST API, Webhooks
│   ├── NOC.Worker.Messaging/
│   ├── NOC.Worker.Campaigns/
│   ├── NOC.Worker.Notifications/
│   └── NOC.Worker.AI/
├── migrations/              ← EF Core migrations versionadas
├── config/
│   ├── prometheus.yml
│   └── grafana/
├── docker-compose.yml
├── docker-compose.dev.yml
├── .env.example
└── docs/
    └── architecture-v2.md
```

---

## 16. Estrategia de Implementación por Fases

### Fase 1 — Core Conversacional (MVP)
*Meta: sistema funcional con canal oficial, agentes y tiempo real*

- [ ] Schema completo con migrations EF Core 10
- [ ] Outbox pattern implementado desde el primer commit
- [ ] NOC.Web: auth JWT, REST CRUD, SignalR hub
- [ ] Webhook Meta: validación HMAC, deduplicación, escritura outbox
- [ ] NOC.Worker.Messaging: conversaciones, threading policy, asignación con optimistic locking
- [ ] NOC.Worker.AI stub
- [ ] NOC.Worker.Notifications: push SignalR
- [ ] MinIO: ingesta de media de mensajes entrantes
- [ ] Audit log para acciones críticas
- [ ] Secretos de inboxes cifrados
- [ ] Logs estructurados con correlation IDs
- [ ] Docker Compose completo + deploy en Dokploy

### Fase 2 — Campañas Masivas
*Meta: motor de campañas confiable con controles anti-ban*

- [ ] NOC.Worker.Campaigns: scheduler, batch claiming, leases, recovery
- [ ] Webhook Evolution: tracking de estados, ingesta de media
- [ ] Rate limiting por inbox con Redis (ventana deslizante)
- [ ] Detección de ban con categorización de errores
- [ ] Evolution session monitor (heartbeat)
- [ ] Reconciliación periódica de contadores
- [ ] UI: crear campañas, monitorear en tiempo real, pausar/reanudar
- [ ] Alertas externas (Slack/email) para ban y sesión caída
- [ ] Métricas Prometheus + dashboards Grafana básicos

### Fase 3 — CRM y Productividad
*Meta: herramientas de agente avanzadas*

- [ ] Tags de contactos (UI + filtros en bandeja)
- [ ] Notas internas en conversaciones
- [ ] Búsqueda full-text (PostgreSQL FTS)
- [ ] Filtros avanzados de bandeja (por agente, inbox, tag, estado, fecha)
- [ ] Importación masiva de contactos (CSV)
- [ ] Exportación de reportes
- [ ] MFA para Admin/Supervisor
- [ ] Time windows para campañas
- [ ] Política de retención y archivado de mensajes viejos

### Fase 4 — Integración IA
*Meta: LLM con RAG en canales oficiales*

- [ ] Reemplazar NOC.Worker.AI stub con implementación real
- [ ] Pipeline RAG con base de conocimiento configurable
- [ ] UI para configurar bot por inbox
- [ ] Métricas de IA: tasa de resolución automática, escalaciones, costo de tokens

---

## 17. Decisiones Técnicas (ADRs)

### ADR-001: Modular Monolith + Workers (no microservicios)
**Decisión:** Un único dominio compartido, múltiples procesos desplegables separados.  
**Razón:** Los servicios comparten DB, equipo y dominio. Separarlos en microservicios reales agregaría complejidad de transacciones distribuidas, service mesh y contratos de API inter-servicio sin beneficio real en este scope. Los workers separados dan el beneficio de escalado independiente y aislamiento de fallos sin el costo cognitivo de microservicios plenos.

### ADR-002: Transactional Outbox (no publicación directa al bus)
**Decisión:** Todo evento pasa por la tabla `outbox_events` antes de llegar a Redis Streams.  
**Razón:** Es la única forma de garantizar consistencia entre la mutación de negocio y la publicación del evento sin coordinación distribuida. Sin outbox, una caída entre el commit y la publicación produce eventos perdidos silenciosamente.

### ADR-003: Redis 8 como event bus + caché + coordinación
**Decisión:** Redis asume múltiples roles: Streams (bus), caché, rate limiting, locks, coordinación de leases.  
**Consecuencia explícita:** Redis es una dependencia central. Si cae, el sistema se degrada: los webhooks pueden seguir siendo recibidos (la web escribe al outbox en DB), pero el procesamiento async se detiene. Mitigación: Redis con `appendonly yes`, monitoreo activo, alertas de `redis_connected`.  
**Por qué no Kafka/RabbitMQ:** Aumentan la complejidad operacional por instancia sin beneficio proporcional en este volumen.

### ADR-004: PostgreSQL 18 como única base de datos
**Decisión:** Una sola instancia PostgreSQL por deployment, compartida por todos los workers.  
**Razón:** Single-tenant elimina la necesidad de aislamiento por servicio. Transacciones ACID son fundamentales para el outbox pattern. `SKIP LOCKED` para campaign claiming es nativo.

### ADR-005: MinIO para media storage
**Decisión:** Toda la media (imágenes, audios, documentos) se almacena en MinIO, no en el sistema de archivos del contenedor ni se referencia directamente desde URLs del proveedor.  
**Razón:** Las URLs de Meta y Evolution expiran. El acceso a media debe ser controlado y duradero. MinIO es S3-compatible, self-hosted y agrega un único contenedor al compose.

### ADR-006: Keyset pagination obligatoria en messages
**Decisión:** Prohibido usar OFFSET en queries sobre la tabla `messages`. Solo keyset pagination por `(created_at DESC, id DESC)`.  
**Razón:** La tabla `messages` crece de forma ilimitada. OFFSET degrada linealmente: la página 1000 hace un full scan de 50.000 filas. Keyset es O(log n) independientemente de la profundidad.

### ADR-007: Optimistic locking en asignación de conversaciones
**Decisión:** Usar `row_version` con UPDATE condicional para asignación. Devolver 409 ante conflicto.  
**Razón:** SELECT FOR UPDATE bloquea la fila durante la transacción (posible deadlock en alta concurrencia). Optimistic locking permite concurrencia máxima y maneja el conflicto en el cliente, que es el comportamiento correcto en una UI de bandeja compartida.

---

*Neuryn Software — Documento de Arquitectura NOC v2.0*  
*Actualizar este documento ante cualquier decisión técnica significativa.*

---

## 18. Módulo de Proxies Rotativos (Evolution API)

### Contexto

Las líneas no oficiales (Evolution API / WhatsApp Web) son vulnerables a detección y ban por IP. Para mitigar esto, cada inbox no oficial puede tener asignada una **salida técnica** (proxy de egreso) a través de la cual NOC realiza todas las llamadas a ese inbox de Evolution. El administrador gestiona un pool de proxies y los asigna manualmente a los inboxes.

### Modelo de datos

```sql
CREATE TYPE proxy_protocol AS ENUM ('HTTP', 'HTTPS', 'SOCKS5');
CREATE TYPE proxy_status   AS ENUM ('ACTIVE', 'ASSIGNED', 'FAILING', 'DISABLED');

CREATE TABLE proxy_outbounds (
    id                  UUID PRIMARY KEY DEFAULT uuidv7(),
    alias               VARCHAR(100) NOT NULL,          -- "Proxy Córdoba 01"
    host                VARCHAR(255) NOT NULL,           -- "gw.dataimpulse.com"
    port                INT NOT NULL,                    -- 823
    protocol            proxy_protocol NOT NULL DEFAULT 'HTTP',
    username            VARCHAR(200),
    encrypted_password  TEXT,                            -- AES-256-GCM, misma key que secretos de inboxes
    
    status              proxy_status DEFAULT 'ACTIVE',
    last_tested_at      TIMESTAMPTZ,
    last_test_ok        BOOLEAN,
    last_test_latency_ms INT,
    last_error          TEXT,
    
    created_by          UUID REFERENCES agents(id),
    created_at          TIMESTAMPTZ DEFAULT now(),
    updated_at          TIMESTAMPTZ DEFAULT now()
);

-- Relación inbox ↔ proxy (un inbox puede tener un proxy, un proxy puede asignarse a varios inboxes)
-- La asignación se guarda en el campo existente de inboxes:
ALTER TABLE inboxes ADD COLUMN proxy_outbound_id UUID REFERENCES proxy_outbounds(id) ON DELETE SET NULL;

CREATE INDEX idx_proxy_inbox ON inboxes(proxy_outbound_id) WHERE proxy_outbound_id IS NOT NULL;
```

### API endpoints

```
GET    /api/proxies                    ← Lista todos los proxies (Admin/Supervisor)
POST   /api/proxies                    ← Crear proxy (alias, host, port, protocol, user, pass)
DELETE /api/proxies/{id}               ← Eliminar proxy (verifica que no esté asignado)
POST   /api/proxies/{id}/test          ← Probar conectividad del proxy
POST   /api/proxies/{id}/assign/{inboxId}  ← Asignar proxy a un inbox
DELETE /api/proxies/{id}/assign/{inboxId}  ← Desasignar proxy de un inbox
```

### Lógica del test de proxy

```csharp
// POST /api/proxies/{id}/test
public async Task<ProxyTestResult> TestProxyAsync(Guid proxyId)
{
    var proxy = await _db.ProxyOutbounds.FindAsync(proxyId);
    var handler = new HttpClientHandler
    {
        Proxy = BuildWebProxy(proxy),
        UseProxy = true
    };

    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    var sw = Stopwatch.StartNew();

    try
    {
        // Test contra endpoint neutral — no contra Evolution directamente
        var response = await client.GetAsync("https://httpbin.org/ip");
        sw.Stop();

        proxy.LastTestOk = response.IsSuccessStatusCode;
        proxy.LastTestLatencyMs = (int)sw.ElapsedMilliseconds;
        proxy.LastTestedAt = DateTime.UtcNow;
        proxy.LastError = null;

        await _db.SaveChangesAsync();
        return new ProxyTestResult { Ok = true, LatencyMs = proxy.LastTestLatencyMs };
    }
    catch (Exception ex)
    {
        proxy.LastTestOk = false;
        proxy.LastError = ex.Message;
        proxy.LastTestedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new ProxyTestResult { Ok = false, Error = ex.Message };
    }
}
```

### Uso del proxy en llamadas a Evolution API

```csharp
// En NOC.Shared/Infrastructure/EvolutionApiClient.cs
public class EvolutionApiClient
{
    public static HttpClient BuildForInbox(Inbox inbox, ProxyOutbound? proxy)
    {
        var handler = new HttpClientHandler();

        if (proxy != null)
        {
            handler.Proxy = new WebProxy($"{proxy.Protocol.ToLower()}://{proxy.Host}:{proxy.Port}")
            {
                Credentials = proxy.Username != null
                    ? new NetworkCredential(proxy.Username, DecryptPassword(proxy.EncryptedPassword))
                    : null
            };
            handler.UseProxy = true;
        }

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(inbox.Config["evolutionApiUrl"].ToString()),
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Add("apikey", DecryptToken(inbox.EncryptedAccessToken));

        return client;
    }
}
```

### UI — "Salidas técnicas" (panel de administración)

El panel tiene dos secciones (tal como la captura de referencia):

**Panel izquierdo — Formulario de alta:**
- Alias (texto libre)
- Host (dominio o IP)
- Puerto (número)
- Protocolo (dropdown: HTTP / HTTPS / SOCKS5)
- Usuario (opcional)
- Contraseña (opcional, se cifra antes de persistir)
- Botón "Guardar salida técnica"

**Panel derecho — Lista de proxies cargados:**
- Por cada proxy: alias, host:puerto, badge de estado (Activo / Asignado / Fallando)
- Botón "Probar" → llama al endpoint de test, muestra latencia o error
- Botón "Eliminar" → solo disponible si el proxy no está asignado a ningún inbox
- La asignación a un inbox se realiza desde el panel de configuración del inbox

### Observabilidad de proxies

```
# Métricas adicionales
noc_proxy_test_latency_ms{proxy_id}
noc_proxy_test_failures_total{proxy_id}
noc_proxy_evolution_call_errors_total{proxy_id, inbox_id}
```

**Alerta:** si un proxy acumula > 10 errores en llamadas a Evolution en 5 minutos → alerta al admin para revisar o rotar manualmente.

