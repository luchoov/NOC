// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Loader2, Plus, RefreshCw, Send, Users, CheckCheck, AlertTriangle } from 'lucide-react';
import { toast } from 'sonner';
import { listCampaigns } from '@/lib/api/campaigns';
import type { CampaignResponse, CampaignStatus } from '@/types/api';
import { CampaignCard } from '@/components/campaigns/campaign-card';
import { CampaignCreateModal } from '@/components/campaigns/campaign-create-modal';
import { CampaignDetailModal } from '@/components/campaigns/campaign-detail-modal';
import { cn } from '@/lib/utils';

type Filter = 'all' | CampaignStatus;

const FILTERS: { value: Filter; label: string }[] = [
  { value: 'all', label: 'Todas' },
  { value: 'DRAFT', label: 'Borrador' },
  { value: 'SCHEDULED', label: 'Programadas' },
  { value: 'RUNNING', label: 'Enviando' },
  { value: 'COMPLETED', label: 'Completadas' },
  { value: 'FAILED', label: 'Fallidas' },
];

export default function CampaignsPage() {
  const [campaigns, setCampaigns] = useState<CampaignResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [detailCampaign, setDetailCampaign] = useState<CampaignResponse | null>(null);
  const [filter, setFilter] = useState<Filter>('all');

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

  // Auto-refresh while any campaign is running
  useEffect(() => {
    const hasRunning = campaigns.some((c) => c.status === 'RUNNING');
    if (!hasRunning) return;
    const interval = setInterval(fetchCampaigns, 5000);
    return () => clearInterval(interval);
  }, [campaigns, fetchCampaigns]);

  const filteredCampaigns = useMemo(
    () => filter === 'all' ? campaigns : campaigns.filter((c) => c.status === filter),
    [campaigns, filter],
  );

  const stats = useMemo(() => {
    const totals = { recipients: 0, sent: 0, delivered: 0, failed: 0 };
    for (const c of campaigns) {
      totals.recipients += c.totalRecipients;
      totals.sent += c.sentCount;
      totals.delivered += c.deliveredCount;
      totals.failed += c.failedCount;
    }
    return totals;
  }, [campaigns]);

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

      {/* Stats summary */}
      {campaigns.length > 0 && (
        <div className="mb-5 grid grid-cols-2 gap-2 sm:grid-cols-4">
          {[
            { label: 'Destinatarios', value: stats.recipients, icon: Users, color: 'text-zinc-200' },
            { label: 'Enviados', value: stats.sent, icon: Send, color: 'text-blue-400' },
            { label: 'Entregados', value: stats.delivered, icon: CheckCheck, color: 'text-emerald-400' },
            { label: 'Fallidos', value: stats.failed, icon: AlertTriangle, color: 'text-red-400' },
          ].map((stat) => (
            <div key={stat.label} className="flex items-center gap-3 rounded-xl border border-zinc-800/60 bg-zinc-900/50 p-3">
              <stat.icon className={`h-4 w-4 ${stat.color}`} />
              <div>
                <p className={`text-sm font-bold ${stat.color}`}>{stat.value.toLocaleString('es-AR')}</p>
                <p className="text-[10px] text-zinc-500">{stat.label}</p>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Filters */}
      {campaigns.length > 0 && (
        <div className="mb-5 flex items-center gap-1 overflow-x-auto">
          {FILTERS.map((f) => {
            const count = f.value === 'all' ? campaigns.length : campaigns.filter((c) => c.status === f.value).length;
            if (count === 0 && f.value !== 'all') return null;
            return (
              <button
                key={f.value}
                onClick={() => setFilter(f.value)}
                className={cn(
                  'shrink-0 rounded-md px-2.5 py-1.5 text-xs font-medium transition-colors',
                  filter === f.value ? 'bg-zinc-800 text-zinc-100' : 'text-zinc-500 hover:text-zinc-300',
                )}
              >
                {f.label} ({count})
              </button>
            );
          })}
        </div>
      )}

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
      ) : filteredCampaigns.length === 0 ? (
        <div className="flex h-40 items-center justify-center rounded-2xl border border-dashed border-zinc-800">
          <p className="text-sm text-zinc-500">No hay campanas con este filtro</p>
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {filteredCampaigns.map((campaign) => (
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
