// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import type { ConversationStatus, BanStatus, ProxyStatus, DeliveryStatus } from '@/types/api';

export const CONVERSATION_STATUS: Record<ConversationStatus, { label: string; class: string }> = {
  OPEN:             { label: 'Abierta',         class: 'bg-sky-500/15 text-sky-400' },
  ASSIGNED:         { label: 'Asignada',        class: 'bg-blue-500/15 text-blue-400' },
  BOT_HANDLING:     { label: 'Bot',             class: 'bg-indigo-500/15 text-indigo-400' },
  PENDING_CUSTOMER: { label: 'Espera cliente',  class: 'bg-cyan-500/15 text-cyan-400' },
  PENDING_INTERNAL: { label: 'Espera interna',  class: 'bg-teal-500/15 text-teal-400' },
  SNOOZED:          { label: 'Pospuesta',       class: 'bg-zinc-500/15 text-zinc-400' },
  RESOLVED:         { label: 'Resuelta',        class: 'bg-emerald-500/15 text-emerald-400' },
  ARCHIVED:         { label: 'Archivada',       class: 'bg-zinc-600/15 text-zinc-500' },
};

export const BAN_STATUS: Record<BanStatus, { label: string; class: string }> = {
  OK:        { label: 'OK',            class: 'bg-emerald-500/15 text-emerald-400' },
  SUSPECTED: { label: 'Sospecha ban',  class: 'bg-red-500/15 text-red-400' },
  BANNED:    { label: 'Baneado',       class: 'bg-red-600/20 text-red-300' },
};

export const PROXY_STATUS: Record<ProxyStatus, { label: string; class: string }> = {
  ACTIVE:   { label: 'Activo',        class: 'bg-emerald-500/15 text-emerald-400' },
  ASSIGNED: { label: 'Asignado',      class: 'bg-blue-500/15 text-blue-400' },
  FAILING:  { label: 'Fallando',      class: 'bg-red-500/15 text-red-400' },
  DISABLED: { label: 'Deshabilitado', class: 'bg-zinc-500/15 text-zinc-500' },
};

export const DELIVERY: Record<DeliveryStatus, { label: string; icon: string }> = {
  PENDING:       { label: 'Pendiente',     icon: '⏳' },
  QUEUED:        { label: 'En cola',       icon: '↑' },
  SENT:          { label: 'Enviado',       icon: '✓' },
  DELIVERED:     { label: 'Entregado',     icon: '✓✓' },
  READ:          { label: 'Leído',         icon: '✓✓' },
  FAILED:        { label: 'Fallido',       icon: '✗' },
  RETRY_PENDING: { label: 'Reintentando', icon: '↻' },
};
