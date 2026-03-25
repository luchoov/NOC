// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Loader2, Play, Pause, Square, RotateCcw, X, Send, CheckCheck, Eye, AlertTriangle, Users } from 'lucide-react';
import { toast } from 'sonner';
import type { CampaignResponse, CampaignRecipientResponse } from '@/types/api';
import { getCampaign, startCampaign, pauseCampaign, resumeCampaign, cancelCampaign, listCampaignRecipients } from '@/lib/api/campaigns';
import { CampaignStatusBadge } from './campaign-status-badge';
import { timeAgo } from '@/lib/utils/format-date';

interface CampaignDetailModalProps {
  campaign: CampaignResponse | null;
  onClose: () => void;
  onUpdated: (campaign: CampaignResponse) => void;
}

export function CampaignDetailModal({ campaign, onClose, onUpdated }: CampaignDetailModalProps) {
  const [current, setCurrent] = useState<CampaignResponse | null>(campaign);
  const [recipients, setRecipients] = useState<CampaignRecipientResponse[]>([]);
  const [loadingRecipients, setLoadingRecipients] = useState(false);
  const [acting, setActing] = useState(false);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const refresh = useCallback(async () => {
    if (!campaign) return;
    try {
      const updated = await getCampaign(campaign.id);
      setCurrent(updated);
      onUpdated(updated);
    } catch { /* silent */ }
  }, [campaign, onUpdated]);

  const loadRecipients = useCallback(async () => {
    if (!campaign) return;
    setLoadingRecipients(true);
    try {
      const data = await listCampaignRecipients(campaign.id, { limit: 200 });
      setRecipients(data);
    } catch { /* silent */ }
    setLoadingRecipients(false);
  }, [campaign]);

  useEffect(() => {
    if (campaign) {
      setCurrent(campaign);
      loadRecipients();
    }
  }, [campaign, loadRecipients]);

  // Poll while running
  useEffect(() => {
    if (current?.status === 'RUNNING') {
      pollRef.current = setInterval(() => {
        refresh();
        loadRecipients();
      }, 4000);
    }
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, [current?.status, refresh, loadRecipients]);

  if (!campaign || !current) return null;

  const progress = current.totalRecipients > 0
    ? Math.round(((current.sentCount + current.failedCount) / current.totalRecipients) * 100)
    : 0;

  async function doAction(action: () => Promise<CampaignResponse>, label: string) {
    setActing(true);
    try {
      const updated = await action();
      setCurrent(updated);
      onUpdated(updated);
      toast.success(label);
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'Error');
    }
    setActing(false);
  }

  const RECIPIENT_STATUS_ICONS: Record<string, typeof Send> = {
    QUEUED: Users,
    CLAIMED: Users,
    SENT: Send,
    DELIVERED: CheckCheck,
    READ: Eye,
    FAILED: AlertTriangle,
    RETRY_PENDING: RotateCcw,
  };

  const RECIPIENT_STATUS_COLORS: Record<string, string> = {
    QUEUED: 'text-zinc-500',
    CLAIMED: 'text-zinc-400',
    SENT: 'text-blue-400',
    DELIVERED: 'text-emerald-400',
    READ: 'text-green-400',
    FAILED: 'text-red-400',
    RETRY_PENDING: 'text-yellow-400',
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm" onClick={onClose}>
      <div className="flex max-h-[90vh] w-full max-w-2xl flex-col rounded-2xl border border-zinc-800 bg-zinc-900 shadow-2xl" onClick={(e) => e.stopPropagation()}>
        {/* Header */}
        <div className="flex shrink-0 items-center justify-between border-b border-zinc-800/60 px-5 py-4">
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <h2 className="truncate text-sm font-semibold text-zinc-100">{current.name}</h2>
              <CampaignStatusBadge status={current.status} />
            </div>
            <p className="mt-0.5 text-[10px] text-zinc-500">
              {current.inboxName} &middot; Creada {timeAgo(current.createdAt)}
            </p>
          </div>
          <button onClick={onClose} className="grid h-8 w-8 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300">
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-auto p-5 space-y-5">
          {/* Progress */}
          <div>
            <div className="flex items-center justify-between text-xs text-zinc-400 mb-1.5">
              <span>Progreso</span>
              <span>{progress}%</span>
            </div>
            <div className="h-2 overflow-hidden rounded-full bg-zinc-800">
              <div
                className={`h-full rounded-full transition-all ${
                  current.status === 'FAILED' ? 'bg-red-500' :
                  current.status === 'COMPLETED' ? 'bg-emerald-500' :
                  'bg-blue-500'
                }`}
                style={{ width: `${progress}%` }}
              />
            </div>
          </div>

          {/* Stats grid */}
          <div className="grid grid-cols-5 gap-2">
            {[
              { label: 'Total', value: current.totalRecipients, color: 'text-zinc-200' },
              { label: 'Enviados', value: current.sentCount, color: 'text-blue-400' },
              { label: 'Entregados', value: current.deliveredCount, color: 'text-emerald-400' },
              { label: 'Leidos', value: current.readCount, color: 'text-green-400' },
              { label: 'Fallidos', value: current.failedCount, color: 'text-red-400' },
            ].map((stat) => (
              <div key={stat.label} className="rounded-lg border border-zinc-800/60 bg-zinc-950/50 p-3 text-center">
                <p className={`text-lg font-bold ${stat.color}`}>{stat.value}</p>
                <p className="text-[10px] text-zinc-500">{stat.label}</p>
              </div>
            ))}
          </div>

          {/* Actions */}
          <div className="flex items-center gap-2">
            {current.status === 'DRAFT' && (
              <button
                onClick={() => doAction(() => startCampaign(current.id), 'Campana iniciada')}
                disabled={acting}
                className="flex items-center gap-1.5 rounded-md bg-green-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-green-500 disabled:opacity-50"
              >
                {acting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Play className="h-3.5 w-3.5" />}
                Iniciar envio
              </button>
            )}
            {current.status === 'SCHEDULED' && (
              <button
                onClick={() => doAction(() => startCampaign(current.id), 'Campana iniciada')}
                disabled={acting}
                className="flex items-center gap-1.5 rounded-md bg-green-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-green-500 disabled:opacity-50"
              >
                {acting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Play className="h-3.5 w-3.5" />}
                Iniciar ahora
              </button>
            )}
            {current.status === 'RUNNING' && (
              <button
                onClick={() => doAction(() => pauseCampaign(current.id), 'Campana pausada')}
                disabled={acting}
                className="flex items-center gap-1.5 rounded-md bg-yellow-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-yellow-500 disabled:opacity-50"
              >
                {acting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Pause className="h-3.5 w-3.5" />}
                Pausar
              </button>
            )}
            {current.status === 'PAUSED' && (
              <button
                onClick={() => doAction(() => resumeCampaign(current.id), 'Campana reanudada')}
                disabled={acting}
                className="flex items-center gap-1.5 rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
              >
                {acting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Play className="h-3.5 w-3.5" />}
                Reanudar
              </button>
            )}
            {(current.status === 'RUNNING' || current.status === 'PAUSED' || current.status === 'DRAFT' || current.status === 'SCHEDULED') && (
              <button
                onClick={() => {
                  if (confirm('Cancelar esta campana?'))
                    doAction(() => cancelCampaign(current.id), 'Campana cancelada');
                }}
                disabled={acting}
                className="flex items-center gap-1.5 rounded-md border border-red-500/30 px-3 py-2 text-xs font-medium text-red-400 transition-colors hover:bg-red-500/10 disabled:opacity-50"
              >
                <Square className="h-3.5 w-3.5" />
                Cancelar
              </button>
            )}
            {current.pausedReason && (
              <span className="text-[10px] text-yellow-400">{current.pausedReason}</span>
            )}
          </div>

          {/* Message preview */}
          <div>
            <h3 className="mb-1.5 text-xs font-medium text-zinc-400">Mensaje</h3>
            <div className="rounded-lg border border-zinc-800/60 bg-zinc-950/50 p-3 text-sm text-zinc-300 whitespace-pre-wrap">
              {current.messageTemplate}
            </div>
          </div>

          {/* Recipients */}
          <div>
            <h3 className="mb-1.5 text-xs font-medium text-zinc-400">Destinatarios</h3>
            {loadingRecipients ? (
              <div className="flex h-20 items-center justify-center">
                <Loader2 className="h-4 w-4 animate-spin text-zinc-600" />
              </div>
            ) : (
              <div className="max-h-60 overflow-auto rounded-lg border border-zinc-800/60">
                <table className="w-full text-xs">
                  <thead className="sticky top-0 bg-zinc-900">
                    <tr className="border-b border-zinc-800/40">
                      <th className="px-3 py-2 text-left font-medium text-zinc-500">Contacto</th>
                      <th className="px-3 py-2 text-left font-medium text-zinc-500">Telefono</th>
                      <th className="px-3 py-2 text-left font-medium text-zinc-500">Estado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {recipients.map((r) => {
                      const Icon = RECIPIENT_STATUS_ICONS[r.status] ?? Users;
                      const color = RECIPIENT_STATUS_COLORS[r.status] ?? 'text-zinc-500';
                      return (
                        <tr key={r.id} className="border-b border-zinc-800/20">
                          <td className="px-3 py-2 text-zinc-300">{r.contactName ?? '-'}</td>
                          <td className="px-3 py-2 text-zinc-400">{r.phone}</td>
                          <td className="px-3 py-2">
                            <span className={`flex items-center gap-1 ${color}`}>
                              <Icon className="h-3 w-3" />
                              {r.status}
                            </span>
                            {r.failureReason && (
                              <p className="mt-0.5 text-[10px] text-red-400/70">{r.failureReason}</p>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
