import type { Me } from '../api/types'

/** True when the signed-in user holds any of the given permission keys (backend
 *  Auth/Permissions.cs, delivered in `me.permissions`). For showing/hiding UI only — the server
 *  enforces every permission itself. */
export function can(me: Me | null | undefined, ...anyOf: string[]): boolean {
  return !!me && anyOf.some(p => me.permissions?.includes(p))
}
