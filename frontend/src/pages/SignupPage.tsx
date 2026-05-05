import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { useAuth } from '../auth/AuthContext'
import { Api } from '../api/client'
import type { Language } from '../api/types'

export function SignupPage() {
  const { t, i18n } = useTranslation()
  const { signup, providers } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const next = params.get('next') || '/register'
  const hasGoogle = providers.includes('Google')
  const hasFacebook = providers.includes('Facebook')
  const hasAnyExternal = hasGoogle || hasFacebook

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [phone, setPhone] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
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

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <label className="flex flex-col text-sm">
              <span className="font-medium text-slate-700 mb-1">{t('auth.firstName')}</span>
              <input value={firstName} onChange={e => setFirstName(e.target.value)} required className={inputCls} />
            </label>
            <label className="flex flex-col text-sm">
              <span className="font-medium text-slate-700 mb-1">{t('auth.lastName')}</span>
              <input value={lastName} onChange={e => setLastName(e.target.value)} required className={inputCls} />
            </label>
          </div>
          <label className="flex flex-col text-sm">
            <span className="font-medium text-slate-700 mb-1">{t('auth.email')}</span>
            <input type="email" value={email} onChange={e => setEmail(e.target.value)} required className={inputCls} />
          </label>
          <label className="flex flex-col text-sm">
            <span className="font-medium text-slate-700 mb-1">{t('auth.phone')}</span>
            <input type="tel" value={phone} onChange={e => setPhone(e.target.value)} className={inputCls} />
          </label>
          <label className="flex flex-col text-sm">
            <span className="font-medium text-slate-700 mb-1">{t('auth.password')}</span>
            <input type="password" value={password} onChange={e => setPassword(e.target.value)} required minLength={8} className={inputCls} />
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
