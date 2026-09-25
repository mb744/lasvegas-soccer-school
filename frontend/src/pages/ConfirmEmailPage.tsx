import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { Api } from '../api/client'
import { useAuth } from '../auth/AuthContext'

/** Landing page for the "confirm your email" link. No sign-in needed — the token is the proof. */
export function ConfirmEmailPage() {
  const { t } = useTranslation()
  const { me, refresh } = useAuth()
  const [params] = useSearchParams()
  const userId = params.get('userId') ?? ''
  const token = params.get('token') ?? ''
  const [state, setState] = useState<'working' | 'done' | 'failed'>('working')
  // React strict mode runs effects twice in dev; the token is single-use, so only submit once.
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true
    if (!userId || !token) { setState('failed'); return }
    Api.confirmEmail(userId, token)
      .then(async () => {
        setState('done')
        // Clear the "please confirm" banner if this browser is signed in as that user.
        if (me?.userId === userId) await refresh()
      })
      .catch(() => setState('failed'))
  }, [userId, token, me?.userId, refresh])

  return (
    <Layout>
      <div className="max-w-md mx-auto px-4 py-10 space-y-6">
        <h1 className="text-3xl font-bold text-emerald-800">{t('auth.confirmTitle')}</h1>
        {state === 'working' && <p className="text-slate-500">{t('auth.confirming')}</p>}
        {state === 'done' && (
          <div className="bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-md p-4 text-sm">
            {t('auth.confirmDone')}
          </div>
        )}
        {state === 'failed' && (
          <div className="bg-rose-50 border border-rose-200 text-rose-800 rounded-md p-4 text-sm">
            {t('auth.confirmFailed')}
          </div>
        )}
        {state !== 'working' && (
          <Link to={me ? '/account' : '/login'} className="inline-block text-emerald-700 hover:underline">
            {t('auth.confirmContinue')} →
          </Link>
        )}
      </div>
    </Layout>
  )
}
