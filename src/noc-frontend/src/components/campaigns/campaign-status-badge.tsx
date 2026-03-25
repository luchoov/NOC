// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import type { CampaignStatus } from '@/types/api';

const STATUS_CONFIG: Record<CampaignStatus, { label: string; className: string }> = {
  DRAFT: { label: 'Borrador', className: 'bg-zinc-500/10 text-zinc-400' },
  SCHEDULED: { label: 'Programada', className: 'bg-blue-500/10 text-blue-400' },
  RUNNING: { label: 'Enviando', className: 'bg-green-500/10 text-green-400' },
  PAUSED: { label: 'Pausada', className: 'bg-yellow-500/10 text-yellow-400' },
  COMPLETED: { label: 'Completada', className: 'bg-emerald-500/10 text-emerald-400' },
  FAILED: { label: 'Fallida', className: 'bg-red-500/10 text-red-400' },
};

export function CampaignStatusBadge({ status }: { status: CampaignStatus }) {
  const config = STATUS_CONFIG[status] ?? STATUS_CONFIG.DRAFT;
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-semibold ${config.className}`}>
      {status === 'RUNNING' && (
        <span className="mr-1 h-1.5 w-1.5 animate-pulse rounded-full bg-green-400" />
      )}
      {config.label}
    </span>
  );
}
