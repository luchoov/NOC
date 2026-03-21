// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useState } from 'react';
import { Lock, FileText, Download, X } from 'lucide-react';
import type { MessageResponse } from '@/types/api';
import { DELIVERY } from '@/lib/utils/constants';
import { formatTime } from '@/lib/utils/format-date';
import { cn } from '@/lib/utils';

interface MessageBubbleProps {
  message: MessageResponse;
}

function formatFileSize(bytes: number | null): string {
  if (!bytes) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function mediaUrl(m: MessageResponse): string {
  return `/api/conversations/${m.conversationId}/messages/${m.id}/media`;
}

export function MessageBubble({ message: m }: MessageBubbleProps) {
  const [lightbox, setLightbox] = useState(false);
  const isOutbound = m.direction === 'OUTBOUND';
  const isNote = m.isPrivateNote;
  const isSystem = m.type === 'SYSTEM';
  const isSticker = m.type === 'STICKER';

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

  // Sticker: no bubble background, just the image
  if (isSticker && m.mediaUrl) {
    return (
      <div className={cn('flex py-0.5', isOutbound ? 'justify-end' : 'justify-start')}>
        <div className="max-w-[160px]">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={mediaUrl(m)}
            alt="Sticker"
            className="h-32 w-32 object-contain"
            loading="lazy"
          />
          <div className={cn('mt-0.5 flex items-center gap-1.5', isOutbound ? 'justify-end' : 'justify-start')}>
            <span className="text-[9px] text-zinc-500">{formatTime(m.createdAt)}</span>
            {isOutbound && <DeliveryIcon m={m} />}
          </div>
        </div>
      </div>
    );
  }

  const deliveryInfo = m.deliveryStatus ? DELIVERY[m.deliveryStatus] : null;

  return (
    <>
      <div className={cn('flex py-0.5', isOutbound ? 'justify-end' : 'justify-start')}>
        <div
          className={cn(
            'max-w-[70%] rounded-lg px-3 py-1.5',
            isOutbound
              ? 'bg-blue-600/20 text-zinc-200'
              : 'bg-zinc-800/70 text-zinc-300',
          )}
        >
          {/* Image */}
          {m.mediaUrl && m.type === 'IMAGE' && (
            <button
              type="button"
              onClick={() => setLightbox(true)}
              className="mb-1.5 block w-full overflow-hidden rounded-md cursor-pointer"
            >
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={mediaUrl(m)}
                alt=""
                className="max-h-64 w-full object-cover"
                loading="lazy"
              />
            </button>
          )}

          {/* Video */}
          {m.mediaUrl && m.type === 'VIDEO' && (
            <div className="mb-1.5 overflow-hidden rounded-md">
              <video
                controls
                preload="metadata"
                className="max-h-64 w-full"
              >
                <source src={mediaUrl(m)} type={m.mediaMimeType ?? 'video/mp4'} />
              </video>
            </div>
          )}

          {/* Audio */}
          {m.mediaUrl && m.type === 'AUDIO' && (
            <div className="mb-1.5">
              <audio controls className="h-10 w-full min-w-[220px]" preload="none">
                <source src={mediaUrl(m)} type={m.mediaMimeType ?? 'audio/ogg'} />
              </audio>
            </div>
          )}

          {/* Document */}
          {m.mediaUrl && m.type === 'DOCUMENT' && (
            <a
              href={mediaUrl(m)}
              target="_blank"
              rel="noopener noreferrer"
              className="mb-1.5 flex items-center gap-2.5 rounded-md bg-zinc-700/30 px-3 py-2"
            >
              <FileText className="h-5 w-5 shrink-0 text-blue-400" />
              <div className="min-w-0 flex-1">
                <p className="truncate text-xs font-medium text-blue-400">
                  {m.mediaFilename ?? 'Documento'}
                </p>
                {m.mediaSizeBytes && (
                  <p className="text-[10px] text-zinc-500">{formatFileSize(m.mediaSizeBytes)}</p>
                )}
              </div>
              <Download className="h-3.5 w-3.5 shrink-0 text-zinc-500" />
            </a>
          )}

          {/* Location placeholder */}
          {m.type === 'LOCATION' && (
            <p className="mb-1 text-[10px] text-zinc-500">Ubicaci&oacute;n compartida</p>
          )}

          {/* Text / caption */}
          {m.content && (
            <p className="text-[13px] leading-relaxed whitespace-pre-wrap break-words">{m.content}</p>
          )}

          {/* Footer */}
          <div className={cn('mt-0.5 flex items-center gap-1.5', isOutbound ? 'justify-end' : 'justify-start')}>
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

      {/* Image lightbox */}
      {lightbox && m.mediaUrl && m.type === 'IMAGE' && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm"
          onClick={() => setLightbox(false)}
        >
          <button
            type="button"
            onClick={() => setLightbox(false)}
            className="absolute right-4 top-4 rounded-full bg-zinc-800/80 p-2 text-zinc-300 hover:bg-zinc-700"
          >
            <X className="h-5 w-5" />
          </button>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={mediaUrl(m)}
            alt=""
            className="max-h-[85vh] max-w-[90vw] rounded-lg object-contain"
            onClick={(e) => e.stopPropagation()}
          />
        </div>
      )}
    </>
  );
}

function DeliveryIcon({ m }: { m: MessageResponse }) {
  const info = m.deliveryStatus ? DELIVERY[m.deliveryStatus] : null;
  if (!info) return null;
  return (
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
      {info.icon}
    </span>
  );
}
