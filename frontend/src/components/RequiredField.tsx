import { useCallback, useRef, useState } from 'react'

/**
 * Shared required-field validation primitive.
 *
 * - `<RequiredLabel>` renders a label with a red asterisk so users always see which fields
 *   are required (independent of error state).
 * - `useRequiredValidation` tracks DOM refs for required fields, exposes a Set of currently-
 *   errored field names, validates on submit, auto-focuses the first missing field, and (on
 *   blur of an errored field that's now filled) advances focus to the next still-missing
 *   required field in declaration order.
 *
 * Usage:
 *   const v = useRequiredValidation(['firstName', 'lastName', 'dob'])
 *   <RequiredLabel>First name</RequiredLabel>
 *   <input
 *     ref={v.register('firstName')}
 *     onBlur={e => v.onFieldBlur('firstName', e.target.value)}
 *     className={`${baseCls} ${v.fieldCls('firstName')}`}
 *     value={firstName} onChange={...} />
 *   ...
 *   const submit = e => {
 *     e.preventDefault()
 *     if (!v.checkSubmit({ firstName, lastName, dob })) return
 *     // proceed with API call
 *   }
 */

/** Renders inner children + a red asterisk. Marks a field as required at-rest. */
export function RequiredLabel({
  children,
  className = 'font-medium text-slate-700',
}: {
  children: React.ReactNode
  className?: string
}) {
  return (
    <span className={className}>
      {children}
      <span aria-hidden="true" className="text-rose-600 ml-0.5">*</span>
      <span className="sr-only"> (required)</span>
    </span>
  )
}

/** Apply this class to an input that's currently in the error set — adds a rose border + ring. */
export const ERROR_CLS = 'border-rose-500 ring-2 ring-rose-200'

type FieldElement = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement | HTMLButtonElement | null

function isFilled(value: unknown): boolean {
  if (value === null || value === undefined) return false
  if (typeof value === 'string') return value.trim() !== ''
  if (typeof value === 'boolean') return value === true
  if (Array.isArray(value)) return value.length > 0
  return true
}

export function useRequiredValidation(required: readonly string[]) {
  const refs = useRef<Map<string, FieldElement>>(new Map())
  const [errors, setErrors] = useState<Set<string>>(new Set())

  const register = useCallback((name: string) => (el: FieldElement) => {
    refs.current.set(name, el)
  }, [])

  const focusName = (name: string) => {
    const el = refs.current.get(name)
    if (el && 'focus' in el) {
      try { (el as HTMLElement).focus() } catch { /* element may have unmounted */ }
    }
  }

  const computeMissing = (values: Record<string, unknown>): string[] =>
    required.filter(name => !isFilled(values[name]))

  /** Validate on submit: stash errors, focus first missing in declaration order. */
  const checkSubmit = (values: Record<string, unknown>): boolean => {
    const missing = computeMissing(values)
    setErrors(new Set(missing))
    if (missing.length === 0) return true
    const first = required.find(n => missing.includes(n))
    if (first) setTimeout(() => focusName(first), 0)
    return false
  }

  /** On blur of a field: if the field has a value now, clear its error and advance focus to
   *  the next still-errored required field (in declaration order, wrapping around). */
  const onFieldBlur = (name: string, value: unknown) => {
    if (!isFilled(value)) return
    setErrors(prev => {
      if (!prev.has(name)) return prev
      const next = new Set(prev)
      next.delete(name)
      if (next.size > 0) {
        const idx = required.indexOf(name)
        const after = required.slice(idx + 1).find(n => next.has(n))
        const wrap = required.find(n => next.has(n))
        const target = after ?? wrap
        if (target) setTimeout(() => focusName(target), 0)
      }
      return next
    })
  }

  const fieldCls = (name: string): string => (errors.has(name) ? ERROR_CLS : '')

  /** Force the error set externally (e.g., for cross-field rules). */
  const setFieldErrors = (names: readonly string[]) => setErrors(new Set(names))

  /** Clear all errors (e.g., on modal open). */
  const reset = () => setErrors(new Set())

  return { register, errors, checkSubmit, onFieldBlur, fieldCls, focus: focusName, setFieldErrors, reset }
}
