// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { formatDistanceToNow, format, parseISO } from 'date-fns';
import { es } from 'date-fns/locale';

export function timeAgo(iso: string): string {
  return formatDistanceToNow(parseISO(iso), { addSuffix: true, locale: es });
}

export function formatFull(iso: string): string {
  return format(parseISO(iso), "d 'de' MMMM yyyy, HH:mm", { locale: es });
}

export function formatShort(iso: string): string {
  return format(parseISO(iso), 'dd/MM/yy HH:mm');
}

export function formatTime(iso: string): string {
  return format(parseISO(iso), 'HH:mm');
}
