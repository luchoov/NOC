// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import type { ConversationResponse } from '@/types/api';
import { CONVERSATION_STATUS } from '@/lib/utils/constants';
import { timeAgo } from '@/lib/utils/format-date';
import { cn } from '@/lib/utils';

interface ConversationListItemProps {
  conversation: ConversationResponse;
  active: boolean;
  onClick: () => void;
}

export function ConversationListItem({ conversation: c, active, onClick }: ConversationListItemProps) {
  const statusCfg = CONVERSATION_STATUS[c.status] ?? CONVERSATION_STATUS.OPEN;
  const displayName = c.contactName || c.contactPhone;
  const initial = displayName.charAt(0).toUpperCase();

  return (
    <button
      onClick={onClick}
      className={cn(
        'flex w-full items-start gap-2.5 border-b border-zinc-800/40 px-3 py-2.5 text-left transition-colors',
        active ? 'bg-blue-500/8' : 'hover:bg-zinc-800/40',
      )}
    >
      {/* Avatar */}
      <div
        className={cn(
          'grid h-8 w-8 shrink-0 place-items-center rounded-full text-xs font-medium',
          active ? 'bg-blue-500/20 text-blue-400' : 'bg-zinc-800 text-zinc-400',
        )}
      >
        {initial}
      </div>

      {/* Content */}
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-2">
          <span className={cn('truncate text-[13px] font-medium', active ? 'text-zinc-100' : 'text-zinc-300')}>
            {displayName}
          </span>
          {c.lastMessageAt && (
            <span className="shrink-0 text-[10px] text-zinc-600">{timeAgo(c.lastMessageAt)}</span>
          )}
        </div>

        <div className="mt-0.5 flex items-center gap-1.5">
          {c.lastMessageDirection === 'OUTBOUND' && (
            <span className="shrink-0 text-[10px] text-zinc-600">→</span>
          )}
          <p className="truncate text-xs text-zinc-500">{c.lastMessagePreview || 'Sin mensajes'}</p>
        </div>

        <div className="mt-1 flex items-center gap-1.5">
          <span className={cn('rounded-full px-1.5 py-0.5 text-[9px] font-medium leading-none', statusCfg.class)}>
            {statusCfg.label}
          </span>
          {c.unreadCount > 0 && (
            <span className="grid h-4 min-w-4 place-items-center rounded-full bg-blue-500 px-1 text-[9px] font-bold text-white">
              {c.unreadCount > 99 ? '99+' : c.unreadCount}
            </span>
          )}
        </div>
      </div>
    </button>
  );
}
