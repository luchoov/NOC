// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api } from './client';
import type { ProxyResponse, CreateProxyRequest, ProxyTestResult } from '@/types/api';

export function listProxies() {
  return api.get<ProxyResponse[]>('/api/proxies');
}

export function getProxy(id: string) {
  return api.get<ProxyResponse>(`/api/proxies/${id}`);
}

export function createProxy(data: CreateProxyRequest) {
  return api.post<ProxyResponse>('/api/proxies', data);
}

export function deleteProxy(id: string) {
  return api.del<void>(`/api/proxies/${id}`);
}

export function testProxy(id: string) {
  return api.post<ProxyTestResult>(`/api/proxies/${id}/test`);
}

export function assignToInbox(proxyId: string, inboxId: string) {
  return api.post<{ message: string }>(`/api/proxies/${proxyId}/assign/${inboxId}`);
}

export function unassignFromInbox(proxyId: string, inboxId: string) {
  return api.del<{ message: string }>(`/api/proxies/${proxyId}/assign/${inboxId}`);
}
