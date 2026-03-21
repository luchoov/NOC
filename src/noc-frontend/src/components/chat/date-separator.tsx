// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { format, parseISO, isToday, isYesterday } from 'date-fns';
import { es } from 'date-fns/locale';

interface DateSeparatorProps {
  date: string;
}

export function DateSeparator({ date }: DateSeparatorProps) {
  const parsed = parseISO(date);
  let label: string;

  if (isToday(parsed)) {
    label = 'Hoy';
  } else if (isYesterday(parsed)) {
    label = 'Ayer';
  } else {
    label = format(parsed, "d 'de' MMMM yyyy", { locale: es });
  }

  return (
    <div className="flex items-center gap-3 py-3">
      <div className="h-px flex-1 bg-zinc-800/60" />
      <span className="shrink-0 text-[10px] font-medium text-zinc-500">{label}</span>
      <div className="h-px flex-1 bg-zinc-800/60" />
    </div>
  );
}
