import type { Translations } from './en';

export const es: Translations = {
  appName: 'LV Soccer School',
  common: {
    retry: 'Reintentar',
    loading: 'Cargando…',
    cancel: 'Cancelar',
    send: 'Enviar',
  },
  login: {
    title: 'Iniciar sesión',
    subtitle: 'Vea su calendario, confirme asistencia y chatee con su equipo.',
    email: 'Correo electrónico',
    password: 'Contraseña',
    signIn: 'Iniciar sesión',
    error: 'Correo o contraseña inválidos.',
  },
  tabs: {
    schedule: 'Calendario',
    chat: 'Chat',
    profile: 'Perfil',
  },
  schedule: {
    title: 'Calendario',
    empty: 'No hay juegos ni prácticas próximas.',
    game: 'Juego',
    practice: 'Práctica',
    event: 'Evento',
    cancelled: 'Cancelado',
    arrive: 'Llegar',
    vs: 'vs {{opponent}}',
    home: 'Local',
    away: 'Visitante',
    uniform: 'Uniforme',
  },
  attendance: {
    going: 'Asiste',
    notGoing: 'No asiste',
    maybe: 'Tal vez',
    pending: 'Toque para confirmar',
    lockedByAdmin: 'Establecido por su entrenador — contáctelo para cambiar.',
  },
  chat: {
    title: 'Chat',
    empty: 'Aún no hay grupos. Su entrenador lo agregará a uno.',
    placeholder: 'Mensaje…',
    you: 'Usted',
    coach: 'Entrenador',
  },
  profile: {
    title: 'Perfil',
    players: 'Mis jugadores',
    noTeams: 'Sin equipo aún',
    signOut: 'Cerrar sesión',
    language: 'Idioma',
  },
};
