// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod/v4';

export const createInboxSchema = z.object({
  name: z.string().min(1, 'Nombre requerido').max(100),
  channelType: z.enum(['WHATSAPP_OFFICIAL', 'WHATSAPP_UNOFFICIAL']),
  phoneNumber: z.string().min(8).max(20),
  evolutionInstanceName: z.string().max(100).optional(),
  autoProvisionEvolution: z.boolean().default(true),
  autoConnectEvolution: z.boolean().default(true),
});

export type CreateInboxFormData = z.infer<typeof createInboxSchema>;
