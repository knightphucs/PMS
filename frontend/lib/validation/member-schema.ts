import { z } from 'zod';

/** Soi gương `InviteMemberRequestValidator.cs`. */
export const inviteMemberSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, 'Vui lòng nhập email của người cần mời.')
    .email('Email không đúng định dạng.')
    .max(256, 'Email tối đa 256 ký tự.'),
  role: z.enum(['ProjectManager', 'Member', 'Viewer']),
});

export type InviteMemberValues = z.infer<typeof inviteMemberSchema>;
