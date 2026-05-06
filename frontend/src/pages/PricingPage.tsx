import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'

export function PricingPage() {
  const { i18n } = useTranslation()
  const es = i18n.resolvedLanguage?.startsWith('es') ?? false

  return (
    <Layout>
      <div className="max-w-3xl mx-auto px-4 py-10 space-y-8 text-slate-700">
        <header>
          <h1 className="text-3xl font-bold text-emerald-800">
            {es ? 'Precios' : 'Pricing'}
          </h1>
          <p className="mt-2 text-slate-600">
            {es
              ? 'Entrenamiento mensual con primer mes gratis, más una cuota única que incluye uniformes y mochila.'
              : 'Monthly training with a free first month, plus a one-time fee that includes uniforms and a backpack.'}
          </p>
        </header>

        <section className="bg-gradient-to-br from-emerald-700 to-emerald-800 text-white rounded-lg p-8 shadow">
          <div className="flex items-baseline gap-2">
            <span className="text-5xl font-extrabold">$40</span>
            <span className="text-xl text-emerald-100">/ {es ? 'mes' : 'month'}</span>
          </div>
          <p className="mt-3 text-emerald-50">
            {es ? 'Cuota mensual de entrenamiento.' : 'Monthly training fee.'}
          </p>

          <div className="mt-6 inline-flex items-center gap-2 bg-white text-emerald-800 font-bold px-4 py-2 rounded-full text-sm">
            <span aria-hidden>🎁</span>
            {es ? 'Primer mes GRATIS' : 'First Month FREE'}
          </div>
        </section>

        <section className="bg-white border border-slate-200 rounded-lg p-6">
          <h2 className="text-xl font-bold text-emerald-800">
            {es ? 'Cuota única de inscripción' : 'One-Time Subscription Fee'}
          </h2>
          <p className="mt-2 text-slate-600">
            {es
              ? 'Después del primer mes gratis, $150 una sola vez cuando continúe con el programa. Incluye:'
              : 'After your free first month, $150 one-time when you continue with the program. Includes:'}
          </p>
          <div className="mt-4 flex items-baseline gap-2">
            <span className="text-3xl font-bold text-slate-900">$150</span>
            <span className="text-slate-500 text-sm">{es ? 'una sola vez' : 'one-time'}</span>
          </div>
          <ul className="mt-4 space-y-2">
            <Includes>{es ? '2 uniformes de juego' : '2 game uniforms'}</Includes>
            <Includes>{es ? '1 uniforme de entrenamiento' : '1 training uniform'}</Includes>
            <Includes>{es ? '1 mochila' : '1 backpack'}</Includes>
          </ul>
        </section>

        <section className="bg-amber-50 border border-amber-200 rounded-lg p-6 text-amber-900 text-sm">
          {es
            ? 'Los precios pueden cambiar para temporadas futuras. Las tarifas vigentes se confirman en el momento de la inscripción.'
            : 'Pricing may change for future seasons. Current rates are confirmed at the time of registration.'}
        </section>
      </div>
    </Layout>
  )
}

function Includes({ children }: { children: React.ReactNode }) {
  return (
    <li className="flex items-start gap-2">
      <span aria-hidden className="text-emerald-700 font-bold mt-0.5">✓</span>
      <span className="text-slate-700">{children}</span>
    </li>
  )
}
