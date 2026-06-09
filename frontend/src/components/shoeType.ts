import type { ShoeType } from '../api/types'

/** All shoe-type values, in display order (Unspecified first as the "Not set" option). */
export const SHOE_TYPES: ShoeType[] = [0, 1, 2, 3]

/** i18n key for a shoe-type value. */
export function shoeTypeKey(s: ShoeType): string {
  return ['admin.shoeUnspecified', 'admin.shoeCleats', 'admin.shoeTurf', 'admin.shoeTennis'][s]
}
