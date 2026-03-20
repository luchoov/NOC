// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { useAuthStore } from '@/lib/store/auth.store';

// Use empty string for relative URLs — Next.js rewrites proxy to backend
const API_BASE_URL = '';

export interface ApiError {
  status: number;
  message: string;
  detail?: string;
}

class ApiClient {
  private baseUrl: string;
  private refreshPromise: Promise<boolean> | null = null;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  private headers(): HeadersInit {
    const h: Record<string, string> = {
      'Content-Type': 'application/json',
      'X-Correlation-Id': crypto.randomUUID(),
    };
    const token = useAuthStore.getState().accessToken;
    if (token) h['Authorization'] = `Bearer ${token}`;
    return h;
  }

  private async tryRefresh(): Promise<boolean> {
    if (this.refreshPromise) return this.refreshPromise;

    this.refreshPromise = (async () => {
      try {
        const { refreshToken } = useAuthStore.getState();
        if (!refreshToken) return false;

        const res = await fetch(`${this.baseUrl}/api/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken }),
        });

        if (!res.ok) return false;

        const data = await res.json();
        useAuthStore
          .getState()
          .setTokens(data.accessToken, data.refreshToken, data.expiresAt);
        return true;
      } catch {
        return false;
      } finally {
        this.refreshPromise = null;
      }
    })();

    return this.refreshPromise;
  }

  private async request<T>(
    method: string,
    path: string,
    body?: unknown,
  ): Promise<T> {
    const url = `${this.baseUrl}${path}`;

    let res = await fetch(url, {
      method,
      headers: this.headers(),
      body: body ? JSON.stringify(body) : undefined,
    });

    if (res.status === 401) {
      const ok = await this.tryRefresh();
      if (ok) {
        res = await fetch(url, {
          method,
          headers: this.headers(),
          body: body ? JSON.stringify(body) : undefined,
        });
      } else {
        useAuthStore.getState().clearAuth();
        if (typeof window !== 'undefined') window.location.href = '/login';
        throw { status: 401, message: 'Session expired' } satisfies ApiError;
      }
    }

    if (!res.ok) {
      const err: ApiError = { status: res.status, message: res.statusText };
      try {
        const body = await res.json();
        err.detail = body.detail || body.message || JSON.stringify(body);
      } catch {
        /* empty */
      }
      throw err;
    }

    if (res.status === 204) return undefined as T;
    return res.json();
  }

  get<T>(path: string) {
    return this.request<T>('GET', path);
  }
  post<T>(path: string, body?: unknown) {
    return this.request<T>('POST', path, body);
  }
  put<T>(path: string, body?: unknown) {
    return this.request<T>('PUT', path, body);
  }
  del<T>(path: string) {
    return this.request<T>('DELETE', path);
  }

  async upload<T>(path: string, file: File, fieldName = 'file'): Promise<T> {
    const form = new FormData();
    form.append(fieldName, file);

    const h: Record<string, string> = {
      'X-Correlation-Id': crypto.randomUUID(),
    };
    const token = useAuthStore.getState().accessToken;
    if (token) h['Authorization'] = `Bearer ${token}`;

    const res = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: h,
      body: form,
    });

    if (!res.ok) {
      throw {
        status: res.status,
        message: res.statusText,
      } satisfies ApiError;
    }
    return res.json();
  }
}

export const api = new ApiClient(API_BASE_URL);

export function qs(params: object): string {
  const s = new URLSearchParams();
  for (const [k, v] of Object.entries(params as Record<string, unknown>)) {
    if (v !== undefined && v !== null && v !== '') s.append(k, String(v));
  }
  const str = s.toString();
  return str ? `?${str}` : '';
}
