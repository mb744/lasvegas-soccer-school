import { useTranslation } from 'react-i18next'

export function LanguageToggle() {
  const { i18n } = useTranslation()
  const current = i18n.resolvedLanguage ?? 'en'

  const set = (lng: 'en' | 'es') => {
    i18n.changeLanguage(lng)
  }

  const btn = (lng: 'en' | 'es', label: string) => (
    <button
      type="button"
      onClick={() => set(lng)}
      className={`px-3 py-1 text-sm rounded-md font-medium transition ${
        current.startsWith(lng)
          ? 'bg-emerald-700 text-white'
          : 'bg-white text-emerald-800 border border-emerald-200 hover:bg-emerald-50'
      }`}
    >
      {label}
    </button>
  )

  return (
    <div className="flex gap-2">
      {btn('en', 'EN')}
      {btn('es', 'ES')}
    </div>
  )
}
