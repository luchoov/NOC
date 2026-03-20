// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { z } from 'zod/v4';

export const loginSchema = z.object({
  email: z.email('Email inválido'),
  password: z.string().min(1, 'La contraseña es requerida'),
});

export type LoginFormData = z.infer<typeof loginSchema>;
