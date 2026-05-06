import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'

const ADDRESS_LINE1 = '4900 E Lana Dr'
const ADDRESS_LINE2 = 'Las Vegas, NV 89121'
const MAPS_URL = 'https://www.google.com/maps/search/?api=1&query=4900+E+Lana+Dr+Las+Vegas+NV+89121'

export function InfoPage() {
  const { i18n } = useTranslation()
  const es = i18n.resolvedLanguage?.startsWith('es') ?? false

  return (
    <Layout>
      <div className="max-w-3xl mx-auto px-4 py-10 space-y-10 text-slate-700">
        <header>
          <h1 className="text-3xl font-bold text-emerald-800">
            {es ? 'Información del programa' : 'Program Information'}
          </h1>
          <p className="mt-2 text-slate-600">
            {es
              ? 'Dónde entrenamos, cuándo entrenamos y dónde competimos.'
              : 'Where we train, when we train, and where we compete.'}
          </p>
        </header>

        <Card>
          <h2 className="text-xl font-bold text-emerald-800">
            {es ? 'Lugar de entrenamiento' : 'Practice Location'}
          </h2>
          <p className="mt-3 font-medium text-slate-900">Maslow Park</p>
          <p className="text-slate-600">
            {ADDRESS_LINE1}<br />
            {ADDRESS_LINE2}
          </p>
          <a
            href={MAPS_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-block mt-3 text-emerald-700 hover:underline font-medium"
          >
            {es ? 'Ver en Google Maps →' : 'View on Google Maps →'}
          </a>
        </Card>

        <Card>
          <h2 className="text-xl font-bold text-emerald-800">
            {es ? 'Horario de entrenamiento' : 'Practice Schedule'}
          </h2>
          <p className="mt-3 text-slate-900">
            {es ? 'Lunes y Miércoles' : 'Mondays and Wednesdays'}
          </p>
          <p className="text-slate-600">6:00 p.m. – 8:00 p.m.</p>
        </Card>

        <Card>
          <h2 className="text-xl font-bold text-emerald-800">
            {es ? 'Dónde jugamos' : 'Where We Play'}
          </h2>
          <ul className="mt-3 space-y-2 text-slate-700">
            <li>
              <span className="font-medium text-slate-900">
                {es ? 'Liga Mexicana' : 'Mexican League'}
              </span>
              <span className="text-slate-600"> — {es ? 'Partidos de viernes por la noche' : 'Friday Night Games'}</span>
            </li>
            <li>
              <span className="font-medium text-slate-900">Nevada South Youth Soccer League</span>
            </li>
            <li>
              <span className="font-medium text-slate-900">
                {es ? 'Todos los torneos locales' : 'All local tournaments'}
              </span>
            </li>
          </ul>
        </Card>
      </div>
    </Layout>
  )
}

function Card({ children }: { children: React.ReactNode }) {
  return (
    <section className="bg-white border border-slate-200 rounded-lg p-6">
      {children}
    </section>
  )
}
