// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

export function parseTagInput(raw: string): string[] {
  return Array.from(
    new Set(
      raw
        .split(',')
        .map((tag) => tag.trim().toLowerCase())
        .filter(Boolean),
    ),
  );
}

export function stringifyCustomAttrs(customAttrs: Record<string, unknown>): string {
  if (Object.keys(customAttrs).length === 0) {
    return '{}';
  }

  return JSON.stringify(customAttrs, null, 2);
}

export function parseCustomAttrsInput(raw: string): Record<string, unknown> {
  const trimmed = raw.trim();
  if (!trimmed) {
    return {};
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(trimmed);
  } catch {
    throw new Error('Los atributos custom deben ser un JSON valido.');
  }

  if (!parsed || Array.isArray(parsed) || typeof parsed !== 'object') {
    throw new Error('Los atributos custom deben ser un objeto JSON.');
  }

  return parsed as Record<string, unknown>;
}
