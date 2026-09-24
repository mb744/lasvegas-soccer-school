import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { Api } from '../api/client'

export function ResetPasswordPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''

  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  const valid = password.length >= 8 && password === confirm && email && token

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!valid) {
      if (password.length < 8) setError(t('auth.resetShort'))
      else if (password !== confirm) setError(t('auth.resetMismatch'))
      else setError(t('auth.resetInvalidLink'))
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      await Api.resetPassword(email, token, password)
      setDone(true)
      setTimeout(() => navigate('/login', { replace: true }), 2000)
    } catch (err: any) {
      const msg = err?.response?.data ?? err?.message ?? 'Error'
      setError(typeof msg === 'string' ? msg : t('auth.resetFailed'))
    } finally {
      setSubmitting(false)
    }
  }

  if (!email || !token) {
    return (
      <Layout>
        <div className="max-w-md mx-auto px-4 py-10">
          <h1 className="text-3xl font-bold text-emerald-800">{t('auth.resetTitle')}</h1>
          <div className="mt-6 bg-rose-50 border border-rose-200 text-rose-800 rounded-md p-4 text-sm">
            {t('auth.resetInvalidLink')}
          </div>
          <p className="text-sm text-slate-600 mt-6 text-center">
            <Link to="/forgot-password" className="text-emerald-700 hover:underline">
              {t('auth.forgotTitle')}
            </Link>
          </p>
        </div>
      </Layout>
    )
  }

  return (
    <Layout>
      <div className="max-w-md mx-auto px-4 py-10">
        <h1 className="text-3xl font-bold text-emerald-800">{t('auth.resetTitle')}</h1>
        <p className="text-slate-600 mt-1">{t('auth.resetSubtitle', { email })}</p>

        {done ? (
          <div className="mt-6 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-md p-4 text-sm">
            {t('auth.resetDone')}
          </div>
        ) : (
          <form onSubmit={onSubmit} noValidate className="mt-6 space-y-4">
            <label className="flex flex-col text-sm">
              <span className="font-medium text-slate-700 mb-1">{t('auth.newPassword')}</span>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                minLength={8}
                required
                className="border border-slate-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </label>
            <label className="flex flex-col text-sm">
              <span className="font-medium text-slate-700 mb-1">{t('auth.confirmPassword')}</span>
              <input
                type="password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                minLength={8}
                required
                className="border border-slate-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </label>
            {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
            <button
              type="submit"
              disabled={submitting || !valid}
              className="w-full bg-emerald-700 text-white font-semibold py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60"
            >
              {submitting ? t('auth.resetSubmitting') : t('auth.resetSubmit')}
            </button>
          </form>
        )}
      </div>
    </Layout>
  )
}
