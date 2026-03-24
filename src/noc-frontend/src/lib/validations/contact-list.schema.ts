// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod/v4';

export const createContactListSchema = z.object({
  name: z.string().min(1, 'Nombre requerido').max(200),
  description: z.string().max(500).optional(),
});

export type CreateContactListFormData = z.infer<typeof createContactListSchema>;
