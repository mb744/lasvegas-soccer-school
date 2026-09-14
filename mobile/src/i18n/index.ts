import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { getLocales } from 'expo-localization';
import { en } from './en';
import { es } from './es';

// Pick Spanish when the device's primary language is Spanish; default English. Parents can't yet
// override in-app (v1) — the device locale drives it, matching the web app's auto-detection.
const deviceLang = getLocales()[0]?.languageCode === 'es' ? 'es' : 'en';

void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    es: { translation: es },
  },
  lng: deviceLang,
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
});

export default i18n;
