import { useTranslation } from 'react-i18next'
import { Layout } from '../components/Layout'

const LAST_UPDATED = '2026-05-05'
const CONTACT_EMAIL = 'registration@lasvegassoccerschool.org'

export function PrivacyPage() {
  const { i18n } = useTranslation()
  const es = i18n.resolvedLanguage?.startsWith('es') ?? false

  return (
    <Layout>
      <div className="max-w-3xl mx-auto px-4 py-10 text-slate-700 leading-relaxed space-y-4">
        {es ? <SpanishBody /> : <EnglishBody />}
        <p className="text-xs text-slate-500 mt-12">
          {es ? 'Última actualización' : 'Last updated'}: {LAST_UPDATED}
        </p>
      </div>
    </Layout>
  )
}

function EnglishBody() {
  return (
    <>
      <h1 className="text-3xl font-bold text-emerald-800">Privacy Policy</h1>
      <p>
        This Privacy Policy explains what information <strong>Las Vegas Soccer School</strong> (“LVSS”, “we”, “us”) collects
        when you use our online registration application, how we use it, and your rights.
      </p>

      <h2 className="text-xl font-bold mt-6">Information we collect</h2>
      <p>When a parent or guardian creates an account and registers a player, we collect:</p>
      <ul className="list-disc pl-6 space-y-1">
        <li>Account info: your name, email address, and (optionally) phone number.</li>
        <li>If you sign in with Google or Facebook, the email address and name returned by those services.</li>
        <li>Registration info: home address, contact phone, and your preferred language.</li>
        <li>Player info: each player’s name, date of birth, school grade, uniform / shoe size, and a digital waiver
            signature provided by the parent or legal guardian.</li>
      </ul>
      <p>We do not handle payments through this application.</p>

      <h2 className="text-xl font-bold mt-6">How we use it</h2>
      <ul className="list-disc pl-6 space-y-1">
        <li>To create and operate your account.</li>
        <li>To process your player’s registration and produce a signed waiver record.</li>
        <li>To contact you about registration status and program logistics.</li>
        <li>For internal recordkeeping required by the program (e.g. emergency contact, signed waiver retention).</li>
      </ul>

      <h2 className="text-xl font-bold mt-6">Third parties</h2>
      <ul className="list-disc pl-6 space-y-1">
        <li><strong>Google / Facebook OAuth</strong> — only used if you choose to sign in with one of these services.
            We receive your email and name; those services do not see your registration data.</li>
        <li><strong>Microsoft Azure</strong> — hosts the application and database; data is encrypted at rest and in transit.</li>
        <li><strong>Azure Communication Services</strong> — used by our admin to send registration links by email or SMS,
            if you receive one.</li>
      </ul>
      <p>We do not sell or rent your information.</p>

      <h2 className="text-xl font-bold mt-6">Children’s information</h2>
      <p>
        We collect personal information about minors (your child) only as it is provided by the parent or legal guardian
        during registration. Children are not permitted to create accounts directly. If you believe a child has signed up
        without parental consent, contact us at the email below and we will delete the account promptly.
      </p>

      <h2 className="text-xl font-bold mt-6">Retention</h2>
      <p>
        We retain registration and signed waiver records for the duration of the active season plus 7 years after, for
        liability and recordkeeping purposes. You may request earlier deletion (see below), subject to the legal record
        we are required to retain for any season your player participated in.
      </p>

      <h2 className="text-xl font-bold mt-6">Your rights</h2>
      <p>You may request to:</p>
      <ul className="list-disc pl-6 space-y-1">
        <li>Access the personal information we hold about you.</li>
        <li>Correct inaccurate information.</li>
        <li>Delete your account and registrations (subject to the retention requirements above).</li>
        <li>Withdraw consent for marketing communications.</li>
      </ul>
      <p>
        To exercise any of these rights, email <a className="text-emerald-700 underline" href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a>{' '}
        and we will respond within 30 days. See also our <a className="text-emerald-700 underline" href="/data-deletion">Data Deletion</a> page.
      </p>

      <h2 className="text-xl font-bold mt-6">Contact</h2>
      <p>
        Las Vegas Soccer School<br />
        <a className="text-emerald-700 underline" href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a>
      </p>
    </>
  )
}

function SpanishBody() {
  return (
    <>
      <h1 className="text-3xl font-bold text-emerald-800">Política de privacidad</h1>
      <p>
        Esta Política de privacidad explica qué información recopila <strong>Las Vegas Soccer School</strong> (“LVSS”,
        “nosotros”) cuando usted usa nuestra aplicación de inscripción en línea, cómo la usamos y cuáles son sus derechos.
      </p>

      <h2 className="text-xl font-bold mt-6">Información que recopilamos</h2>
      <p>Cuando un padre/madre o tutor crea una cuenta e inscribe a un jugador, recopilamos:</p>
      <ul className="list-disc pl-6 space-y-1">
        <li>Información de la cuenta: su nombre, correo electrónico y, opcionalmente, número de teléfono.</li>
        <li>Si inicia sesión con Google o Facebook: el correo electrónico y nombre que esos servicios devuelven.</li>
        <li>Información de inscripción: dirección, teléfono de contacto e idioma preferido.</li>
        <li>Información del jugador: nombre, fecha de nacimiento, grado escolar, talla de uniforme/calzado y la firma
            digital de la exención proporcionada por el padre o tutor legal.</li>
      </ul>
      <p>No procesamos pagos a través de esta aplicación.</p>

      <h2 className="text-xl font-bold mt-6">Cómo usamos la información</h2>
      <ul className="list-disc pl-6 space-y-1">
        <li>Para crear y operar su cuenta.</li>
        <li>Para procesar la inscripción de su jugador y generar un registro firmado de la exención.</li>
        <li>Para contactarlo sobre el estado de la inscripción y la logística del programa.</li>
        <li>Para mantener registros internos requeridos por el programa (por ejemplo, contacto de emergencia y retención
            de exenciones firmadas).</li>
      </ul>

      <h2 className="text-xl font-bold mt-6">Terceros</h2>
      <ul className="list-disc pl-6 space-y-1">
        <li><strong>Google / Facebook OAuth</strong> — solo se usan si usted elige iniciar sesión con uno de esos servicios.
            Recibimos su correo y nombre; esos servicios no ven sus datos de inscripción.</li>
        <li><strong>Microsoft Azure</strong> — aloja la aplicación y la base de datos; los datos están cifrados en reposo y en tránsito.</li>
        <li><strong>Azure Communication Services</strong> — el administrador lo usa para enviar enlaces de inscripción por
            correo o SMS, si usted recibe uno.</li>
      </ul>
      <p>No vendemos ni alquilamos su información.</p>

      <h2 className="text-xl font-bold mt-6">Información de menores</h2>
      <p>
        Solo recopilamos información personal sobre menores (su hijo/a) cuando el padre o tutor legal la proporciona
        durante la inscripción. Los menores no pueden crear cuentas directamente. Si cree que un menor se registró sin
        consentimiento parental, contáctenos al correo abajo y eliminaremos la cuenta de inmediato.
      </p>

      <h2 className="text-xl font-bold mt-6">Conservación</h2>
      <p>
        Conservamos los registros de inscripción y exenciones firmadas durante la temporada activa más 7 años posteriores,
        por motivos de responsabilidad y mantenimiento de registros. Puede solicitar la eliminación anticipada (véase abajo),
        sujeto al registro legal que debamos conservar de cualquier temporada en la que su jugador haya participado.
      </p>

      <h2 className="text-xl font-bold mt-6">Sus derechos</h2>
      <p>Puede solicitar:</p>
      <ul className="list-disc pl-6 space-y-1">
        <li>Acceder a la información personal que tenemos sobre usted.</li>
        <li>Corregir información inexacta.</li>
        <li>Eliminar su cuenta e inscripciones (sujeto a los requisitos de conservación).</li>
        <li>Retirar el consentimiento para comunicaciones de marketing.</li>
      </ul>
      <p>
        Para ejercer cualquiera de estos derechos, escriba a{' '}
        <a className="text-emerald-700 underline" href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a>{' '}
        y responderemos dentro de 30 días. Vea también nuestra página de{' '}
        <a className="text-emerald-700 underline" href="/data-deletion">Eliminación de datos</a>.
      </p>

      <h2 className="text-xl font-bold mt-6">Contacto</h2>
      <p>
        Las Vegas Soccer School<br />
        <a className="text-emerald-700 underline" href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a>
      </p>
    </>
  )
}
