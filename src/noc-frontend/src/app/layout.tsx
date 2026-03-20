// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import { Toaster } from 'sonner';
import { QueryProvider } from '@/lib/hooks/query-provider';
import './globals.css';

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: 'NOC — Neuryn Omnichannel',
  description: 'Plataforma de mensajería omnicanal',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="es" className="dark" suppressHydrationWarning>
      <body className={inter.className}>
        <QueryProvider>
          {children}
          <Toaster
            theme="dark"
            position="top-right"
            toastOptions={{
              style: {
                background: 'oklch(0.17 0.006 264.531)',
                border: '1px solid oklch(0.24 0.01 264.531)',
                color: 'oklch(0.94 0.006 264.531)',
              },
            }}
          />
        </QueryProvider>
      </body>
    </html>
  );
}
