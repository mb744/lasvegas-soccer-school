import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { RequiredLabel, useRequiredValidation } from '../components/RequiredField'
import { useAuth } from '../auth/AuthContext'
import { Api } from '../api/client'
import type { Language } from '../api/types'

export function SignupPage() {
  const { t, i18n } = useTranslation()
  const { signup, providers } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  // New parents land on /account after signup (which falls through to /register when they
  // don't yet have a registration on file).
  const next = params.get('next') || '/account'
  const hasGoogle = providers.includes('Google')
  const hasFacebook = providers.includes('Facebook')
  const hasAnyExternal = hasGoogle || hasFacebook

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [phone, setPhone] = useState('')
  const [smsConsent, setSmsConsent] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const v = useRequiredValidation(['firstName', 'lastName', 'email', 'password'])

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!v.checkSubmit({ firstName, lastName, email, password })) return
    if (phone.trim() && !smsConsent) {
      setError(t('auth.smsConsentRequired'))
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      const language: Language = i18n.resolvedLanguage?.startsWith('es') ? 1 : 0
      await signup({ email, password, firstName, lastName, phone: phone || undefined, language })
      navigate(next, { replace: true })
    } catch (err: any) {
      const msg = err?.response?.data ?? err?.message ?? 'Error'
      setError(typeof msg === 'string' ? msg : t('auth.signupFailed'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Layout>
      <div className="max-w-md mx-auto px-4 py-10">
        <h1 className="text-3xl font-bold text-emerald-800">{t('auth.signupTitle')}</h1>
        <p className="text-slate-600 mt-1">{t('auth.signupSubtitle')}</p>

        {hasAnyExternal && (
          <>
            <div className="mt-8 space-y-3">
              {hasGoogle && (
                <a href={Api.externalLoginUrl('Google', next)} className={socialBtn}>
                  <span aria-hidden>🇬</span> {t('auth.continueWithGoogle')}
                </a>
              )}
              {hasFacebook && (
                <a href={Api.externalLoginUrl('Facebook', next)} className={socialBtn}>
                  <span aria-hidden>📘</span> {t('auth.continueWithFacebook')}
                </a>
              )}
            </div>

            <div className="my-6 flex items-center gap-3 text-xs text-slate-400">
              <div className="h-px flex-1 bg-slate-200" />
              {t('auth.or')}
              <div className="h-px flex-1 bg-slate-200" />
            </div>
          </>
        )}

        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <label className="flex flex-col text-sm">
              <RequiredLabel className="font-medium text-slate-700 mb-1">{t('auth.firstName')}</RequiredLabel>
              <input ref={v.register('firstName')} value={firstName} onChange={e => setFirstName(e.target.value)}
                onBlur={e => v.onFieldBlur('firstName', e.target.value)}
                className={`${inputCls} ${v.fieldCls('firstName')}`} />
            </label>
            <label className="flex flex-col text-sm">
              <RequiredLabel className="font-medium text-slate-700 mb-1">{t('auth.lastName')}</RequiredLabel>
              <input ref={v.register('lastName')} value={lastName} onChange={e => setLastName(e.target.value)}
                onBlur={e => v.onFieldBlur('lastName', e.target.value)}
                className={`${inputCls} ${v.fieldCls('lastName')}`} />
            </label>
          </div>
          <label className="flex flex-col text-sm">
            <RequiredLabel className="font-medium text-slate-700 mb-1">{t('auth.email')}</RequiredLabel>
            <input ref={v.register('email')} type="email" value={email} onChange={e => setEmail(e.target.value)}
              onBlur={e => v.onFieldBlur('email', e.target.value)}
              className={`${inputCls} ${v.fieldCls('email')}`} />
          </label>
          <label className="flex flex-col text-sm">
            <span className="font-medium text-slate-700 mb-1">{t('auth.phone')}</span>
            <input type="tel" value={phone} onChange={e => setPhone(e.target.value)} className={inputCls} />
          </label>
          <label className="flex items-start gap-2 text-xs text-slate-700">
            <input
              type="checkbox"
              checked={smsConsent}
              onChange={e => setSmsConsent(e.target.checked)}
              className="mt-0.5 w-4 h-4"
            />
            <span>
              {t('auth.smsConsentCheckbox')}
              <span
                aria-label={t('auth.smsConsent')}
                title={t('auth.smsConsent')}
                className="inline-flex items-center justify-center w-4 h-4 ml-1 text-[10px] font-bold rounded-full bg-slate-300 text-white cursor-help align-middle"
              >i</span>
              {' '}
              <a className="text-emerald-700 underline" href="/sms-terms.html" target="_blank" rel="noopener noreferrer">
                {t('auth.smsTermsLink')}
              </a>
            </span>
          </label>
          <label className="flex flex-col text-sm">
            <RequiredLabel className="font-medium text-slate-700 mb-1">{t('auth.password')}</RequiredLabel>
            <input ref={v.register('password')} type="password" value={password} onChange={e => setPassword(e.target.value)}
              onBlur={e => v.onFieldBlur('password', e.target.value)}
              minLength={8} className={`${inputCls} ${v.fieldCls('password')}`} />
            <span className="text-xs text-slate-500 mt-1">{t('auth.passwordHint')}</span>
          </label>
          {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
          <button
            type="submit"
            disabled={submitting}
            className="w-full bg-emerald-700 text-white font-semibold py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60"
          >
            {submitting ? t('auth.creatingAccount') : t('auth.createAccount')}
          </button>
        </form>

        <p className="text-sm text-slate-600 mt-6 text-center">
          {t('auth.haveAccount')}{' '}
          <Link to={`/login?next=${encodeURIComponent(next)}`} className="text-emerald-700 hover:underline font-medium">
            {t('auth.login')}
          </Link>
        </p>
      </div>
    </Layout>
  )
}

const inputCls = 'border border-slate-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500'
const socialBtn = 'flex items-center justify-center gap-2 w-full bg-white border border-slate-300 text-slate-700 font-medium py-2 rounded-md hover:bg-slate-50'
