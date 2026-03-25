// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useState } from 'react';
import { Loader2, Plus, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { listCampaigns } from '@/lib/api/campaigns';
import type { CampaignResponse } from '@/types/api';
import { CampaignCard } from '@/components/campaigns/campaign-card';
import { CampaignCreateModal } from '@/components/campaigns/campaign-create-modal';
import { CampaignDetailModal } from '@/components/campaigns/campaign-detail-modal';
import { cn } from '@/lib/utils';

export default function CampaignsPage() {
  const [campaigns, setCampaigns] = useState<CampaignResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [detailCampaign, setDetailCampaign] = useState<CampaignResponse | null>(null);

  const fetchCampaigns = useCallback(async () => {
    setLoading(true);
    try {
      const data = await listCampaigns();
      setCampaigns(data);
    } catch {
      toast.error('Error al cargar campanas');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchCampaigns();
  }, [fetchCampaigns]);

  function handleCreated(saved: CampaignResponse) {
    setCampaigns((prev) => [saved, ...prev]);
    setShowCreate(false);
  }

  function handleUpdated(updated: CampaignResponse) {
    setCampaigns((prev) => prev.map((c) => (c.id === updated.id ? updated : c)));
    setDetailCampaign(updated);
  }

  return (
    <div className="min-h-full p-6">
      {/* Header */}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-lg font-semibold text-zinc-100">Campanas</h1>
          <p className="mt-1 text-xs text-zinc-500">
            Envia mensajes masivos a listas o segmentos de contactos.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={fetchCampaigns}
            disabled={loading}
            className="grid h-8 w-8 place-items-center rounded-md border border-zinc-800 text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300 disabled:opacity-40"
          >
            <RefreshCw className={cn('h-3.5 w-3.5', loading && 'animate-spin')} />
          </button>
          <button
            onClick={() => setShowCreate(true)}
            className="flex items-center gap-2 rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-blue-500"
          >
            <Plus className="h-3.5 w-3.5" />
            Nueva campana
          </button>
        </div>
      </div>

      {/* Content */}
      {loading ? (
        <div className="flex h-40 items-center justify-center">
          <Loader2 className="h-5 w-5 animate-spin text-zinc-600" />
        </div>
      ) : campaigns.length === 0 ? (
        <div className="flex h-40 flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-zinc-800 text-center">
          <p className="text-sm text-zinc-500">No hay campanas todavia</p>
          <button
            onClick={() => setShowCreate(true)}
            className="flex items-center gap-1.5 text-xs text-blue-400 transition-colors hover:text-blue-300"
          >
            <Plus className="h-3 w-3" />
            Crear primera campana
          </button>
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {campaigns.map((campaign) => (
            <CampaignCard
              key={campaign.id}
              campaign={campaign}
              onClick={() => setDetailCampaign(campaign)}
              onDeleted={(id) => setCampaigns((prev) => prev.filter((c) => c.id !== id))}
            />
          ))}
        </div>
      )}

      {/* Modals */}
      <CampaignCreateModal
        open={showCreate}
        onClose={() => setShowCreate(false)}
        onSaved={handleCreated}
      />

      <CampaignDetailModal
        campaign={detailCampaign}
        onClose={() => setDetailCampaign(null)}
        onUpdated={handleUpdated}
      />
    </div>
  );
}
