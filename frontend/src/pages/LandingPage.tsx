import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { useAuth } from '../auth/AuthContext'

export function LandingPage() {
  const { t } = useTranslation()
  const { me } = useAuth()
  const ctaTo = me ? '/register' : '/signup'

  const pillars = [
    { key: 'coach', icon: '🏆' },
    { key: 'train', icon: '⚡' },
    { key: 'family', icon: '🤝' },
  ] as const

  return (
    <Layout>
      <section className="bg-gradient-to-br from-emerald-700 via-emerald-600 to-emerald-800 text-white">
        <div className="max-w-5xl mx-auto px-4 py-20 sm:py-28 text-center">
          <h1 className="text-4xl sm:text-5xl font-extrabold tracking-tight">
            {t('landing.hero')}
          </h1>
          <p className="mt-4 text-lg sm:text-xl text-emerald-50 max-w-2xl mx-auto">
            {t('landing.subhero')}
          </p>
          <div className="mt-8 flex items-center justify-center gap-3">
            <Link
              to={ctaTo}
              className="inline-block bg-white text-emerald-800 font-semibold px-6 py-3 rounded-md shadow hover:shadow-lg transition"
            >
              {t('landing.cta')}
            </Link>
            {!me && (
              <Link
                to="/login"
                className="inline-block bg-emerald-900/40 text-white font-medium px-5 py-3 rounded-md border border-emerald-100/30 hover:bg-emerald-900/60 transition"
              >
                {t('auth.login')}
              </Link>
            )}
          </div>
        </div>
      </section>

      <section id="info" className="max-w-5xl mx-auto px-4 py-16">
        <div className="grid sm:grid-cols-3 gap-6">
          {pillars.map(p => (
            <div key={p.key} className="bg-white rounded-lg border border-slate-200 p-6 shadow-sm">
              <div className="text-3xl">{p.icon}</div>
              <h3 className="mt-3 font-bold text-emerald-800 text-lg">
                {t(`landing.pillars.${p.key}.title`)}
              </h3>
              <p className="mt-2 text-slate-600 text-sm">
                {t(`landing.pillars.${p.key}.body`)}
              </p>
            </div>
          ))}
        </div>

        <div className="mt-12 bg-amber-50 border border-amber-200 rounded-lg p-6 text-amber-800 text-sm">
          {t('landing.placeholder')}
        </div>
      </section>
    </Layout>
  )
}
