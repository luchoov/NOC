// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { Users } from 'lucide-react';

export default function ContactsPage() {
  return (
    <div className="flex h-full items-center justify-center">
      <div className="text-center">
        <Users className="mx-auto h-8 w-8 text-zinc-700" />
        <p className="mt-2 text-sm text-zinc-500">
          Contactos se construirá en el Sprint 3
        </p>
      </div>
    </div>
  );
}
