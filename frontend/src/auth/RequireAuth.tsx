import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import { Layout } from '../components/Layout'
import { useTranslation } from 'react-i18next'

export function RequireAuth({ children, adminOnly = false }: { children: React.ReactNode; adminOnly?: boolean }) {
  const { me, loading } = useAuth()
  const location = useLocation()
  const { t } = useTranslation()

  if (loading) {
    return (
      <Layout>
        <div className="max-w-2xl mx-auto px-4 py-16 text-center text-slate-500">{t('common.loading')}</div>
      </Layout>
    )
  }

  if (!me) {
    const next = encodeURIComponent(location.pathname + location.search)
    return <Navigate to={`/login?next=${next}`} replace />
  }

  if (adminOnly && !me.isAdmin) {
    return (
      <Layout>
        <div className="max-w-2xl mx-auto px-4 py-16 text-center">
          <h1 className="text-2xl font-bold text-rose-700">{t('auth.adminOnly')}</h1>
        </div>
      </Layout>
    )
  }

  return <>{children}</>
}
