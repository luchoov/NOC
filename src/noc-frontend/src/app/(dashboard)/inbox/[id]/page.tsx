// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';

// Deep-link to a specific conversation redirects to inbox.
// Conversation selection is managed via client-side state.
export default function ConversationPage() {
  const router = useRouter();

  useEffect(() => {
    router.replace('/inbox');
  }, [router]);

  return null;
}
