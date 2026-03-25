// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { Send, Users, Trash2, CheckCheck, Eye, AlertTriangle } from 'lucide-react';
import { toast } from 'sonner';
import type { CampaignResponse } from '@/types/api';
import { CampaignStatusBadge } from './campaign-status-badge';
import { deleteCampaign } from '@/lib/api/campaigns';
import { timeAgo } from '@/lib/utils/format-date';

interface CampaignCardProps {
  campaign: CampaignResponse;
  onClick: () => void;
  onDeleted: (id: string) => void;
}

export function CampaignCard({ campaign, onClick, onDeleted }: CampaignCardProps) {
  const processed = campaign.sentCount + campaign.failedCount;
  const progress = campaign.totalRecipients > 0 ? Math.round((processed / campaign.totalRecipients) * 100) : 0;
  const isActive = campaign.status === 'RUNNING' || campaign.status === 'PAUSED';
  const isDone = campaign.status === 'COMPLETED' || campaign.status === 'FAILED';

  async function handleDelete(e: React.MouseEvent) {
    e.stopPropagation();
    if (campaign.status !== 'DRAFT') return;
    if (!confirm('Eliminar esta campana?')) return;
    try {
      await deleteCampaign(campaign.id);
      onDeleted(campaign.id);
      toast.success('Campana eliminada');
    } catch {
      toast.error('Error al eliminar');
    }
  }

  return (
    <div
      onClick={onClick}
      className="group cursor-pointer rounded-xl border border-zinc-800/60 bg-zinc-900/50 p-4 transition-all hover:border-zinc-700 hover:bg-zinc-900"
    >
      {/* Header row */}
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <h3 className="truncate text-sm font-medium text-zinc-100">{campaign.name}</h3>
          <p className="mt-0.5 text-[10px] text-zinc-500">
            {campaign.inboxName} &middot; {timeAgo(campaign.createdAt)}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-1.5">
          <CampaignStatusBadge status={campaign.status} />
          {campaign.status === 'DRAFT' && (
            <button
              onClick={handleDelete}
              className="grid h-6 w-6 place-items-center rounded-md text-zinc-600 opacity-0 transition-all hover:bg-red-500/10 hover:text-red-400 group-hover:opacity-100"
            >
              <Trash2 className="h-3 w-3" />
            </button>
          )}
        </div>
      </div>

      {/* Message preview */}
      <p className="mt-2 line-clamp-2 text-xs text-zinc-500">{campaign.messageTemplate}</p>

      {/* Progress bar for active/done campaigns */}
      {(isActive || isDone) && campaign.totalRecipients > 0 && (
        <div className="mt-3">
          <div className="mb-1 flex items-center justify-between text-[10px] text-zinc-500">
            <span>{processed} / {campaign.totalRecipients}</span>
            <span>{progress}%</span>
          </div>
          <div className="h-1.5 overflow-hidden rounded-full bg-zinc-800">
            <div
              className={`h-full rounded-full transition-all duration-500 ${
                campaign.status === 'FAILED' ? 'bg-red-500' :
                campaign.status === 'COMPLETED' ? 'bg-emerald-500' :
                campaign.status === 'PAUSED' ? 'bg-yellow-500' :
                'bg-blue-500'
              }`}
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>
      )}

      {/* Stats row */}
      <div className="mt-3 flex items-center gap-3 text-[10px] text-zinc-500">
        <span className="flex items-center gap-1" title="Destinatarios">
          <Users className="h-3 w-3" />
          {campaign.totalRecipients}
        </span>
        {campaign.sentCount > 0 && (
          <span className="flex items-center gap-1 text-blue-400" title="Enviados">
            <Send className="h-3 w-3" />
            {campaign.sentCount}
          </span>
        )}
        {campaign.deliveredCount > 0 && (
          <span className="flex items-center gap-1 text-emerald-400" title="Entregados">
            <CheckCheck className="h-3 w-3" />
            {campaign.deliveredCount}
          </span>
        )}
        {campaign.readCount > 0 && (
          <span className="flex items-center gap-1 text-green-400" title="Leidos">
            <Eye className="h-3 w-3" />
            {campaign.readCount}
          </span>
        )}
        {campaign.failedCount > 0 && (
          <span className="flex items-center gap-1 text-red-400" title="Fallidos">
            <AlertTriangle className="h-3 w-3" />
            {campaign.failedCount}
          </span>
        )}
      </div>

      {/* Scheduled date */}
      {campaign.status === 'SCHEDULED' && campaign.scheduledAt && (
        <p className="mt-2 text-[10px] text-blue-400">
          Programada: {new Date(campaign.scheduledAt).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' })}
        </p>
      )}
    </div>
  );
}
