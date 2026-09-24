import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { Api } from '../api/client'
import type { PlayerPasswordResetInfo } from '../api/types'

const MIN_LENGTH = 6

/** Public page a parent reaches from the "reset your child's password" email sent when their kid
 *  taps "Forgot your password?" in the Daily Training app. No sign-in: the single-use token in
 *  the link is the authorization. */
export function ResetPlayerPasswordPage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''

  const [info, setInfo] = useState<PlayerPasswordResetInfo | null>(null)
  const [state, setState] = useState<'loading' | 'ready' | 'invalid' | 'done'>('loading')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!token) { setState('invalid'); return }
    Api.playerPasswordResetInfo(token)
      .then(i => { setInfo(i); setState('ready') })
      .catch(() => setState('invalid'))
  }, [token])

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (password.length < MIN_LENGTH) { setError(t('playerReset.tooShort')); return }
    if (password !== confirm) { setError(t('playerReset.mismatch')); return }
    setBusy(true)
    try {
      await Api.resetPlayerPassword(token, password)
      setState('done')
    } catch (e: any) {
      if (e?.response?.status === 404) setState('invalid')
      else setError(e?.response?.data || e?.message || 'Error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Layout>
      <div className="max-w-md mx-auto px-4 py-12">
        <div className="bg-white border border-slate-200 rounded-lg p-6 space-y-4">
          <h1 className="text-2xl font-bold text-emerald-800">{t('playerReset.title')}</h1>

          {state === 'loading' && <p className="text-slate-500">{t('playerReset.loading')}</p>}

          {state === 'invalid' && (
            <p className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{t('playerReset.invalid')}</p>
          )}

          {state === 'done' && info && (
            <p className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">
              {t('playerReset.done', { name: info.playerFirstName })}
            </p>
          )}

          {state === 'ready' && info && (
            <form onSubmit={submit} noValidate className="space-y-4">
              <p className="text-sm text-slate-600">
                {t('playerReset.forKid', { name: info.playerFirstName, username: info.username })}
              </p>
              <div>
                <label className="text-xs font-medium text-slate-600 block mb-1" htmlFor="pw">{t('playerReset.newPassword')}</label>
                <input id="pw" type="password" autoComplete="new-password" value={password}
                  onChange={e => setPassword(e.target.value)}
                  className="w-full border border-slate-300 rounded-md px-3 py-2" />
                <p className="text-xs text-slate-500 mt-1">{t('playerReset.help')}</p>
              </div>
              <div>
                <label className="text-xs font-medium text-slate-600 block mb-1" htmlFor="pw2">{t('playerReset.confirmPassword')}</label>
                <input id="pw2" type="password" autoComplete="new-password" value={confirm}
                  onChange={e => setConfirm(e.target.value)}
                  className="w-full border border-slate-300 rounded-md px-3 py-2" />
              </div>
              {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
              <button type="submit" disabled={busy}
                className="w-full bg-emerald-700 text-white font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                {t('playerReset.submit')}
              </button>
            </form>
          )}
        </div>
      </div>
    </Layout>
  )
}
