import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { Api } from '../api/client'

export function ForgotPasswordPage() {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [sent, setSent] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!email.trim()) return
    setSubmitting(true)
    setError(null)
    try {
      await Api.forgotPassword(email.trim())
      setSent(true)
    } catch (err: any) {
      setError(err?.response?.data ?? err?.message ?? 'Error')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Layout>
      <div className="max-w-md mx-auto px-4 py-10">
        <h1 className="text-3xl font-bold text-emerald-800">{t('auth.forgotTitle')}</h1>
        <p className="text-slate-600 mt-1">{t('auth.forgotSubtitle')}</p>

        {sent ? (
          <div className="mt-6 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-md p-4 text-sm">
            {t('auth.forgotSent')}
          </div>
        ) : (
          <form onSubmit={onSubmit} noValidate className="mt-6 space-y-4">
            <label className="flex flex-col text-sm">
              <span className="font-medium text-slate-700 mb-1">{t('auth.email')}</span>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                className="border border-slate-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </label>
            {error && <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>}
            <button
              type="submit"
              disabled={submitting || !email.trim()}
              className="w-full bg-emerald-700 text-white font-semibold py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60"
            >
              {submitting ? t('auth.forgotSending') : t('auth.forgotSubmit')}
            </button>
          </form>
        )}

        <p className="text-sm text-slate-600 mt-6 text-center">
          <Link to="/login" className="text-emerald-700 hover:underline">
            {t('auth.backToLogin')}
          </Link>
        </p>
      </div>
    </Layout>
  )
}
