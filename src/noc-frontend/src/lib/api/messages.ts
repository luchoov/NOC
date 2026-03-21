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

export function sendMediaMessage(
  conversationId: string,
  file: File,
  caption?: string,
) {
  const extra: Record<string, string> = {};
  if (caption) extra.caption = caption;
  return api.upload<MessageResponse>(
    `/api/conversations/${conversationId}/messages/media`,
    file,
    'file',
    extra,
  );
}

export function getMediaUrl(conversationId: string, messageId: string): string {
  return `/api/conversations/${conversationId}/messages/${messageId}/media`;
}
