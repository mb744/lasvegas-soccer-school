import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { SignaturePad } from '../components/SignaturePad'
import { RequiredLabel, useRequiredValidation } from '../components/RequiredField'
import { Api } from '../api/client'
import type { AddRegistrationPlayerRequest, RegistrationDetail, RegistrationPlayerDetail, RegistrationSummary } from '../api/types'

const GRADES = ['Pre-K', 'K', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12']
const UNIFORM_SIZES = ['YXS', 'YS', 'YM', 'YL', 'YXL', 'AS', 'AM', 'AL', 'AXL', 'A2XL']

/**
 * Parent home page after login. Shows the parent's most recent registration with controls to
 * finish it: tick the registration-level waiver consent, add players to the roster, and sign
 * each player's waiver (signature pad inline). Falls through to /register when the parent
 * doesn't yet have any registration.
 */
export function AccountPage() {
  const { t } = useTranslation()
  const [summaries, setSummaries] = useState<RegistrationSummary[] | null>(null)
  const [detail, setDetail] = useState<RegistrationDetail | null>(null)
  const [pickedId, setPickedId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    (async () => {
      try {
        const s = await Api.myRegistrations()
        setSummaries(s)
        if (s.length > 0) setPickedId(s[0].id)
      } catch (e: any) { setError(extractErr(e)) }
    })()
  }, [])

  useEffect(() => {
    if (pickedId === null) { setDetail(null); return }
    (async () => {
      try { setDetail(await Api.getRegistration(pickedId)) }
      catch (e: any) { setError(extractErr(e)) }
    })()
  }, [pickedId])

  const consent = async () => {
    if (!detail) return
    setBusy(true); setError(null); setNotice(null)
    try {
      setDetail(await Api.consentRegistration(detail.id))
      setNotice(t('account.consentSaved'))
    } catch (e: any) { setError(extractErr(e)) }
    finally { setBusy(false) }
  }

  if (summaries === null) {
    return <Layout><div className="max-w-3xl mx-auto px-4 py-10 text-slate-500">{t('common.loading')}</div></Layout>
  }
  if (summaries.length === 0) {
    return (
      <Layout>
        <div className="max-w-3xl mx-auto px-4 py-10 space-y-3">
          <h1 className="text-2xl font-bold text-emerald-800">{t('account.title')}</h1>
          <p className="text-slate-600">{t('account.noRegistration')}</p>
          <Link to="/register" className="inline-block bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
            {t('account.goRegister')}
          </Link>
        </div>
      </Layout>
    )
  }
  if (!detail) {
    return <Layout><div className="max-w-3xl mx-auto px-4 py-10 text-slate-500">{t('common.loading')}</div></Layout>
  }

  const unsignedCount = detail.players.filter(p => !p.hasSignature).length

  return (
    <Layout>
      <div className="max-w-3xl mx-auto px-4 py-10 space-y-6">
        <header className="space-y-1">
          <h1 className="text-2xl font-bold text-emerald-800">{t('account.title')}</h1>
          <p className="text-slate-600">{detail.parentFirstName} {detail.parentLastName} · {detail.email}</p>
          {summaries.length > 1 && (
            <label className="block text-sm">
              <span className="text-slate-500">{t('account.season')}</span>
              <select value={pickedId ?? ''} onChange={e => setPickedId(Number(e.target.value))}
                className="ml-2 border border-slate-300 rounded-md px-2 py-1 text-sm">
                {summaries.map(s => <option key={s.id} value={s.id}>{s.season}</option>)}
              </select>
            </label>
          )}
        </header>

        {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
        {notice && <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>}

        <section className="bg-white border border-slate-200 rounded-lg p-5 space-y-3">
          <div className="flex items-baseline justify-between">
            <h2 className="font-semibold text-emerald-800">{detail.season}</h2>
            <span className="text-xs text-slate-500">{t('account.unsignedCount', { count: unsignedCount })}</span>
          </div>
          {!detail.waiverConsent ? (
            <label className="flex items-start gap-2 text-sm">
              <input type="checkbox" disabled={busy} onChange={consent} className="mt-1" />
              <span>{t('account.consentLabel')}</span>
            </label>
          ) : (
            <p className="text-xs text-emerald-700">✓ {t('account.consentDone')}</p>
          )}
        </section>

        <section className="bg-white border border-slate-200 rounded-lg p-5 space-y-3">
          <h2 className="font-semibold text-emerald-800">{t('account.players')}</h2>
          {detail.players.length === 0 && <p className="text-xs text-slate-500">{t('account.noPlayers')}</p>}
          <div className="space-y-3">
            {detail.players.map(p => (
              <PlayerRow key={p.id} regId={detail.id} player={p}
                onUpdated={d => { setDetail(d); setNotice(t('account.signSaved')) }}
                onError={e => setError(e)} />
            ))}
          </div>
          <AddOwnPlayerForm regId={detail.id}
            onUpdated={d => { setDetail(d); setNotice(t('account.playerAdded')) }}
            onError={e => setError(e)} />
        </section>

        {unsignedCount === 0 && detail.waiverConsent && (
          <div className="text-sm">
            <button onClick={() => Api.viewWaivers(detail.id)} className="text-emerald-700 hover:underline">
              {t('account.viewWaivers')}
            </button>
          </div>
        )}
      </div>
    </Layout>
  )
}

function PlayerRow({ regId, player, onUpdated, onError }: {
  regId: number
  player: RegistrationPlayerDetail
  onUpdated: (d: RegistrationDetail) => void
  onError: (msg: string) => void
}) {
  const { t } = useTranslation()
  const [signing, setSigning] = useState(false)
  const [signature, setSignature] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const confirm = async () => {
    if (!signature) { onError(t('account.signRequired')); return }
    setSaving(true)
    try {
      onUpdated(await Api.signRegistrationPlayer(regId, player.id, { signatureDataUrl: signature }))
      setSigning(false); setSignature(null)
    } catch (e: any) { onError(extractErr(e)) }
    finally { setSaving(false) }
  }

  return (
    <div className="border border-slate-200 rounded p-3">
      <div className="flex items-baseline justify-between gap-2 flex-wrap">
        <div>
          <div className="font-medium text-slate-800">{player.firstName} {player.lastName}</div>
          <div className="text-[11px] text-slate-500">
            {player.dateOfBirth} · {t('register.players.grade')} {player.schoolGrade} · {player.ageClassificationName ?? '—'}
          </div>
        </div>
        {player.hasSignature ? (
          <span className="text-xs text-emerald-700">✓ {t('account.signed')}</span>
        ) : (
          !signing && (
            <button onClick={() => setSigning(true)} className="text-xs bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800">
              {t('account.signWaiver')}
            </button>
          )
        )}
      </div>
      {signing && (
        <div className="mt-3 space-y-2">
          <SignaturePad value={signature} onChange={setSignature} />
          <div className="flex gap-2">
            <button onClick={confirm} disabled={saving || !signature}
              className="text-xs bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
              {saving ? t('register.submitting') : t('account.confirmSignature')}
            </button>
            <button onClick={() => { setSigning(false); setSignature(null) }} className="text-xs text-slate-600 hover:underline">
              {t('admin.cancel')}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}

function AddOwnPlayerForm({ regId, onUpdated, onError }: {
  regId: number
  onUpdated: (d: RegistrationDetail) => void
  onError: (msg: string) => void
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [first, setFirst] = useState(''); const [last, setLast] = useState('')
  const [dob, setDob] = useState(''); const [grade, setGrade] = useState('')
  const [uniform, setUniform] = useState(''); const [shoe, setShoe] = useState('')
  const [saving, setSaving] = useState(false)
  const v = useRequiredValidation(['firstName', 'lastName', 'dob'])

  const reset = () => {
    setFirst(''); setLast(''); setDob(''); setGrade(''); setUniform(''); setShoe('')
    setOpen(false); v.reset()
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!v.checkSubmit({ firstName: first, lastName: last, dob })) {
      onError(t('account.addPlayerRequired')); return
    }
    setSaving(true)
    try {
      const payload: AddRegistrationPlayerRequest = {
        firstName: first.trim(), lastName: last.trim(), dateOfBirth: dob,
        schoolGrade: grade.trim(), uniformSize: uniform.trim(), shoeSize: shoe.trim(),
      }
      onUpdated(await Api.addOwnRegistrationPlayer(regId, payload))
      reset()
    } catch (e: any) { onError(extractErr(e)) }
    finally { setSaving(false) }
  }

  if (!open) {
    return (
      <button onClick={() => setOpen(true)} className="text-sm text-emerald-700 hover:underline">+ {t('account.addPlayer')}</button>
    )
  }

  const baseCls = 'border border-slate-300 rounded-md px-2 py-1 text-sm w-full'
  return (
    <form onSubmit={submit} noValidate className="border border-slate-200 rounded p-3 grid sm:grid-cols-2 gap-2">
      <label className="block text-xs space-y-1">
        <RequiredLabel>{t('register.players.firstName')}</RequiredLabel>
        <input ref={v.register('firstName')}
          className={`${baseCls} ${v.fieldCls('firstName')}`}
          value={first} onChange={e => setFirst(e.target.value)}
          onBlur={e => v.onFieldBlur('firstName', e.target.value)} />
      </label>
      <label className="block text-xs space-y-1">
        <RequiredLabel>{t('register.players.lastName')}</RequiredLabel>
        <input ref={v.register('lastName')}
          className={`${baseCls} ${v.fieldCls('lastName')}`}
          value={last} onChange={e => setLast(e.target.value)}
          onBlur={e => v.onFieldBlur('lastName', e.target.value)} />
      </label>
      <label className="block text-xs space-y-1">
        <RequiredLabel>{t('register.players.dob')}</RequiredLabel>
        <input ref={v.register('dob')} type="date"
          className={`${baseCls} ${v.fieldCls('dob')}`}
          value={dob} onChange={e => setDob(e.target.value)}
          onBlur={e => v.onFieldBlur('dob', e.target.value)} />
      </label>
      <label className="block text-xs space-y-1">
        <span className="text-slate-700">{t('register.players.grade')}</span>
        <select className={baseCls} value={grade} onChange={e => setGrade(e.target.value)}>
          <option value="">—</option>
          {GRADES.map(g => <option key={g} value={g}>{g}</option>)}
        </select>
      </label>
      <label className="block text-xs space-y-1">
        <span className="text-slate-700">{t('register.players.uniformSize')}</span>
        <select className={baseCls} value={uniform} onChange={e => setUniform(e.target.value)}>
          <option value="">—</option>
          {UNIFORM_SIZES.map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </label>
      <label className="block text-xs space-y-1">
        <span className="text-slate-700">{t('register.players.shoeSize')}</span>
        <input className={baseCls} value={shoe} onChange={e => setShoe(e.target.value)} />
      </label>
      <div className="sm:col-span-2 flex gap-2 pt-1">
        <button type="submit" disabled={saving}
          className="text-xs bg-emerald-700 text-white px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {saving ? t('register.submitting') : t('account.addPlayer')}
        </button>
        <button type="button" onClick={reset} className="text-xs text-slate-600 hover:underline">{t('admin.cancel')}</button>
      </div>
    </form>
  )
}

function extractErr(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}
