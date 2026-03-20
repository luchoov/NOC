// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api, qs } from './client';
import type {
  InboxResponse,
  CreateInboxRequest,
  CreateInboxResponse,
  UpdateInboxRequest,
  ProvisionEvolutionRequest,
  EvolutionOperationResponse,
} from '@/types/api';

export function listInboxes(params?: { channelType?: string; isActive?: boolean; limit?: number }) {
  return api.get<InboxResponse[]>(`/api/inboxes${qs(params || {})}`);
}

export function getInbox(id: string) {
  return api.get<InboxResponse>(`/api/inboxes/${id}`);
}

export function createInbox(data: CreateInboxRequest) {
  return api.post<CreateInboxResponse>('/api/inboxes', data);
}

export function updateInbox(id: string, data: UpdateInboxRequest) {
  return api.put<InboxResponse>(`/api/inboxes/${id}`, data);
}

export function deleteInbox(id: string) {
  return api.del<void>(`/api/inboxes/${id}`);
}

export function provisionEvolution(id: string, data?: ProvisionEvolutionRequest) {
  return api.post<EvolutionOperationResponse>(`/api/inboxes/${id}/provision-evolution`, data);
}

export function connectEvolution(id: string) {
  return api.post<EvolutionOperationResponse>(`/api/inboxes/${id}/connect`);
}

export function getEvolutionStatus(id: string, doRefresh?: boolean) {
  return api.get<EvolutionOperationResponse>(`/api/inboxes/${id}/status${doRefresh ? '?refresh=true' : ''}`);
}
