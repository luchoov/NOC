// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api, qs } from './client';
import type {
  CampaignResponse,
  CreateCampaignRequest,
  UpdateCampaignRequest,
  CampaignRecipientResponse,
} from '@/types/api';

export function listCampaigns() {
  return api.get<CampaignResponse[]>('/api/campaigns');
}

export function getCampaign(id: string) {
  return api.get<CampaignResponse>(`/api/campaigns/${id}`);
}

export function createCampaign(data: CreateCampaignRequest) {
  return api.post<CampaignResponse>('/api/campaigns', data);
}

export function updateCampaign(id: string, data: UpdateCampaignRequest) {
  return api.put<CampaignResponse>(`/api/campaigns/${id}`, data);
}

export function deleteCampaign(id: string) {
  return api.del<void>(`/api/campaigns/${id}`);
}

export function startCampaign(id: string) {
  return api.post<CampaignResponse>(`/api/campaigns/${id}/start`);
}

export function pauseCampaign(id: string) {
  return api.post<CampaignResponse>(`/api/campaigns/${id}/pause`);
}

export function resumeCampaign(id: string) {
  return api.post<CampaignResponse>(`/api/campaigns/${id}/resume`);
}

export function cancelCampaign(id: string) {
  return api.post<CampaignResponse>(`/api/campaigns/${id}/cancel`);
}

export function scheduleCampaign(id: string, scheduledAt: string) {
  return api.post<CampaignResponse>(`/api/campaigns/${id}/schedule`, { scheduledAt });
}

export function listCampaignRecipients(id: string, params?: { status?: string; limit?: number }) {
  return api.get<CampaignRecipientResponse[]>(`/api/campaigns/${id}/recipients${qs(params ?? {})}`);
}
