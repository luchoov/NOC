// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuthStore } from '@/lib/store/auth.store';
import { useUIStore } from '@/lib/store/ui.store';
import { useSignalR } from '@/lib/signalr/hooks';
import { Sidebar } from '@/components/layout/sidebar';
import { Header } from '@/components/layout/header';
import { ReconnectionBanner } from '@/components/layout/reconnection-banner';

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const isAuth = useAuthStore((s) => s.isAuthenticated);
  const collapsed = useUIStore((s) => s.sidebarCollapsed);
  const hubStatus = useSignalR();

  useEffect(() => {
    if (!isAuth) router.push('/login');
  }, [isAuth, router]);

  if (!isAuth) return null;

  return (
    <div className="flex h-screen overflow-hidden bg-zinc-950">
      <Sidebar />
      <div
        className="flex flex-1 flex-col overflow-hidden transition-[margin] duration-200"
        style={{ marginLeft: collapsed ? 56 : 224 }}
      >
        {hubStatus === 'reconnecting' && <ReconnectionBanner />}
        <Header />
        <main className="flex-1 overflow-auto">{children}</main>
      </div>
    </div>
  );
}
