// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import {
  Inbox,
  Users,
  Megaphone,
  Settings,
  LogOut,
  PanelLeftClose,
  PanelLeft,
} from 'lucide-react';
import { useAuthStore } from '@/lib/store/auth.store';
import { useUIStore } from '@/lib/store/ui.store';
import { logout as apiLogout } from '@/lib/api/auth';
import { cn } from '@/lib/utils';

const NAV = [
  { href: '/inbox', label: 'Bandeja', icon: Inbox },
  { href: '/contacts', label: 'Contactos', icon: Users },
  { href: '/campaigns', label: 'Campañas', icon: Megaphone },
  { href: '/settings', label: 'Configuración', icon: Settings },
] as const;

export function Sidebar() {
  const path = usePathname();
  const router = useRouter();
  const { agent, clearAuth } = useAuthStore();
  const { sidebarCollapsed, toggleSidebar } = useUIStore();

  async function handleLogout() {
    try {
      await apiLogout();
    } catch {
      /* ignore */
    }
    clearAuth();
    router.push('/login');
  }

  const collapsed = sidebarCollapsed;

  return (
    <aside
      className={cn(
        'fixed inset-y-0 left-0 z-40 flex flex-col border-r border-zinc-800/60 bg-zinc-950 transition-[width] duration-200',
        collapsed ? 'w-14' : 'w-56',
      )}
    >
      {/* Header */}
      <div className="flex h-12 items-center justify-between px-3">
        {!collapsed && (
          <span className="text-sm font-semibold tracking-tight text-blue-400">
            neuryn
          </span>
        )}
        <button
          onClick={toggleSidebar}
          className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
        >
          {collapsed ? (
            <PanelLeft className="h-4 w-4" />
          ) : (
            <PanelLeftClose className="h-4 w-4" />
          )}
        </button>
      </div>

      {/* Nav */}
      <nav className="flex-1 space-y-0.5 px-2 pt-2">
        {NAV.map(({ href, label, icon: Icon }) => {
          const active = path.startsWith(href);
          return (
            <Link
              key={href}
              href={href}
              className={cn(
                'flex items-center gap-2.5 rounded-md px-2.5 py-1.5 text-[13px] font-medium transition-colors',
                active
                  ? 'bg-blue-500/10 text-blue-400'
                  : 'text-zinc-500 hover:bg-zinc-800/60 hover:text-zinc-300',
              )}
            >
              <Icon className="h-4 w-4 shrink-0" />
              {!collapsed && <span>{label}</span>}
            </Link>
          );
        })}
      </nav>

      {/* Footer — agent info */}
      <div className="border-t border-zinc-800/60 p-2">
        <div
          className={cn(
            'flex items-center gap-2.5 rounded-md px-2.5 py-1.5',
            collapsed && 'justify-center px-0',
          )}
        >
          <div className="grid h-7 w-7 shrink-0 place-items-center rounded-md bg-blue-500/15 text-xs font-medium text-blue-400">
            {agent?.name?.charAt(0).toUpperCase() || '?'}
          </div>
          {!collapsed && (
            <div className="min-w-0 flex-1">
              <p className="truncate text-[13px] font-medium text-zinc-300">
                {agent?.name}
              </p>
              <p className="truncate text-[11px] text-zinc-600">
                {agent?.role}
              </p>
            </div>
          )}
        </div>
        <button
          onClick={handleLogout}
          className={cn(
            'mt-1 flex w-full items-center gap-2.5 rounded-md px-2.5 py-1.5 text-[13px] text-zinc-600 transition-colors hover:bg-zinc-800/60 hover:text-red-400',
            collapsed && 'justify-center px-0',
          )}
        >
          <LogOut className="h-4 w-4 shrink-0" />
          {!collapsed && <span>Salir</span>}
        </button>
      </div>
    </aside>
  );
}
