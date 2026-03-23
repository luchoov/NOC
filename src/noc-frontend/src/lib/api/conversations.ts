// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api, qs } from './client';
import type {
  ConversationResponse,
  CreateConversationRequest,
  ListConversationsParams,
  AssignConversationRequest,
  UpdateConversationStatusRequest,
} from '@/types/api';

export function createConversation(data: CreateConversationRequest) {
  return api.post<ConversationResponse>('/api/conversations', data);
}

export function listConversations(params: ListConversationsParams) {
  return api.get<ConversationResponse[]>(`/api/conversations${qs(params)}`);
}

export function getConversation(id: string) {
  return api.get<ConversationResponse>(`/api/conversations/${id}`);
}

export function assignConversation(id: string, data: AssignConversationRequest) {
  return api.post<ConversationResponse>(`/api/conversations/${id}/assign`, data);
}

export function updateConversationStatus(id: string, data: UpdateConversationStatusRequest) {
  return api.post<ConversationResponse>(`/api/conversations/${id}/status`, data);
}

export function markConversationRead(id: string) {
  return api.post<void>(`/api/conversations/${id}/read`, {});
}

export function deleteConversation(id: string) {
  return api.del<void>(`/api/conversations/${id}`);
}
