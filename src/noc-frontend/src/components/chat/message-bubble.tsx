// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { Lock } from 'lucide-react';
import type { MessageResponse } from '@/types/api';
import { DELIVERY } from '@/lib/utils/constants';
import { formatTime } from '@/lib/utils/format-date';
import { cn } from '@/lib/utils';

interface MessageBubbleProps {
  message: MessageResponse;
}

export function MessageBubble({ message: m }: MessageBubbleProps) {
  const isOutbound = m.direction === 'OUTBOUND';
  const isNote = m.isPrivateNote;
  const isSystem = m.type === 'SYSTEM';

  if (isSystem) {
    return (
      <div className="flex justify-center py-1.5">
        <span className="rounded-full bg-zinc-800/60 px-3 py-1 text-[10px] text-zinc-500">
          {m.content}
        </span>
      </div>
    );
  }

  if (isNote) {
    return (
      <div className="flex justify-center py-1">
        <div className="max-w-md rounded-md border border-blue-500/15 bg-blue-500/5 px-3 py-2">
          <div className="flex items-center gap-1.5 text-[10px] text-blue-400/70">
            <Lock className="h-2.5 w-2.5" />
            Nota privada
          </div>
          <p className="mt-1 text-xs text-blue-300/80 whitespace-pre-wrap">{m.content}</p>
          <span className="mt-1 block text-right text-[9px] text-blue-400/40">
            {formatTime(m.createdAt)}
          </span>
        </div>
      </div>
    );
  }

  const deliveryInfo = m.deliveryStatus ? DELIVERY[m.deliveryStatus] : null;

  return (
    <div className={cn('flex py-0.5', isOutbound ? 'justify-end' : 'justify-start')}>
      <div
        className={cn(
          'max-w-[70%] rounded-lg px-3 py-2',
          isOutbound
            ? 'bg-blue-600/20 text-zinc-200'
            : 'bg-zinc-800/70 text-zinc-300',
        )}
      >
        {/* Media preview */}
        {m.mediaUrl && (m.type === 'IMAGE' || m.type === 'STICKER') && (
          <div className="mb-1.5 overflow-hidden rounded-md">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src={`/api/messages/${m.id}/media`}
              alt=""
              className="max-h-48 w-full object-cover"
              loading="lazy"
            />
          </div>
        )}

        {m.type === 'AUDIO' && (
          <div className="mb-1.5">
            <audio controls className="h-8 w-full" preload="none">
              <source src={`/api/messages/${m.id}/media`} />
            </audio>
          </div>
        )}

        {m.type === 'DOCUMENT' && (
          <a
            href={`/api/messages/${m.id}/media`}
            target="_blank"
            rel="noopener noreferrer"
            className="mb-1.5 flex items-center gap-2 rounded-md bg-zinc-700/30 px-2 py-1.5 text-xs text-blue-400 hover:underline"
          >
            Documento adjunto
          </a>
        )}

        {m.type === 'LOCATION' && (
          <p className="mb-1 text-[10px] text-zinc-500">Ubicación compartida</p>
        )}

        {/* Text content */}
        {m.content && (
          <p className="text-[13px] leading-relaxed whitespace-pre-wrap break-words">{m.content}</p>
        )}

        {/* Footer: time + delivery status */}
        <div className={cn('mt-1 flex items-center gap-1.5', isOutbound ? 'justify-end' : 'justify-start')}>
          <span className="text-[9px] text-zinc-500">{formatTime(m.createdAt)}</span>
          {isOutbound && deliveryInfo && (
            <span
              className={cn(
                'text-[9px]',
                m.deliveryStatus === 'READ'
                  ? 'text-blue-400'
                  : m.deliveryStatus === 'FAILED'
                    ? 'text-red-400'
                    : 'text-zinc-500',
              )}
            >
              {deliveryInfo.icon}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
