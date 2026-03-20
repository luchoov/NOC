// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api, qs } from './client';
import type { MessageResponse, SendMessageRequest, ListMessagesParams } from '@/types/api';

export function listMessages(conversationId: string, params: ListMessagesParams) {
  return api.get<MessageResponse[]>(
    `/api/conversations/${conversationId}/messages${qs(params)}`,
  );
}

export function sendMessage(conversationId: string, data: SendMessageRequest) {
  return api.post<MessageResponse>(
    `/api/conversations/${conversationId}/messages`,
    data,
  );
}

export function getMediaUrl(messageId: string): string {
  return `/api/messages/${messageId}/media`;
}
