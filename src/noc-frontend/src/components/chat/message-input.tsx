// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useRef, useState } from 'react';
import { Send, Lock, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { sendMessage } from '@/lib/api/messages';
import type { MessageResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { cn } from '@/lib/utils';

interface MessageInputProps {
  conversationId: string;
  onSent: (message: MessageResponse) => void;
}

export function MessageInput({ conversationId, onSent }: MessageInputProps) {
  const [content, setContent] = useState('');
  const [isNote, setIsNote] = useState(false);
  const [sending, setSending] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  async function handleSend() {
    const trimmed = content.trim();
    if (!trimmed || sending) return;

    setSending(true);
    try {
      const msg = await sendMessage(conversationId, {
        content: trimmed,
        type: 'TEXT',
        isPrivateNote: isNote,
      });
      onSent(msg);
      setContent('');
      setIsNote(false);
      // Reset textarea height
      if (textareaRef.current) textareaRef.current.style.height = 'auto';
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al enviar mensaje');
    } finally {
      setSending(false);
    }
  }

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  }

  function handleInput() {
    if (!textareaRef.current) return;
    textareaRef.current.style.height = 'auto';
    textareaRef.current.style.height = Math.min(textareaRef.current.scrollHeight, 160) + 'px';
  }

  return (
    <div className={cn('border-t px-4 py-3', isNote ? 'border-blue-500/30 bg-blue-500/5' : 'border-zinc-800/60')}>
      {isNote && (
        <div className="mb-2 flex items-center gap-1.5 text-[10px] text-blue-400/70">
          <Lock className="h-2.5 w-2.5" />
          Nota privada — solo visible para el equipo
        </div>
      )}

      <div className="flex items-end gap-2">
        <textarea
          ref={textareaRef}
          value={content}
          onChange={(e) => setContent(e.target.value)}
          onKeyDown={handleKeyDown}
          onInput={handleInput}
          placeholder={isNote ? 'Escribí una nota privada...' : 'Escribí un mensaje...'}
          rows={1}
          className={cn(
            'flex-1 resize-none rounded-md border bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors',
            isNote
              ? 'border-blue-500/30 focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25'
              : 'border-zinc-800 focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25',
          )}
        />

        <div className="flex items-center gap-1">
          {/* Note toggle */}
          <button
            onClick={() => setIsNote(!isNote)}
            title={isNote ? 'Cambiar a mensaje' : 'Nota privada'}
            className={cn(
              'grid h-8 w-8 place-items-center rounded-md transition-colors',
              isNote
                ? 'bg-blue-500/15 text-blue-400'
                : 'text-zinc-500 hover:bg-zinc-800 hover:text-zinc-300',
            )}
          >
            <Lock className="h-3.5 w-3.5" />
          </button>

          {/* Send */}
          <button
            onClick={handleSend}
            disabled={!content.trim() || sending}
            className="grid h-8 w-8 place-items-center rounded-md bg-blue-600 text-white transition-colors hover:bg-blue-500 disabled:opacity-40"
          >
            {sending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Send className="h-3.5 w-3.5" />}
          </button>
        </div>
      </div>
    </div>
  );
}
