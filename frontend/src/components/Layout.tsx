import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { LanguageToggle } from './LanguageToggle'
import type { ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext'

export function Layout({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const { me, logout } = useAuth()
  const navigate = useNavigate()

  const onLogout = async () => {
    await logout()
    navigate('/', { replace: true })
  }

  return (
    <div className="min-h-screen flex flex-col">
      <header className="bg-white border-b border-slate-200">
        <div className="max-w-5xl mx-auto px-4 py-3 flex items-center justify-between">
          <Link to="/" className="flex items-center gap-2 text-emerald-800 font-bold text-lg">
            <span aria-hidden className="inline-block w-7 h-7 rounded-full bg-emerald-700 text-white grid place-items-center">⚽</span>
            {t('common.appName')}
          </Link>
          <div className="flex items-center gap-3">
            <LanguageToggle />
            {me ? (
              <div className="flex items-center gap-2 text-sm">
                <span className="text-slate-600 hidden sm:inline">{me.firstName}</span>
                <button onClick={onLogout} className="text-emerald-700 hover:underline">
                  {t('auth.logout')}
                </button>
              </div>
            ) : (
              <Link to="/login" className="text-sm text-emerald-700 hover:underline">
                {t('auth.login')}
              </Link>
            )}
          </div>
        </div>
      </header>
      <main className="flex-1">{children}</main>
      <footer className="bg-white border-t border-slate-200 mt-12">
        <div className="max-w-5xl mx-auto px-4 py-6 text-sm text-slate-500 flex flex-wrap items-center justify-between gap-3">
          <span>© {new Date().getFullYear()} {t('common.appName')}</span>
          <div className="flex items-center gap-4">
            <Link to="/privacy" className="text-slate-400 hover:text-slate-600">{t('common.privacy')}</Link>
            <Link to="/data-deletion" className="text-slate-400 hover:text-slate-600">{t('common.dataDeletion')}</Link>
            {me?.isAdmin && (
              <Link to="/admin" className="text-slate-400 hover:text-slate-600">Admin</Link>
            )}
          </div>
        </div>
      </footer>
    </div>
  )
}
