// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { Radio, Globe, Users } from 'lucide-react';
import { cn } from '@/lib/utils';

const TABS = [
  { href: '/settings/inboxes', label: 'Bandejas', icon: Radio },
  { href: '/settings/proxies', label: 'Proxies', icon: Globe },
  { href: '/settings/agents', label: 'Agentes', icon: Users },
] as const;

export default function SettingsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const path = usePathname();

  return (
    <div className="flex h-full flex-col">
      <div className="border-b border-zinc-800/60 px-6 pt-1">
        <nav className="flex gap-1">
          {TABS.map(({ href, label, icon: Icon }) => {
            const active = path.startsWith(href);
            return (
              <Link
                key={href}
                href={href}
                className={cn(
                  'flex items-center gap-2 border-b-2 px-3 py-2.5 text-[13px] font-medium transition-colors',
                  active
                    ? 'border-blue-500 text-blue-400'
                    : 'border-transparent text-zinc-500 hover:text-zinc-300',
                )}
              >
                <Icon className="h-3.5 w-3.5" />
                {label}
              </Link>
            );
          })}
        </nav>
      </div>
      <div className="flex-1 overflow-auto">{children}</div>
    </div>
  );
}
