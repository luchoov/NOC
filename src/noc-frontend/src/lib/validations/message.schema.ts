// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod/v4';

export const sendMessageSchema = z.object({
  content: z.string().min(1, 'Mensaje vacío').max(4096),
  isPrivateNote: z.boolean().default(false),
});

export type SendMessageFormData = z.infer<typeof sendMessageSchema>;
