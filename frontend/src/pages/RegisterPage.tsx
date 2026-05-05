import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useForm, useFieldArray, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Layout } from '../components/Layout'
import { SignaturePad } from '../components/SignaturePad'
import { Api } from '../api/client'
import type { InvitationLookupResponse, SubmitRegistrationRequest } from '../api/types'

const playerSchema = z.object({
  firstName: z.string().min(1),
  lastName: z.string().min(1),
  dateOfBirth: z.string().min(1),
  schoolGrade: z.string().min(1),
  shirtSize: z.string().min(1),
  shortSize: z.string().min(1),
  shoeSize: z.string().min(1),
  heardFrom: z.string().optional(),
  waiverParticipantName: z.string().min(1),
  waiverTeamName: z.string().optional(),
  waiverParentGuardianName: z.string().min(1),
  waiverPhone: z.string().min(7),
  waiverEmail: z.string().email(),
  signatureDataUrl: z.string().refine(v => v.startsWith('data:image/'), { message: 'signature required' }),
})

const formSchema = z.object({
  parentFirstName: z.string().min(1),
  parentLastName: z.string().min(1),
  addressLine1: z.string().min(1),
  addressLine2: z.string().optional(),
  city: z.string().min(1),
  state: z.string().min(1),
  postalCode: z.string().min(1),
  cellPhone: z.string().min(7),
  email: z.string().email(),
  waiverConsent: z.boolean().refine(v => v === true, { message: 'required' }),
  players: z.array(playerSchema).min(1),
})

type FormValues = z.infer<typeof formSchema>

const SHIRT_SIZES = ['YXS', 'YS', 'YM', 'YL', 'YXL', 'AS', 'AM', 'AL', 'AXL', 'A2XL']
const GRADES = ['Pre-K', 'K', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12']

const emptyPlayer = {
  firstName: '',
  lastName: '',
  dateOfBirth: '',
  schoolGrade: '',
  shirtSize: '',
  shortSize: '',
  shoeSize: '',
  heardFrom: '',
  waiverParticipantName: '',
  waiverTeamName: '',
  waiverParentGuardianName: '',
  waiverPhone: '',
  waiverEmail: '',
  signatureDataUrl: '',
}

export function RegisterPage() {
  const { token = '' } = useParams()
  const { t, i18n } = useTranslation()

  const [invite, setInvite] = useState<InvitationLookupResponse | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [submitState, setSubmitState] = useState<'idle' | 'submitting' | 'success' | 'error'>('idle')
  const [submitError, setSubmitError] = useState<string | null>(null)

  const form = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      parentFirstName: '',
      parentLastName: '',
      addressLine1: '',
      addressLine2: '',
      city: '',
      state: 'NV',
      postalCode: '',
      cellPhone: '',
      email: '',
      waiverConsent: false,
      players: [emptyPlayer],
    },
  })

  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'players' })

  useEffect(() => {
    let active = true
    Api.lookupInvite(token)
      .then(data => {
        if (!active) return
        setInvite(data)
        const lang = data.language === 1 ? 'es' : 'en'
        if (i18n.resolvedLanguage !== lang) i18n.changeLanguage(lang)
        if (data.email) form.setValue('email', data.email)
        if (data.phone) form.setValue('cellPhone', data.phone)
      })
      .catch(() => {
        if (active) setLoadError(t('register.invalidLink'))
      })
    return () => { active = false }
  }, [token])

  // Sync prepopulated waiver fields whenever parent info or player name/DOB change.
  // Watching specific fields and updating per-player waiver fields if they're still
  // matching the previous parent value (i.e. user hasn't customized them).
  const watch = form.watch
  const parentFirst = watch('parentFirstName')
  const parentLast = watch('parentLastName')
  const parentPhone = watch('cellPhone')
  const parentEmail = watch('email')
  const players = watch('players')

  useEffect(() => {
    players.forEach((p, idx) => {
      const fullPlayerName = `${p.firstName} ${p.lastName}`.trim()
      const fullParentName = `${parentFirst} ${parentLast}`.trim()
      if (!p.waiverParticipantName || isPlayerNameDerived(p.waiverParticipantName, players, idx)) {
        if (p.waiverParticipantName !== fullPlayerName) form.setValue(`players.${idx}.waiverParticipantName`, fullPlayerName)
      }
      if (!p.waiverParentGuardianName || isParentNameDerived(p.waiverParentGuardianName)) {
        if (p.waiverParentGuardianName !== fullParentName) form.setValue(`players.${idx}.waiverParentGuardianName`, fullParentName)
      }
      if (!p.waiverPhone) form.setValue(`players.${idx}.waiverPhone`, parentPhone)
      if (!p.waiverEmail) form.setValue(`players.${idx}.waiverEmail`, parentEmail)
    })
  }, [parentFirst, parentLast, parentPhone, parentEmail, JSON.stringify(players.map(p => `${p.firstName}|${p.lastName}`))])

  const onSubmit = async (values: FormValues) => {
    setSubmitState('submitting')
    setSubmitError(null)
    const payload: SubmitRegistrationRequest = {
      token,
      parentFirstName: values.parentFirstName,
      parentLastName: values.parentLastName,
      addressLine1: values.addressLine1,
      addressLine2: values.addressLine2,
      city: values.city,
      state: values.state,
      postalCode: values.postalCode,
      cellPhone: values.cellPhone,
      email: values.email,
      language: i18n.resolvedLanguage?.startsWith('es') ? 1 : 0,
      waiverConsent: values.waiverConsent,
      players: values.players.map(p => ({
        firstName: p.firstName,
        lastName: p.lastName,
        dateOfBirth: p.dateOfBirth,
        schoolGrade: p.schoolGrade,
        shirtSize: p.shirtSize,
        shortSize: p.shortSize,
        shoeSize: p.shoeSize,
        heardFrom: p.heardFrom,
        waiverParticipantName: p.waiverParticipantName,
        waiverTeamName: p.waiverTeamName,
        waiverParentGuardianName: p.waiverParentGuardianName,
        waiverPhone: p.waiverPhone,
        waiverEmail: p.waiverEmail,
        signatureDataUrl: p.signatureDataUrl,
      })),
    }
    try {
      await Api.submitRegistration(payload)
      setSubmitState('success')
    } catch (e: any) {
      setSubmitState('error')
      const msg = e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
      setSubmitError(typeof msg === 'string' ? msg : t('common.error'))
    }
  }

  if (loadError) {
    return (
      <Layout>
        <div className="max-w-2xl mx-auto px-4 py-16 text-center">
          <h1 className="text-2xl font-bold text-rose-700">{loadError}</h1>
        </div>
      </Layout>
    )
  }

  if (invite?.alreadyRegistered) {
    return (
      <Layout>
        <div className="max-w-2xl mx-auto px-4 py-16 text-center">
          <h1 className="text-2xl font-bold text-emerald-800">{t('register.alreadyUsed')}</h1>
        </div>
      </Layout>
    )
  }

  if (submitState === 'success') {
    return (
      <Layout>
        <div className="max-w-2xl mx-auto px-4 py-16 text-center">
          <div className="text-5xl mb-4">✅</div>
          <h1 className="text-3xl font-bold text-emerald-800">{t('register.successTitle')}</h1>
          <p className="mt-3 text-slate-600">{t('register.successBody')}</p>
        </div>
      </Layout>
    )
  }

  if (!invite) {
    return (
      <Layout>
        <div className="max-w-2xl mx-auto px-4 py-16 text-center text-slate-500">{t('common.loading')}</div>
      </Layout>
    )
  }

  return (
    <Layout>
      <div className="max-w-3xl mx-auto px-4 py-10">
        <h1 className="text-3xl font-bold text-emerald-800">{t('register.title')}</h1>
        <p className="text-slate-600 mt-1">{t('register.subtitle')}</p>

        <form onSubmit={form.handleSubmit(onSubmit)} className="mt-8 space-y-8">
          <ParentSection form={form} />

          <PlayersSection
            fields={fields}
            form={form}
            onAdd={() => append(emptyPlayer)}
            onRemove={(i) => remove(i)}
          />

          <div className="bg-white border border-slate-200 rounded-lg p-6">
            <label className="flex items-start gap-3">
              <input
                type="checkbox"
                className="mt-1 w-5 h-5"
                {...form.register('waiverConsent')}
              />
              <span className="text-slate-700">{t('register.overallConsent')}</span>
            </label>
            {form.formState.errors.waiverConsent && (
              <p className="text-rose-600 text-xs mt-2">{t('common.required')}</p>
            )}
          </div>

          {submitError && (
            <div className="bg-rose-50 border border-rose-200 text-rose-800 rounded-md p-3 text-sm">
              {submitError}
            </div>
          )}

          <button
            type="submit"
            disabled={submitState === 'submitting'}
            className="w-full sm:w-auto bg-emerald-700 text-white font-semibold px-6 py-3 rounded-md hover:bg-emerald-800 disabled:opacity-60"
          >
            {submitState === 'submitting' ? t('register.submitting') : t('register.submit')}
          </button>
        </form>
      </div>
    </Layout>
  )
}

// Heuristic: is `value` derived from one of the player names in `players`?
function isPlayerNameDerived(value: string, players: any[], skipIdx: number) {
  const norm = value.trim().toLowerCase()
  return players.some((p, i) => i !== skipIdx && norm === `${p.firstName} ${p.lastName}`.trim().toLowerCase())
}
function isParentNameDerived(_value: string) {
  // We don't know the previous parent name reliably; only auto-fill when empty.
  return false
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <fieldset className="bg-white border border-slate-200 rounded-lg p-6">
      <legend className="px-2 text-emerald-800 font-bold">{title}</legend>
      <div className="grid sm:grid-cols-2 gap-4 mt-2">{children}</div>
    </fieldset>
  )
}

function Field({
  label,
  error,
  children,
  span = 1,
}: {
  label: string
  error?: string
  children: React.ReactNode
  span?: 1 | 2
}) {
  return (
    <label className={`flex flex-col text-sm ${span === 2 ? 'sm:col-span-2' : ''}`}>
      <span className="text-slate-700 font-medium mb-1">{label}</span>
      {children}
      {error && <span className="text-rose-600 text-xs mt-1">{error}</span>}
    </label>
  )
}

const inputCls =
  'border border-slate-300 rounded-md px-3 py-2 text-base focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500'

function ParentSection({ form }: { form: any }) {
  const { t } = useTranslation()
  const errors = form.formState.errors
  return (
    <Section title={t('register.parent.heading')}>
      <Field label={t('register.parent.firstName')} error={errors.parentFirstName && t('common.required')}>
        <input className={inputCls} {...form.register('parentFirstName')} />
      </Field>
      <Field label={t('register.parent.lastName')} error={errors.parentLastName && t('common.required')}>
        <input className={inputCls} {...form.register('parentLastName')} />
      </Field>
      <Field label={t('register.parent.address1')} error={errors.addressLine1 && t('common.required')} span={2}>
        <input className={inputCls} {...form.register('addressLine1')} />
      </Field>
      <Field label={t('register.parent.address2')} span={2}>
        <input className={inputCls} {...form.register('addressLine2')} />
      </Field>
      <Field label={t('register.parent.city')} error={errors.city && t('common.required')}>
        <input className={inputCls} {...form.register('city')} />
      </Field>
      <Field label={t('register.parent.state')} error={errors.state && t('common.required')}>
        <input className={inputCls} {...form.register('state')} />
      </Field>
      <Field label={t('register.parent.postal')} error={errors.postalCode && t('common.required')}>
        <input className={inputCls} {...form.register('postalCode')} />
      </Field>
      <Field label={t('register.parent.cell')} error={errors.cellPhone && t('common.required')}>
        <input type="tel" className={inputCls} {...form.register('cellPhone')} />
      </Field>
      <Field label={t('register.parent.email')} error={errors.email && t('common.required')} span={2}>
        <input type="email" className={inputCls} {...form.register('email')} />
      </Field>
    </Section>
  )
}

function PlayersSection({
  fields,
  form,
  onAdd,
  onRemove,
}: {
  fields: any[]
  form: any
  onAdd: () => void
  onRemove: (i: number) => void
}) {
  const { t } = useTranslation()
  return (
    <div className="space-y-6">
      {fields.map((field, idx) => (
        <PlayerCard
          key={field.id}
          idx={idx}
          form={form}
          canRemove={fields.length > 1}
          onRemove={() => onRemove(idx)}
        />
      ))}

      <button
        type="button"
        onClick={onAdd}
        className="w-full bg-white border-2 border-dashed border-emerald-300 text-emerald-700 hover:bg-emerald-50 font-medium py-3 rounded-md"
      >
        + {t('register.players.add')}
      </button>
    </div>
  )
}

function PlayerCard({
  idx,
  form,
  canRemove,
  onRemove,
}: {
  idx: number
  form: any
  canRemove: boolean
  onRemove: () => void
}) {
  const { t } = useTranslation()
  const e = form.formState.errors.players?.[idx] ?? {}
  return (
    <div className="bg-white border border-slate-200 rounded-lg p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-bold text-emerald-800">{t('register.players.player', { n: idx + 1 })}</h3>
        {canRemove && (
          <button type="button" onClick={onRemove} className="text-sm text-rose-600 hover:underline">
            {t('register.players.remove')}
          </button>
        )}
      </div>

      <div className="grid sm:grid-cols-2 gap-4">
        <Field label={t('register.players.firstName')} error={e.firstName && t('common.required')}>
          <input className={inputCls} {...form.register(`players.${idx}.firstName`)} />
        </Field>
        <Field label={t('register.players.lastName')} error={e.lastName && t('common.required')}>
          <input className={inputCls} {...form.register(`players.${idx}.lastName`)} />
        </Field>
        <Field label={t('register.players.dob')} error={e.dateOfBirth && t('common.required')}>
          <input type="date" className={inputCls} {...form.register(`players.${idx}.dateOfBirth`)} />
        </Field>
        <Field label={t('register.players.grade')} error={e.schoolGrade && t('common.required')}>
          <select className={inputCls} {...form.register(`players.${idx}.schoolGrade`)}>
            <option value="">—</option>
            {GRADES.map(g => <option key={g} value={g}>{g}</option>)}
          </select>
        </Field>
        <Field label={t('register.players.shirtSize')} error={e.shirtSize && t('common.required')}>
          <select className={inputCls} {...form.register(`players.${idx}.shirtSize`)}>
            <option value="">—</option>
            {SHIRT_SIZES.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </Field>
        <Field label={t('register.players.shortSize')} error={e.shortSize && t('common.required')}>
          <select className={inputCls} {...form.register(`players.${idx}.shortSize`)}>
            <option value="">—</option>
            {SHIRT_SIZES.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </Field>
        <Field label={t('register.players.shoeSize')} error={e.shoeSize && t('common.required')}>
          <input className={inputCls} placeholder="e.g. 4Y, 7" {...form.register(`players.${idx}.shoeSize`)} />
        </Field>
        <Field label={t('register.players.heardFrom')} span={2}>
          <input
            className={inputCls}
            placeholder={t('register.players.heardFromPlaceholder')}
            {...form.register(`players.${idx}.heardFrom`)}
          />
        </Field>
      </div>

      <PlayerWaiver idx={idx} form={form} />
    </div>
  )
}

function PlayerWaiver({ idx, form }: { idx: number; form: any }) {
  const { t } = useTranslation()
  const e = form.formState.errors.players?.[idx] ?? {}
  const w = (k: string) => t(`register.waiver.${k}`)

  const renderBullets = (key: string) => {
    const items = t(`register.waiver.${key}`, { returnObjects: true }) as string[]
    return (
      <ul className="list-disc pl-6 space-y-1 mt-1">
        {items.map((b, i) => <li key={i}>{b}</li>)}
      </ul>
    )
  }

  return (
    <div className="border-t-2 border-emerald-100 pt-6">
      <h4 className="text-base font-bold text-emerald-800">{w('heading')}</h4>
      <p className="text-xs text-slate-500 mb-1">{w('subheading')}</p>
      <p className="text-xs text-amber-700 mb-4">{w('hint')}</p>

      <div className="bg-slate-50 rounded-md p-4 mb-4">
        <h5 className="font-bold text-sm text-slate-700 mb-3">{w('participantInfo')}</h5>
        <div className="grid sm:grid-cols-2 gap-3 text-sm">
          <Field label={w('participantName')} error={e.waiverParticipantName && t('common.required')} span={2}>
            <input className={inputCls} {...form.register(`players.${idx}.waiverParticipantName`)} />
          </Field>
          <Field label={w('teamName')}>
            <input className={inputCls} {...form.register(`players.${idx}.waiverTeamName`)} />
          </Field>
          <Field label={w('parentGuardian')} error={e.waiverParentGuardianName && t('common.required')}>
            <input className={inputCls} {...form.register(`players.${idx}.waiverParentGuardianName`)} />
          </Field>
          <Field label={w('phone')} error={e.waiverPhone && t('common.required')}>
            <input type="tel" className={inputCls} {...form.register(`players.${idx}.waiverPhone`)} />
          </Field>
          <Field label={w('email')} error={e.waiverEmail && t('common.required')}>
            <input type="email" className={inputCls} {...form.register(`players.${idx}.waiverEmail`)} />
          </Field>
        </div>
      </div>

      <div className="space-y-4 text-sm text-slate-700 leading-relaxed">
        <div>
          <h5 className="font-bold text-slate-800 bg-slate-100 px-3 py-1.5 rounded">{w('assumptionRisk')}</h5>
          <p className="mt-2">{w('assumptionRiskBody')}</p>
          {renderBullets('assumptionRiskBullets')}
          <p className="mt-2 font-medium">{w('assumptionRiskAccept')}</p>
        </div>
        <div>
          <h5 className="font-bold text-slate-800 bg-slate-100 px-3 py-1.5 rounded">{w('waiverLiability')}</h5>
          <p className="mt-2">{w('waiverLiabilityBody')}</p>
          {renderBullets('waiverLiabilityBullets')}
          <p className="mt-2">{w('waiverLiabilityClose')}</p>
        </div>
        <div>
          <h5 className="font-bold text-slate-800 bg-slate-100 px-3 py-1.5 rounded">{w('medicalAuth')}</h5>
          <p className="mt-2">{w('medicalAuthBody')}</p>
          {renderBullets('medicalAuthBullets')}
          <p className="mt-2">{w('medicalAuthClose')}</p>
        </div>
        <div>
          <h5 className="font-bold text-slate-800 bg-slate-100 px-3 py-1.5 rounded">{w('mediaRelease')}</h5>
          <p className="mt-2">{w('mediaReleaseBody')}</p>
          {renderBullets('mediaReleaseBullets')}
          <p className="mt-2">{w('mediaReleaseClose')}</p>
        </div>
        <div>
          <h5 className="font-bold text-slate-800 bg-slate-100 px-3 py-1.5 rounded">{w('rules')}</h5>
          <p className="mt-2">{w('rulesBody')}</p>
          {renderBullets('rulesBullets')}
        </div>
      </div>

      <div className="mt-6">
        <label className="block text-sm font-bold text-slate-700 mb-2">{w('signature')}</label>
        <Controller
          control={form.control}
          name={`players.${idx}.signatureDataUrl`}
          render={({ field, fieldState }) => (
            <SignaturePad
              value={field.value || null}
              onChange={(v) => field.onChange(v ?? '')}
              error={fieldState.error ? w('signatureRequired') : undefined}
            />
          )}
        />
      </div>
    </div>
  )
}
