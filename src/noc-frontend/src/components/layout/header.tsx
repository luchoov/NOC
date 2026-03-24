// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { usePathname } from 'next/navigation';

const TITLES: Record<string, string> = {
  '/inbox': 'Bandeja',
  '/contacts': 'Contactos',
  '/audiences': 'Audiencias',
  '/campaigns': 'Campañas',
  '/settings': 'Configuración',
  '/settings/inboxes': 'Bandejas',
  '/settings/proxies': 'Proxies',
  '/settings/agents': 'Agentes',
};

export function Header() {
  const path = usePathname();

  // Match longest prefix
  const title =
    Object.entries(TITLES)
      .filter(([k]) => path.startsWith(k))
      .sort((a, b) => b[0].length - a[0].length)[0]?.[1] || 'NOC';

  return (
    <header className="flex h-12 shrink-0 items-center border-b border-zinc-800/60 bg-zinc-950 px-5">
      <h1 className="text-sm font-medium text-zinc-300">{title}</h1>
    </header>
  );
}
