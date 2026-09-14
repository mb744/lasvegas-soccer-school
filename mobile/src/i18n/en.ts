export const en = {
  appName: 'LV Soccer School',
  common: {
    retry: 'Retry',
    loading: 'Loading…',
    cancel: 'Cancel',
    send: 'Send',
  },
  login: {
    title: 'Sign in',
    subtitle: 'See your schedule, confirm attendance, and chat with your team.',
    email: 'Email',
    password: 'Password',
    signIn: 'Sign in',
    error: 'Invalid email or password.',
  },
  tabs: {
    schedule: 'Schedule',
    chat: 'Chat',
    profile: 'Profile',
  },
  schedule: {
    title: 'Schedule',
    empty: 'No upcoming games or practices.',
    game: 'Game',
    practice: 'Practice',
    event: 'Event',
    cancelled: 'Cancelled',
    arrive: 'Arrive',
    vs: 'vs {{opponent}}',
    home: 'Home',
    away: 'Away',
    uniform: 'Uniform',
  },
  attendance: {
    going: 'Going',
    notGoing: 'Not going',
    maybe: 'Maybe',
    pending: 'Tap to confirm',
    lockedByAdmin: 'Set by your coach — contact them to change.',
  },
  chat: {
    title: 'Chat',
    empty: 'No chat groups yet. Your coach will add you to one.',
    placeholder: 'Message…',
    you: 'You',
    coach: 'Coach',
  },
  profile: {
    title: 'Profile',
    players: 'My players',
    noTeams: 'No team yet',
    signOut: 'Sign out',
    language: 'Language',
  },
};

export type Translations = typeof en;
