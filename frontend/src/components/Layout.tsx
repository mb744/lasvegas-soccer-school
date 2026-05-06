import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { LanguageToggle } from './LanguageToggle'
import type { ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext'

export function Layout({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const { me, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const navLink = ({ isActive }: { isActive: boolean }) =>
    `text-sm font-medium ${isActive ? 'text-emerald-800' : 'text-slate-600 hover:text-emerald-700'}`

  const onLogout = async () => {
    await logout()
    navigate('/', { replace: true })
  }

  const displayName = me?.firstName?.trim() || me?.email.split('@')[0] || ''
  const showRegisterCta = !location.pathname.startsWith('/admin')
  const ctaTo = me ? '/register' : '/signup'

  return (
    <div className="min-h-screen flex flex-col">
      <header className="bg-white border-b border-slate-200">
        <div className="max-w-5xl mx-auto px-4 py-3 flex items-center justify-between gap-4">
          <Link to="/" className="flex items-center gap-2 text-emerald-800 font-bold text-lg shrink-0">
            <img src="/logo.png" alt="" className="w-8 h-8 object-contain" />
            <span className="hidden sm:inline">{t('common.appName')}</span>
            <span className="sm:hidden">LVSS</span>
          </Link>
          <nav className="hidden md:flex items-center gap-5">
            <NavLink to="/about" className={navLink}>{t('common.about')}</NavLink>
            <NavLink to="/info" className={navLink}>{t('common.info')}</NavLink>
            <NavLink to="/pricing" className={navLink}>{t('common.pricing')}</NavLink>
          </nav>
          <div className="flex items-center gap-3">
            <LanguageToggle />
            {me ? (
              <div className="flex items-center gap-2 text-sm">
                <span className="text-slate-600 hidden sm:inline">{displayName}</span>
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
        <nav className="md:hidden border-t border-slate-100 bg-slate-50">
          <div className="max-w-5xl mx-auto px-4 py-2 flex items-center gap-5">
            <NavLink to="/about" className={navLink}>{t('common.about')}</NavLink>
            <NavLink to="/info" className={navLink}>{t('common.info')}</NavLink>
            <NavLink to="/pricing" className={navLink}>{t('common.pricing')}</NavLink>
          </div>
        </nav>
      </header>
      <main className="flex-1">{children}</main>
      {showRegisterCta && (
        <section className="bg-emerald-700 text-white">
          <div className="max-w-5xl mx-auto px-4 py-10 sm:py-12 flex flex-col sm:flex-row items-center justify-between gap-6 text-center sm:text-left">
            <div>
              <h2 className="text-2xl font-bold">{t('cta.title')}</h2>
              <p className="text-emerald-50 text-sm mt-1">{t('cta.subtitle')}</p>
            </div>
            <Link
              to={ctaTo}
              className="inline-block bg-white text-emerald-800 font-semibold px-6 py-3 rounded-md shadow hover:shadow-lg transition shrink-0"
            >
              {t('cta.button')}
            </Link>
          </div>
        </section>
      )}
      <footer className="bg-white border-t border-slate-200">
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
