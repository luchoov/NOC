// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod/v4';

export const createProxySchema = z.object({
  alias: z.string().min(1, 'Alias requerido').max(100),
  host: z.string().min(1, 'Host requerido'),
  port: z.coerce.number().int().min(1).max(65535),
  protocol: z.enum(['HTTP', 'HTTPS', 'SOCKS5']).default('HTTP'),
  username: z.string().optional(),
  password: z.string().optional(),
});

export type CreateProxyFormData = z.infer<typeof createProxySchema>;
