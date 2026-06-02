import type { Language, WhatsAppTemplate } from './types'

/**
 * Picks the highest-versioned WhatsApp template matching the convention
 *   `{baseName}_{english|spanish|en|es}`              (treated as v1)
 *   `{baseName}v{N}_{english|spanish|en|es}`          (v2, v3, …)
 * An optional underscore before the v-token is also accepted, so both
 * `monthlyfeev2_english` and `monthlyfee_v2_english` work.
 *
 * Mirror of `MessagingController.FindLatestVersionedTemplateAsync` on the backend — when admin
 * uploads a v2 of an existing template, the next pick uses it automatically without a code
 * change.
 */
export function pickLatestTemplate(
  baseName: string,
  language: Language,
  templates: WhatsAppTemplate[],
): WhatsAppTemplate | null {
  const suffixes = language === 1 ? ['_spanish', '_es'] : ['_english', '_en']
  // Allow letters/numbers/underscores in baseName; pattern is admin-defined so escape it.
  const escaped = baseName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const rx = new RegExp(`^${escaped}(?:_?v(\\d+))?(${suffixes.join('|')})$`, 'i')
  let best: { t: WhatsAppTemplate; v: number } | null = null
  for (const t of templates) {
    if (t.language !== language) continue
    const m = rx.exec(t.name)
    if (!m) continue
    const v = m[1] ? parseInt(m[1], 10) : 1
    if (!best || v > best.v) best = { t, v }
  }
  return best?.t ?? null
}
