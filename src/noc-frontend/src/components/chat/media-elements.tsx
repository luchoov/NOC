// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useMediaUrl } from '@/lib/hooks/use-media-url';
import { FileText, Download, Loader2 } from 'lucide-react';

function formatFileSize(bytes: number | null): string {
  if (!bytes) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function MediaLoader() {
  return (
    <div className="flex h-20 items-center justify-center">
      <Loader2 className="h-4 w-4 animate-spin text-zinc-600" />
    </div>
  );
}

// ── Image ───────────────────────────────────────────────────────────────

interface AuthImageProps {
  apiPath: string;
  alt?: string;
  className?: string;
  onClick?: () => void;
}

export function AuthImage({ apiPath, alt = '', className, onClick }: AuthImageProps) {
  const url = useMediaUrl(apiPath);
  if (!url) return <MediaLoader />;
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={url} alt={alt} className={className} onClick={onClick} loading="lazy" />
  );
}

// ── Video ───────────────────────────────────────────────────────────────

interface AuthVideoProps {
  apiPath: string;
  mimeType?: string | null;
  className?: string;
}

export function AuthVideo({ apiPath, mimeType, className }: AuthVideoProps) {
  const url = useMediaUrl(apiPath);
  if (!url) return <MediaLoader />;
  return (
    <video controls preload="metadata" className={className}>
      <source src={url} type={mimeType ?? 'video/mp4'} />
    </video>
  );
}

// ── Audio ───────────────────────────────────────────────────────────────

interface AuthAudioProps {
  apiPath: string;
  mimeType?: string | null;
}

export function AuthAudio({ apiPath, mimeType }: AuthAudioProps) {
  const url = useMediaUrl(apiPath);
  if (!url) return <MediaLoader />;
  return (
    <audio controls className="h-10 w-full min-w-[220px]" preload="none">
      <source src={url} type={mimeType ?? 'audio/ogg'} />
    </audio>
  );
}

// ── Document ────────────────────────────────────────────────────────────

interface AuthDocumentProps {
  apiPath: string;
  filename?: string | null;
  sizeBytes?: number | null;
}

export function AuthDocument({ apiPath, filename, sizeBytes }: AuthDocumentProps) {
  const url = useMediaUrl(apiPath);
  return (
    <a
      href={url ?? '#'}
      download={filename ?? 'document'}
      target="_blank"
      rel="noopener noreferrer"
      className="mb-1.5 flex items-center gap-2.5 rounded-md bg-zinc-700/30 px-3 py-2"
    >
      <FileText className="h-5 w-5 shrink-0 text-blue-400" />
      <div className="min-w-0 flex-1">
        <p className="truncate text-xs font-medium text-blue-400">
          {filename ?? 'Documento'}
        </p>
        {sizeBytes != null && sizeBytes > 0 && (
          <p className="text-[10px] text-zinc-500">{formatFileSize(sizeBytes)}</p>
        )}
      </div>
      <Download className="h-3.5 w-3.5 shrink-0 text-zinc-500" />
    </a>
  );
}
