// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod/v4';

export const createContactSchema = z.object({
  phone: z.string().min(8, 'Teléfono inválido').max(20),
  name: z.string().max(150).optional(),
  email: z.union([z.email(), z.literal('')]).optional(),
  tags: z.array(z.string().max(50)).optional(),
});

export const updateContactSchema = z.object({
  name: z.string().max(150).optional(),
  email: z.union([z.email(), z.literal('')]).optional(),
});

export type CreateContactFormData = z.infer<typeof createContactSchema>;
export type UpdateContactFormData = z.infer<typeof updateContactSchema>;
