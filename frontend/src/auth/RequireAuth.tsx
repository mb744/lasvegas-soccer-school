import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import { Layout } from '../components/Layout'
import { useTranslation } from 'react-i18next'
import { can } from './can'

/** `adminOnly`: admins. `permission`: anyone holding at least one of the given permission keys. */
export function RequireAuth({
  children, adminOnly = false, permission,
}: { children: React.ReactNode; adminOnly?: boolean; permission?: string | string[] }) {
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

  const required = permission === undefined ? [] : Array.isArray(permission) ? permission : [permission]
  const denied = (adminOnly && !me.isAdmin) || (required.length > 0 && !can(me, ...required))
  if (denied) {
    return (
      <Layout>
        <div className="max-w-2xl mx-auto px-4 py-16 text-center">
          <h1 className="text-2xl font-bold text-rose-700">{adminOnly ? t('auth.adminOnly') : t('auth.noPermission')}</h1>
        </div>
      </Layout>
    )
  }

  return <>{children}</>
}
