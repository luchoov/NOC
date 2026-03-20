// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { Construction } from 'lucide-react';

export function PlaceholderPage({
  title,
  description,
}: {
  title: string;
  description?: string;
}) {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-3 text-center">
      <Construction className="h-8 w-8 text-zinc-600" />
      <div>
        <h2 className="text-sm font-medium text-zinc-300">{title}</h2>
        <p className="mt-1 text-xs text-zinc-600">
          {description || 'Esta sección estará disponible próximamente.'}
        </p>
      </div>
    </div>
  );
}
