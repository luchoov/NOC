// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useState } from 'react';
import { User, CheckCircle2, RotateCcw, UserPlus, PanelRightOpen, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { assignConversation, updateConversationStatus } from '@/lib/api/conversations';
import { useAuthStore } from '@/lib/store/auth.store';
import type { ConversationResponse, ConversationStatus } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { CONVERSATION_STATUS } from '@/lib/utils/constants';
import { cn } from '@/lib/utils';

interface ChatHeaderProps {
  conversation: ConversationResponse;
  onToggleContactPanel: () => void;
  onConversationUpdated: (c: ConversationResponse) => void;
}

export function ChatHeader({ conversation: c, onToggleContactPanel, onConversationUpdated }: ChatHeaderProps) {
  const agent = useAuthStore((s) => s.agent);
  const [assigning, setAssigning] = useState(false);
  const [updatingStatus, setUpdatingStatus] = useState(false);

  const displayName = c.contactName || c.contactPhone;
  const statusCfg = CONVERSATION_STATUS[c.status] ?? CONVERSATION_STATUS.OPEN;
  const isAssignedToMe = c.assignedTo === agent?.id;
  const isResolved = c.status === 'RESOLVED' || c.status === 'ARCHIVED';

  async function handleTake() {
    if (!agent?.id) return;
    setAssigning(true);
    try {
      const updated = await assignConversation(c.id, {
        agentId: agent.id,
        expectedRowVersion: c.rowVersion,
      });
      onConversationUpdated(updated);
      toast.success('Conversación asignada');
    } catch (e: unknown) {
      const err = e as ApiError;
      if (err.status === 409) {
        toast.error('La conversación fue modificada. Refrescá e intentá de nuevo.');
      } else {
        toast.error(err.detail || 'Error al asignar');
      }
    } finally {
      setAssigning(false);
    }
  }

  async function handleStatusChange(status: ConversationStatus) {
    setUpdatingStatus(true);
    try {
      const updated = await updateConversationStatus(c.id, {
        status,
        expectedRowVersion: c.rowVersion,
      });
      onConversationUpdated(updated);
      toast.success(`Conversación ${status === 'RESOLVED' ? 'resuelta' : 'reabierta'}`);
    } catch (e: unknown) {
      const err = e as ApiError;
      if (err.status === 409) {
        toast.error('La conversación fue modificada. Refrescá e intentá de nuevo.');
      } else {
        toast.error(err.detail || 'Error al actualizar estado');
      }
    } finally {
      setUpdatingStatus(false);
    }
  }

  return (
    <div className="flex h-12 shrink-0 items-center justify-between border-b border-zinc-800/60 px-4">
      <div className="flex items-center gap-3 min-w-0">
        <div className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-zinc-800 text-xs font-medium text-zinc-400">
          {displayName.charAt(0).toUpperCase()}
        </div>
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-zinc-200">{displayName}</p>
          <div className="flex items-center gap-1.5">
            <span className={cn('rounded-full px-1.5 py-0.5 text-[9px] font-medium leading-none', statusCfg.class)}>
              {statusCfg.label}
            </span>
            {c.contactPhone !== displayName && (
              <span className="text-[10px] text-zinc-600">{c.contactPhone}</span>
            )}
          </div>
        </div>
      </div>

      <div className="flex items-center gap-1">
        {/* Take / assign to me */}
        {!isAssignedToMe && (
          <button
            onClick={handleTake}
            disabled={assigning}
            title="Tomar conversación"
            className="flex items-center gap-1.5 rounded-md px-2.5 py-1 text-[11px] font-medium text-zinc-400 transition-colors hover:bg-blue-500/10 hover:text-blue-400 disabled:opacity-50"
          >
            {assigning ? <Loader2 className="h-3 w-3 animate-spin" /> : <UserPlus className="h-3 w-3" />}
            Tomar
          </button>
        )}

        {/* Resolve / Reopen */}
        {!isResolved ? (
          <button
            onClick={() => handleStatusChange('RESOLVED')}
            disabled={updatingStatus}
            title="Resolver"
            className="flex items-center gap-1.5 rounded-md px-2.5 py-1 text-[11px] font-medium text-zinc-400 transition-colors hover:bg-emerald-500/10 hover:text-emerald-400 disabled:opacity-50"
          >
            {updatingStatus ? <Loader2 className="h-3 w-3 animate-spin" /> : <CheckCircle2 className="h-3 w-3" />}
            Resolver
          </button>
        ) : (
          <button
            onClick={() => handleStatusChange('OPEN')}
            disabled={updatingStatus}
            title="Reabrir"
            className="flex items-center gap-1.5 rounded-md px-2.5 py-1 text-[11px] font-medium text-zinc-400 transition-colors hover:bg-sky-500/10 hover:text-sky-400 disabled:opacity-50"
          >
            {updatingStatus ? <Loader2 className="h-3 w-3 animate-spin" /> : <RotateCcw className="h-3 w-3" />}
            Reabrir
          </button>
        )}

        {/* Contact panel toggle */}
        <button
          onClick={onToggleContactPanel}
          title="Info del contacto"
          className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
        >
          <PanelRightOpen className="h-3.5 w-3.5" />
        </button>
      </div>
    </div>
  );
}
