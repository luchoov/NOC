// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect, useState } from 'react';
import { useAuthStore } from '@/lib/store/auth.store';

/**
 * Fetches a media URL with Authorization header and returns a blob URL.
 * HTML elements like <img>, <video>, <audio> can't send auth headers,
 * so we fetch manually and create an object URL.
 */
export function useMediaUrl(apiPath: string | null): string | null {
  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const token = useAuthStore((s) => s.accessToken);

  useEffect(() => {
    if (!apiPath) return;

    let cancelled = false;
    let objectUrl: string | null = null;

    (async () => {
      try {
        const res = await fetch(apiPath, {
          headers: token ? { Authorization: `Bearer ${token}` } : {},
        });
        if (!res.ok || cancelled) return;
        const blob = await res.blob();
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setBlobUrl(objectUrl);
      } catch {
        // silent
      }
    })();

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [apiPath, token]);

  return blobUrl;
}
