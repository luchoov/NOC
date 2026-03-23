// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useRef, useState } from 'react';
import { Send, Lock, Loader2, Paperclip, X, FileText, Image as ImageIcon, Film, Music, Mic, Square } from 'lucide-react';
import { toast } from 'sonner';
import { sendMessage, sendMediaMessage } from '@/lib/api/messages';
import type { MessageResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { cn } from '@/lib/utils';

interface MessageInputProps {
  conversationId: string;
  onSent: (message: MessageResponse) => void;
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function fileIcon(type: string) {
  if (type.startsWith('image/')) return <ImageIcon className="h-4 w-4" />;
  if (type.startsWith('video/')) return <Film className="h-4 w-4" />;
  if (type.startsWith('audio/')) return <Music className="h-4 w-4" />;
  return <FileText className="h-4 w-4" />;
}

const MAX_FILE_SIZE = 20 * 1024 * 1024; // 20 MB

export function MessageInput({ conversationId, onSent }: MessageInputProps) {
  const [content, setContent] = useState('');
  const [isNote, setIsNote] = useState(false);
  const [sending, setSending] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [filePreview, setFilePreview] = useState<string | null>(null);
  const [recording, setRecording] = useState(false);
  const [recordingTime, setRecordingTime] = useState(0);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const recordingTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  function handleFileSelect(e: React.ChangeEvent<HTMLInputElement>) {
    const selected = e.target.files?.[0];
    if (!selected) return;

    if (selected.size > MAX_FILE_SIZE) {
      toast.error('El archivo es muy grande. Máximo 20 MB.');
      return;
    }

    setFile(selected);

    // Generate preview for images
    if (selected.type.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = (ev) => setFilePreview(ev.target?.result as string);
      reader.readAsDataURL(selected);
    } else {
      setFilePreview(null);
    }

    // Reset file input
    if (fileInputRef.current) fileInputRef.current.value = '';
  }

  function clearFile() {
    setFile(null);
    setFilePreview(null);
  }

  async function startRecording() {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      // Prefer ogg/opus — WhatsApp treats webm as video and won't deliver it as audio
      const mimeType = MediaRecorder.isTypeSupported('audio/ogg;codecs=opus')
        ? 'audio/ogg;codecs=opus'
        : MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
          ? 'audio/webm;codecs=opus'
          : 'audio/webm';
      const ext = mimeType.includes('ogg') ? 'ogg' : 'webm';
      const recorder = new MediaRecorder(stream, { mimeType });
      const chunks: Blob[] = [];

      recorder.ondataavailable = (e) => {
        if (e.data.size > 0) chunks.push(e.data);
      };

      recorder.onstop = () => {
        stream.getTracks().forEach((t) => t.stop());
        if (recordingTimerRef.current) clearInterval(recordingTimerRef.current);
        setRecordingTime(0);

        const blob = new Blob(chunks, { type: mimeType });
        const audioFile = new File([blob], `audio-${Date.now()}.${ext}`, { type: mimeType });
        setFile(audioFile);
        setFilePreview(null);
        setRecording(false);
      };

      mediaRecorderRef.current = recorder;
      recorder.start();
      setRecording(true);
      setRecordingTime(0);
      recordingTimerRef.current = setInterval(() => setRecordingTime((t) => t + 1), 1000);
    } catch {
      toast.error('No se pudo acceder al microfono.');
    }
  }

  function stopRecording() {
    mediaRecorderRef.current?.stop();
  }

  function cancelRecording() {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      mediaRecorderRef.current.ondataavailable = null;
      mediaRecorderRef.current.onstop = null;
      mediaRecorderRef.current.stream.getTracks().forEach((t) => t.stop());
      mediaRecorderRef.current.stop();
    }
    if (recordingTimerRef.current) clearInterval(recordingTimerRef.current);
    mediaRecorderRef.current = null;
    setRecording(false);
    setRecordingTime(0);
  }

  function formatRecordingTime(seconds: number): string {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}:${s.toString().padStart(2, '0')}`;
  }

  async function handleSend() {
    const trimmed = content.trim();
    if ((!trimmed && !file) || sending) return;

    setSending(true);
    try {
      let msg: MessageResponse;

      if (file) {
        // Send media message
        msg = await sendMediaMessage(conversationId, file, trimmed || undefined);
        clearFile();
      } else {
        // Send text message
        msg = await sendMessage(conversationId, {
          content: trimmed,
          type: 'TEXT',
          isPrivateNote: isNote,
        });
      }

      onSent(msg);
      setContent('');
      setIsNote(false);
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

  function handleDrop(e: React.DragEvent) {
    e.preventDefault();
    const dropped = e.dataTransfer.files?.[0];
    if (!dropped) return;
    if (dropped.size > MAX_FILE_SIZE) {
      toast.error('El archivo es muy grande. Máximo 20 MB.');
      return;
    }
    setFile(dropped);
    if (dropped.type.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = (ev) => setFilePreview(ev.target?.result as string);
      reader.readAsDataURL(dropped);
    } else {
      setFilePreview(null);
    }
  }

  function handleDragOver(e: React.DragEvent) {
    e.preventDefault();
  }

  return (
    <div
      className={cn('border-t px-4 py-3', isNote ? 'border-blue-500/30 bg-blue-500/5' : 'border-zinc-800/60')}
      onDrop={handleDrop}
      onDragOver={handleDragOver}
    >
      {/* File preview */}
      {file && (
        <div className="mb-2 flex items-center gap-2 rounded-md border border-zinc-700/50 bg-zinc-900/50 px-3 py-2">
          {filePreview ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={filePreview} alt="" className="h-12 w-12 rounded object-cover" />
          ) : (
            <div className="flex h-12 w-12 items-center justify-center rounded bg-zinc-800 text-zinc-400">
              {fileIcon(file.type)}
            </div>
          )}
          <div className="min-w-0 flex-1">
            <p className="truncate text-xs text-zinc-300">{file.name}</p>
            <p className="text-[10px] text-zinc-500">{formatFileSize(file.size)}</p>
          </div>
          <button
            type="button"
            onClick={clearFile}
            className="rounded p-1 text-zinc-500 hover:bg-zinc-800 hover:text-zinc-300"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      )}

      {isNote && (
        <div className="mb-2 flex items-center gap-1.5 text-[10px] text-blue-400/70">
          <Lock className="h-2.5 w-2.5" />
          Nota privada — solo visible para el equipo
        </div>
      )}

      {/* Recording state */}
      {recording ? (
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={cancelRecording}
            title="Cancelar"
            className="grid h-8 w-8 shrink-0 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-red-400"
          >
            <X className="h-3.5 w-3.5" />
          </button>
          <div className="flex flex-1 items-center gap-2">
            <span className="h-2 w-2 animate-pulse rounded-full bg-red-500" />
            <span className="text-xs font-medium text-red-400">
              Grabando {formatRecordingTime(recordingTime)}
            </span>
          </div>
          <button
            type="button"
            onClick={stopRecording}
            title="Detener y adjuntar"
            className="grid h-8 w-8 place-items-center rounded-md bg-red-600 text-white transition-colors hover:bg-red-500"
          >
            <Square className="h-3 w-3" />
          </button>
        </div>
      ) : (
        <div className="flex items-end gap-2">
          {/* File attachment button */}
          {!isNote && (
            <>
              <input
                ref={fileInputRef}
                type="file"
                className="hidden"
                accept="image/*,video/*,audio/*,.pdf,.doc,.docx,.xls,.xlsx,.zip,.rar"
                onChange={handleFileSelect}
              />
              <button
                type="button"
                onClick={() => fileInputRef.current?.click()}
                title="Adjuntar archivo"
                className="grid h-8 w-8 shrink-0 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
              >
                <Paperclip className="h-3.5 w-3.5" />
              </button>
            </>
          )}

          <textarea
            ref={textareaRef}
            value={content}
            onChange={(e) => setContent(e.target.value)}
            onKeyDown={handleKeyDown}
            onInput={handleInput}
            placeholder={
              file
                ? 'Agregar un comentario (opcional)...'
                : isNote
                  ? 'Escribí una nota privada...'
                  : 'Escribí un mensaje...'
            }
            rows={1}
            className={cn(
              'flex-1 resize-none rounded-md border bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors',
              isNote
                ? 'border-blue-500/30 focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25'
                : 'border-zinc-800 focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25',
            )}
          />

          <div className="flex items-center gap-1">
            {/* Note toggle (only when no file attached) */}
            {!file && (
              <button
                type="button"
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
            )}

            {/* Mic button (only when no text/file and not in note mode) */}
            {!content.trim() && !file && !isNote && (
              <button
                type="button"
                onClick={startRecording}
                title="Grabar audio"
                className="grid h-8 w-8 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
              >
                <Mic className="h-3.5 w-3.5" />
              </button>
            )}

            {/* Send */}
            <button
              type="button"
              onClick={handleSend}
              disabled={(!content.trim() && !file) || sending}
              className="grid h-8 w-8 place-items-center rounded-md bg-blue-600 text-white transition-colors hover:bg-blue-500 disabled:opacity-40"
            >
              {sending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Send className="h-3.5 w-3.5" />}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
