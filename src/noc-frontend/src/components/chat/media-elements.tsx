// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect, useRef, useState } from 'react';
import { useMediaUrl } from '@/lib/hooks/use-media-url';
import { FileText, Download, Loader2, Play, Pause } from 'lucide-react';

function formatFileSize(bytes: number | null): string {
  if (!bytes) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDuration(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

function MediaLoader() {
  return (
    <div className="flex h-20 w-48 items-center justify-center rounded-lg bg-zinc-800/30">
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
  const [loaded, setLoaded] = useState(false);

  if (!url) return <MediaLoader />;
  return (
    <div className="relative">
      {!loaded && (
        <div className="absolute inset-0 flex items-center justify-center rounded-lg bg-zinc-800/30">
          <Loader2 className="h-4 w-4 animate-spin text-zinc-600" />
        </div>
      )}
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        src={url}
        alt={alt}
        className={`${className ?? ''} ${loaded ? 'opacity-100' : 'opacity-0'} transition-opacity duration-200 rounded-lg`}
        onClick={onClick}
        onLoad={() => setLoaded(true)}
        loading="lazy"
      />
    </div>
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
    <video controls preload="metadata" className={`${className ?? ''} rounded-lg`}>
      <source src={url} type={mimeType ?? 'video/mp4'} />
    </video>
  );
}

// ── Audio ───────────────────────────────────────────────────────────────

interface AuthAudioProps {
  apiPath: string;
  mimeType?: string | null;
  isOutbound?: boolean;
}

export function AuthAudio({ apiPath, mimeType, isOutbound }: AuthAudioProps) {
  const url = useMediaUrl(apiPath);
  const audioRef = useRef<HTMLAudioElement>(null);
  const [playing, setPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio || !url) return;

    // When the blob URL changes, force the audio element to reload
    audio.src = url;
    audio.load();

    const onTimeUpdate = () => setCurrentTime(audio.currentTime);
    const onLoadedMetadata = () => { setDuration(audio.duration); setLoaded(true); };
    const onEnded = () => { setPlaying(false); setCurrentTime(0); };
    const onDurationChange = () => { if (audio.duration && isFinite(audio.duration)) setDuration(audio.duration); };
    const onError = () => { console.warn('[AuthAudio] audio load error', audio.error); };

    audio.addEventListener('timeupdate', onTimeUpdate);
    audio.addEventListener('loadedmetadata', onLoadedMetadata);
    audio.addEventListener('durationchange', onDurationChange);
    audio.addEventListener('ended', onEnded);
    audio.addEventListener('error', onError);

    return () => {
      audio.removeEventListener('timeupdate', onTimeUpdate);
      audio.removeEventListener('loadedmetadata', onLoadedMetadata);
      audio.removeEventListener('durationchange', onDurationChange);
      audio.removeEventListener('ended', onEnded);
      audio.removeEventListener('error', onError);
    };
  }, [url]);

  if (!url) return <MediaLoader />;

  const progress = duration > 0 ? (currentTime / duration) * 100 : 0;

  function togglePlay() {
    const audio = audioRef.current;
    if (!audio) return;
    if (playing) {
      audio.pause();
      setPlaying(false);
    } else {
      audio.play().then(() => setPlaying(true)).catch(() => setPlaying(false));
    }
  }

  function handleSeek(e: React.MouseEvent<HTMLDivElement>) {
    const audio = audioRef.current;
    if (!audio || !duration) return;
    const rect = e.currentTarget.getBoundingClientRect();
    const ratio = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
    audio.currentTime = ratio * duration;
    setCurrentTime(audio.currentTime);
  }

  return (
    <div className="flex items-center gap-2.5 min-w-[220px] max-w-[280px]">
      {/* src is set imperatively in useEffect so .load() is called on url change */}
      <audio ref={audioRef} preload="metadata" />

      {/* Play/Pause button */}
      <button
        type="button"
        onClick={togglePlay}
        className={`grid h-9 w-9 shrink-0 place-items-center rounded-full transition-colors ${
          isOutbound
            ? 'bg-blue-500/30 text-blue-300 hover:bg-blue-500/40'
            : 'bg-zinc-600/40 text-zinc-200 hover:bg-zinc-600/60'
        }`}
      >
        {playing
          ? <Pause className="h-3.5 w-3.5" />
          : <Play className="h-3.5 w-3.5 ml-0.5" />}
      </button>

      {/* Waveform / progress */}
      <div className="flex-1 min-w-0">
        <div
          className="group relative h-6 cursor-pointer flex items-center"
          onClick={handleSeek}
        >
          {/* Background bars (fake waveform) */}
          <div className="flex w-full items-center gap-[2px] h-5">
            {Array.from({ length: 28 }).map((_, i) => {
              const height = [3, 5, 8, 12, 6, 10, 14, 8, 4, 11, 16, 9, 5, 13, 7, 10, 15, 6, 11, 4, 8, 14, 9, 6, 12, 7, 10, 5][i % 28];
              const barProgress = (i / 28) * 100;
              const isActive = barProgress <= progress;
              return (
                <div
                  key={i}
                  className={`w-[3px] rounded-full transition-colors ${
                    isActive
                      ? isOutbound ? 'bg-blue-400' : 'bg-zinc-300'
                      : isOutbound ? 'bg-blue-500/20' : 'bg-zinc-600/50'
                  }`}
                  style={{ height: `${height}px` }}
                />
              );
            })}
          </div>
        </div>
        <div className="flex justify-between mt-0.5">
          <span className={`text-[10px] ${isOutbound ? 'text-blue-400/60' : 'text-zinc-500'}`}>
            {playing || currentTime > 0 ? formatDuration(currentTime) : loaded && duration > 0 ? formatDuration(duration) : '--:--'}
          </span>
        </div>
      </div>
    </div>
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
      className="mb-1.5 flex items-center gap-2.5 rounded-lg bg-zinc-700/20 px-3 py-2.5 transition-colors hover:bg-zinc-700/30"
    >
      <div className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-blue-500/15">
        <FileText className="h-4 w-4 text-blue-400" />
      </div>
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
