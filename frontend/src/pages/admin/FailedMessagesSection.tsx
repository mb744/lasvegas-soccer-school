import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Api } from '../../api/client'
import { MESSAGE_CHANNEL_LABELS } from '../../api/types'
import type { FailedMessage } from '../../api/types'

function errMsg(e: any): string {
  return e?.response?.data?.title || e?.response?.data || e?.message || 'Error'
}

/** Settings → Failed messages. Recent recipients whose send failed — carrier/WhatsApp errors and
 *  sends we blocked because a personalization value was blank. */
export function FailedMessagesSection({ onError }: { onError: (e: string | null) => void }) {
  const { t } = useTranslation()
  const [items, setItems] = useState<FailedMessage[]>([])
  const [loading, setLoading] = useState(false)

  const load = async () => {
    setLoading(true); onError(null)
    try { setItems(await Api.listFailedMessages()) }
    catch (e: any) { onError(errMsg(e)) }
    finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <p className="text-xs text-slate-500">{t('admin.settingsFailedMessagesBlurb')}</p>
        <button onClick={load} className="text-sm text-emerald-700 hover:underline">↻</button>
      </div>

      {items.length > 0 ? (
        <table className="w-full text-xs">
          <thead>
            <tr className="text-left text-slate-500 border-b">
              <th className="py-1 pr-2">{t('admin.msgWhen')}</th>
              <th className="py-1 pr-2">{t('admin.msgChannel')}</th>
              <th className="py-1 pr-2">{t('admin.msgMember')}</th>
              <th className="py-1 pr-2">{t('admin.msgTarget')}</th>
              <th className="py-1 pr-2">{t('admin.msgStatusMessage')}</th>
            </tr>
          </thead>
          <tbody>
            {items.map(m => (
              <tr key={m.recipientId} className="border-b last:border-0 align-top">
                <td className="py-1 pr-2 whitespace-nowrap text-slate-500">{new Date(m.createdAt).toLocaleString()}</td>
                <td className="py-1 pr-2">{MESSAGE_CHANNEL_LABELS[m.channel]}</td>
                <td className="py-1 pr-2">
                  {m.name ?? '—'}
                  <div className="text-[10px] text-slate-400">{m.phone}</div>
                </td>
                <td className="py-1 pr-2 text-slate-600">{m.targetLabel ?? '—'}</td>
                <td className="py-1 pr-2 text-rose-700">
                  {m.statusMessage ?? '—'}
                  {m.errorCode && <span className="ml-1 font-mono text-slate-400">({m.errorCode})</span>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="text-xs text-slate-400">{loading ? t('admin.msgPreviewLoading') : t('admin.settingsFailedMessagesEmpty')}</p>
      )}
    </div>
  )
}
