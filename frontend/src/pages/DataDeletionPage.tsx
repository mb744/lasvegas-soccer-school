import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'

const CONTACT_EMAIL = 'registration@lasvegassoccerschool.org'

export function DataDeletionPage() {
  const { i18n } = useTranslation()
  const es = i18n.resolvedLanguage?.startsWith('es') ?? false

  return (
    <Layout>
      <div className="max-w-3xl mx-auto px-4 py-10 text-slate-700 leading-relaxed space-y-4">
        {es ? <SpanishBody /> : <EnglishBody />}
      </div>
    </Layout>
  )
}

function EnglishBody() {
  return (
    <>
      <h1 className="text-3xl font-bold text-emerald-800">Data Deletion</h1>
      <p>
        If you would like us to delete your account and your player’s registration data, send an email to{' '}
        <a className="text-emerald-700 underline" href={`mailto:${CONTACT_EMAIL}?subject=Data%20Deletion%20Request`}>
          {CONTACT_EMAIL}
        </a>{' '}
        with the subject line <strong>“Data Deletion Request”</strong> and include the email address you used to sign up.
      </p>

      <h2 className="text-xl font-bold mt-6">What happens next</h2>
      <ol className="list-decimal pl-6 space-y-1">
        <li>We confirm receipt within 3 business days.</li>
        <li>We delete your account, your player profiles, and any registrations within 30 days.</li>
        <li>We email you when the deletion is complete.</li>
      </ol>

      <h2 className="text-xl font-bold mt-6">What we may need to retain</h2>
      <p>
        For any season in which your player participated, we are required to retain the signed waiver and a minimal
        registration record for liability purposes. The retention period is described in our{' '}
        <a className="text-emerald-700 underline" href="/privacy">Privacy Policy</a>.
      </p>

      <h2 className="text-xl font-bold mt-6">Facebook Login</h2>
      <p>
        You can also revoke this app’s access to your Facebook account at any time via{' '}
        <strong>Facebook → Settings & privacy → Apps and Websites</strong>. Doing so disconnects sign-in with Facebook
        but does not delete the registration records you submitted; for that, please use the email request above.
      </p>
    </>
  )
}

function SpanishBody() {
  return (
    <>
      <h1 className="text-3xl font-bold text-emerald-800">Eliminación de datos</h1>
      <p>
        Si desea que eliminemos su cuenta y los datos de inscripción de su jugador, envíe un correo electrónico a{' '}
        <a className="text-emerald-700 underline" href={`mailto:${CONTACT_EMAIL}?subject=Solicitud%20de%20eliminaci%C3%B3n%20de%20datos`}>
          {CONTACT_EMAIL}
        </a>{' '}
        con el asunto <strong>“Solicitud de eliminación de datos”</strong> e incluya el correo electrónico que usó para registrarse.
      </p>

      <h2 className="text-xl font-bold mt-6">Qué sucede a continuación</h2>
      <ol className="list-decimal pl-6 space-y-1">
        <li>Confirmamos la recepción dentro de 3 días hábiles.</li>
        <li>Eliminamos su cuenta, los perfiles de sus jugadores y cualquier inscripción dentro de 30 días.</li>
        <li>Le enviamos un correo cuando se complete la eliminación.</li>
      </ol>

      <h2 className="text-xl font-bold mt-6">Lo que podríamos necesitar conservar</h2>
      <p>
        Para cualquier temporada en la que su jugador haya participado, debemos conservar la exención firmada y un
        registro mínimo de inscripción por motivos de responsabilidad. El período de conservación se describe en nuestra{' '}
        <a className="text-emerald-700 underline" href="/privacy">Política de privacidad</a>.
      </p>

      <h2 className="text-xl font-bold mt-6">Inicio de sesión con Facebook</h2>
      <p>
        También puede revocar el acceso de esta aplicación a su cuenta de Facebook en cualquier momento desde{' '}
        <strong>Facebook → Configuración y privacidad → Aplicaciones y sitios web</strong>. Esto desconecta el inicio de
        sesión con Facebook pero no elimina los registros de inscripción que usted envió; para ello, use la solicitud
        por correo arriba.
      </p>
    </>
  )
}
