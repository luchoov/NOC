// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod/v4';

export const segmentRuleSchema = z.object({
  field: z.enum(['locality', 'tags', 'email']),
  operator: z.enum(['equals', 'contains', 'has_any_of', 'has_all_of', 'is_present', 'is_absent']),
  value: z.union([z.string(), z.array(z.string())]).optional(),
});

export const createSegmentSchema = z.object({
  name: z.string().min(1, 'Nombre requerido').max(200),
  description: z.string().max(500).optional(),
});

export type CreateSegmentFormData = z.infer<typeof createSegmentSchema>;
export type SegmentRuleFormData = z.infer<typeof segmentRuleSchema>;
