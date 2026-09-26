import type { Me } from '../api/types';

/**
 * True when the login has at least one of the given permissions (keys from backend
 * Auth/Permissions.cs). For showing and hiding UI only; the server checks every request itself.
 * Servers from before permissions existed don't send the list, so fall back to the admin flag.
 */
export function can(me: Me | null | undefined, ...anyOf: string[]): boolean {
  if (!me) return false;
  if (!me.permissions) return me.isAdmin;
  return anyOf.some((p) => me.permissions!.includes(p));
}

export const Perm = {
  AdminAccess: 'admin.access',
  EventsCreate: 'events.create',
  RolesManage: 'roles.manage',
  UsersManage: 'users.manage',
} as const;
