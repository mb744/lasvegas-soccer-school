import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../../api/client'
import type { MobileAppSettings } from '../../api/types'

/** Settings → Mobile app: the store version the app compares against, and the minimum version
 *  that forces older installs to update. */
export function MobileAppSettingsSection({
  onError,
  onNotice,
}: {
  onError: (e: string | null) => void
  onNotice: (n: string | null) => void
}) {
  const { t } = useTranslation()
  const [settings, setSettings] = useState<MobileAppSettings | null>(null)
  const [minimum, setMinimum] = useState('')
  const [saving, setSaving] = useState(false)

  // Load once. (The parent passes a new onError each render, so it can't be a dependency.)
  useEffect(() => {
    Api.mobileAppSettings()
      .then(s => { setSettings(s); setMinimum(s.minimumVersion ?? '') })
      .catch((e: any) => onError(e?.response?.data ?? e?.message ?? 'Error'))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const save = async (value: string) => {
    setSaving(true)
    onError(null)
    try {
      const s = await Api.saveMobileAppSettings({ minimumVersion: value.trim() || null })
      setSettings(s)
      setMinimum(s.minimumVersion ?? '')
      onNotice(t('admin.mobileAppSaved'))
    } catch (e: any) {
      onError(typeof e?.response?.data === 'string' ? e.response.data : e?.message ?? 'Error')
    } finally {
      setSaving(false)
    }
  }

  if (!settings) return <div className="text-sm text-slate-500">{t('common.loading')}</div>

  return (
    <div className="space-y-5">
      <div>
        <h3 className="font-semibold text-slate-800">{t('admin.mobileAppStoreTitle')}</h3>
        <p className="text-sm text-slate-600 mt-1">
          {settings.appStoreVersion
            ? t('admin.mobileAppStoreVersion', { version: settings.appStoreVersion })
            : t('admin.mobileAppStoreUnknown')}
        </p>
        <p className="text-xs text-slate-500 mt-1">{t('admin.mobileAppStoreHelp')}</p>
      </div>

      <div>
        <h3 className="font-semibold text-slate-800">{t('admin.mobileAppMinimumTitle')}</h3>
        <p className="text-xs text-slate-500 mt-1">{t('admin.mobileAppMinimumHelp')}</p>
        <div className="mt-2 flex flex-wrap items-end gap-3">
          <label className="block text-sm">
            <span className="text-slate-700">{t('admin.mobileAppMinimumLabel')}</span>
            <input
              value={minimum}
              onChange={e => setMinimum(e.target.value)}
              placeholder="1.4.0"
              className="mt-1 w-32 border border-slate-300 rounded-md px-2 py-1 text-sm"
            />
          </label>
          <button
            onClick={() => save(minimum)}
            disabled={saving}
            className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60"
          >
            {t('admin.mobileAppSave')}
          </button>
          {settings.minimumVersion && (
            <button onClick={() => save('')} disabled={saving} className="text-sm text-slate-600 hover:underline">
              {t('admin.mobileAppClear')}
            </button>
          )}
        </div>
        {settings.minimumVersion && settings.appStoreVersion && compare(settings.minimumVersion, settings.appStoreVersion) > 0 && (
          <p className="text-sm text-rose-700 mt-2">{t('admin.mobileAppMinimumAboveStore')}</p>
        )}
      </div>
    </div>
  )
}

/** Numeric dotted-version compare: 1.10.0 > 1.9.2. */
function compare(a: string, b: string): number {
  const pa = a.split('.').map(Number)
  const pb = b.split('.').map(Number)
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const d = (pa[i] ?? 0) - (pb[i] ?? 0)
    if (d !== 0) return d
  }
  return 0
}
