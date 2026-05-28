import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'

export function AdminPage() {
  const { t } = useTranslation()

  const cards = [
    { to: '/admin/outreach',            title: t('admin.hubOutreach'),         blurb: t('admin.hubOutreachBlurb'),         icon: '📨' },
    { to: '/admin/messaging',           title: t('admin.hubMessaging'),        blurb: t('admin.hubMessagingBlurb'),        icon: '💬' },
    { to: '/admin/teams',               title: t('admin.hubTeams'),            blurb: t('admin.hubTeamsBlurb'),            icon: '⚽' },
    { to: '/admin/registrations',       title: t('admin.hubRegistrations'),    blurb: t('admin.hubRegistrationsBlurb'),    icon: '📋' },
    { to: '/admin/age-classifications', title: t('admin.hubAgeClassifications'), blurb: t('admin.hubAgeClassificationsBlurb'), icon: '🏷️' },
    { to: '/admin/users',               title: t('admin.hubUsers'),            blurb: t('admin.hubUsersBlurb'),            icon: '👥' },
  ] as const

  return (
    <Layout>
      <div className="max-w-5xl mx-auto px-4 py-10 space-y-8">
        <header>
          <h1 className="text-3xl font-bold text-emerald-800">{t('admin.title')}</h1>
          <p className="mt-2 text-slate-600">{t('admin.hubSubtitle')}</p>
        </header>

        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {cards.map(c => (
            <Link
              key={c.to}
              to={c.to}
              className="group bg-white border border-slate-200 hover:border-emerald-400 hover:shadow-md transition rounded-lg p-6"
            >
              <div className="text-3xl">{c.icon}</div>
              <h2 className="mt-3 font-bold text-emerald-800 text-lg group-hover:underline">{c.title}</h2>
              <p className="mt-1 text-slate-600 text-sm">{c.blurb}</p>
            </Link>
          ))}
        </div>
      </div>
    </Layout>
  )
}
