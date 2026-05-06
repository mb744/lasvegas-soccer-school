import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'
import { useAuth } from '../auth/AuthContext'

export function AboutPage() {
  const { i18n } = useTranslation()
  const { me } = useAuth()
  const es = i18n.resolvedLanguage?.startsWith('es') ?? false
  const ctaTo = me ? '/register' : '/signup'

  return (
    <Layout>
      <div className="max-w-3xl mx-auto px-4 py-10 space-y-8 text-slate-700 leading-relaxed">
        <header>
          <h1 className="text-3xl font-bold text-emerald-800">
            {es ? 'Sobre Las Vegas Soccer School' : 'About Las Vegas Soccer School'}
          </h1>
          <p className="mt-2 text-slate-600">
            {es
              ? 'Una academia local fundada por un jugador profesional retirado, dedicada a desarrollar la próxima generación de jugadores de Las Vegas.'
              : 'A local academy founded by a retired professional player, dedicated to developing the next generation of Las Vegas players.'}
          </p>
        </header>

        <section className="bg-white border border-slate-200 rounded-lg p-6 sm:p-8">
          <div className="flex items-start gap-4">
            <div aria-hidden className="hidden sm:flex w-16 h-16 rounded-full bg-emerald-700 text-white items-center justify-center font-bold text-2xl shrink-0">
              RL
            </div>
            <div>
              <h2 className="text-2xl font-bold text-emerald-800">
                {es ? 'Conozca al fundador: Ricardo Llamas' : 'Meet the founder: Ricardo Llamas'}
              </h2>
              <p className="text-sm text-slate-500 mt-1">
                {es ? 'Ex profesional · Más de 30 años entrenando' : 'Former professional · 30+ years coaching'}
              </p>
            </div>
          </div>

          <div className="mt-6 space-y-4">
            {es ? (
              <>
                <p>
                  Ricardo Llamas creció jugando fútbol en Las Vegas, perfeccionando su técnica en los mismos
                  campos donde hoy entrenan sus jugadores. A los <strong>16 años</strong> firmó su primer
                  contrato profesional, comenzando una carrera que lo llevaría a competir al más alto nivel en
                  cuatro continentes.
                </p>
                <p>
                  Su trayectoria como jugador atravesó <strong>Europa, Japón, Estados Unidos y México</strong>,
                  cada parada moldeando su forma de ver el juego. De la disciplina técnica europea, al ritmo y
                  estructura del fútbol japonés, a la intensidad física del juego mexicano y estadounidense —
                  Ricardo absorbió lo mejor de cada cultura futbolística.
                </p>
                <p>
                  Cuando colgó los botines, regresó a Las Vegas con una misión clara: dar a los jóvenes locales
                  el tipo de formación que él tuvo que viajar por todo el mundo para encontrar. Por
                  <strong> más de 30 años</strong> ha entrenado a fútbol juvenil, formando a cientos de
                  jugadores no solo como atletas — sino como personas con disciplina, confianza y amor por el
                  juego.
                </p>
                <p>
                  Las Vegas Soccer School es la culminación de esa misión: una academia bilingüe, accesible y
                  centrada en la comunidad, donde cada niño tiene la oportunidad de crecer dentro y fuera del
                  campo.
                </p>
              </>
            ) : (
              <>
                <p>
                  Ricardo Llamas grew up playing soccer in Las Vegas, sharpening his game on the same fields
                  where his players train today. At <strong>16</strong>, he signed his first professional
                  contract — the start of a career that would take him to the top flight on four continents.
                </p>
                <p>
                  His playing days spanned <strong>Europe, Japan, the United States, and Mexico</strong>, with
                  every stop shaping the way he reads the game. From the technical discipline of European
                  soccer, to the rhythm and structure of the Japanese game, to the physical intensity of
                  Mexican and American leagues — Ricardo absorbed the best of each soccer culture and brought
                  it home.
                </p>
                <p>
                  When he hung up his boots, he came back to Las Vegas with a clear mission: give local kids
                  the kind of foundation he had to travel the world to find. For
                  <strong> over 30 years</strong> he has been coaching youth soccer, mentoring hundreds of
                  players — not just as athletes, but as people with discipline, confidence, and a real love
                  for the game.
                </p>
                <p>
                  Las Vegas Soccer School is the culmination of that mission: a bilingual, accessible,
                  community-first academy where every kid gets the chance to grow on and off the field.
                </p>
              </>
            )}
          </div>
        </section>

        <section className="grid sm:grid-cols-3 gap-4">
          <Stat number="30+" label={es ? 'Años entrenando' : 'Years coaching'} />
          <Stat number="4" label={es ? 'Continentes jugados' : 'Continents played'} />
          <Stat number="16" label={es ? 'Años al firmar profesional' : 'Age signed professional'} />
        </section>

        <section className="bg-emerald-50 border border-emerald-200 rounded-lg p-6">
          <h2 className="text-xl font-bold text-emerald-800">
            {es ? 'Nuestra forma de entrenar' : 'How we train'}
          </h2>
          <p className="mt-2">
            {es
              ? 'Cada sesión combina lo que Ricardo aprendió a nivel profesional con un enfoque centrado en el desarrollo del jugador joven: trabajo técnico, inteligencia táctica, y formación de carácter. Entrenamos en español e inglés, en grupos pequeños, con atención individual.'
              : 'Every session blends what Ricardo learned at the professional level with a focus on youth player development — technical work, tactical IQ, and character. We coach in both Spanish and English, in small groups, with individual attention.'}
          </p>
          <div className="mt-4 flex flex-wrap gap-3 text-sm">
            <Link to="/info" className="text-emerald-700 hover:underline font-medium">
              {es ? 'Ver horario y lugar →' : 'See schedule & location →'}
            </Link>
            <Link to="/pricing" className="text-emerald-700 hover:underline font-medium">
              {es ? 'Ver precios →' : 'See pricing →'}
            </Link>
          </div>
        </section>

        <div className="text-center pt-2">
          <Link
            to={ctaTo}
            className="inline-block bg-emerald-700 text-white font-semibold px-6 py-3 rounded-md hover:bg-emerald-800 shadow"
          >
            {es ? 'Inscriba a su jugador' : 'Register your player'}
          </Link>
        </div>
      </div>
    </Layout>
  )
}

function Stat({ number, label }: { number: string; label: string }) {
  return (
    <div className="bg-white border border-slate-200 rounded-lg p-4 text-center">
      <div className="text-3xl font-extrabold text-emerald-700">{number}</div>
      <div className="text-xs uppercase tracking-wide text-slate-500 mt-1">{label}</div>
    </div>
  )
}
