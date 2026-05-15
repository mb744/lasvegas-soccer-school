import { Fragment, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type {
  BroadcastDetail,
  BroadcastSummary,
  ConversationParticipant,
  DynamicGroup,
  GroupConversationDetail,
  GroupConversationSummary,
  Language,
  MessageChannel,
  MessageGroupDetail,
  MessageGroupSummary,
  MessagingConfig,
  SaveTemplateVariable,
  WhatsAppTemplate,
} from '../../api/types'
import {
  MESSAGE_CHANNEL_LABELS,
  MESSAGE_DELIVERY_LABELS,
} from '../../api/types'

type Tab = 'compose' | 'groups' | 'conversations' | 'templates' | 'history'
type RecipientMode = 'individual' | 'curated' | 'dynamic'
type SendMode = 'broadcast' | 'group-chat'
type ComposeBodyMode = 'free-form' | 'template'

export function AdminMessagingPage() {
  const { t } = useTranslation()

  // Capabilities — driven by what's configured on the server.
  const [config, setConfig] = useState<MessagingConfig | null>(null)

  // Tab state
  const [tab, setTab] = useState<Tab>('compose')

  // Shared state used by Compose + Groups + History tabs.
  const [curated, setCurated] = useState<MessageGroupSummary[]>([])
  const [dynamicGroups, setDynamicGroups] = useState<DynamicGroup[]>([])
  const [broadcasts, setBroadcasts] = useState<BroadcastSummary[]>([])
  const [conversations, setConversations] = useState<GroupConversationSummary[]>([])
  const [templates, setTemplates] = useState<WhatsAppTemplate[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const refreshConfig = async () => {
    try { setConfig(await Api.messagingConfig()) }
    catch (e: any) { setError(extractError(e)) }
  }
  const refreshGroups = async () => {
    try {
      const r = await Api.listMessagingGroups()
      setCurated(r.curated); setDynamicGroups(r.dynamic)
    } catch (e: any) { setError(extractError(e)) }
  }
  const refreshHistory = async () => {
    try {
      const [bs, cs] = await Promise.all([Api.listBroadcasts(), Api.listConversations()])
      setBroadcasts(bs); setConversations(cs)
    } catch (e: any) { setError(extractError(e)) }
  }
  const refreshTemplates = async () => {
    try { setTemplates(await Api.listWhatsAppTemplates()) }
    catch (e: any) { setError(extractError(e)) }
  }

  useEffect(() => {
    refreshConfig()
    refreshGroups()
    refreshHistory()
    refreshTemplates()
  }, [])

  const tabBtn = (key: Tab, label: string) => (
    <button
      key={key}
      onClick={() => { setTab(key); setError(null); setNotice(null) }}
      className={`px-4 py-2 text-sm font-medium border-b-2 ${tab === key
        ? 'border-emerald-700 text-emerald-800'
        : 'border-transparent text-slate-500 hover:text-slate-700'}`}
    >{label}</button>
  )

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-10 space-y-6">
        <div>
          <Link to="/admin" className="text-sm text-emerald-700 hover:underline">← {t('admin.backToHub')}</Link>
          <h1 className="text-3xl font-bold text-emerald-800 mt-2">{t('admin.messagingTitle')}</h1>
          <p className="text-sm text-slate-600 mt-1">{t('admin.messagingSubtitle')}</p>
        </div>

        {config && (
          <div className="flex flex-wrap gap-2 text-xs">
            <Capability ok={config.sms} label="SMS" />
            <Capability ok={config.whatsApp} label="WhatsApp" />
            <Capability ok={config.conversations} label={t('admin.msgGroupChat')} />
          </div>
        )}

        <div className="flex gap-1 border-b border-slate-200">
          {tabBtn('compose', t('admin.msgTabCompose'))}
          {tabBtn('groups', t('admin.msgTabGroups'))}
          {tabBtn('conversations', t('admin.msgTabConversations'))}
          {tabBtn('templates', t('admin.msgTabTemplates'))}
          {tabBtn('history', t('admin.msgTabHistory'))}
        </div>

        {error && (
          <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{error}</div>
        )}
        {notice && (
          <div className="text-sm text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-md p-3">{notice}</div>
        )}

        {tab === 'compose' && (
          <ComposeTab
            config={config}
            curated={curated}
            dynamicGroups={dynamicGroups}
            templates={templates}
            onSent={async (msg) => {
              setNotice(msg); setError(null)
              await refreshHistory()
              await refreshGroups()
            }}
            onError={(e) => { setError(e); setNotice(null) }}
          />
        )}

        {tab === 'templates' && (
          <TemplatesTab
            templates={templates}
            onChanged={refreshTemplates}
            onError={(e) => setError(e)}
            onNotice={(n) => setNotice(n)}
          />
        )}

        {tab === 'groups' && (
          <GroupsTab
            curated={curated}
            onChanged={refreshGroups}
            onError={(e) => setError(e)}
            onNotice={(n) => setNotice(n)}
          />
        )}

        {tab === 'conversations' && (
          <ConversationsTab
            conversations={conversations}
            curated={curated}
            dynamicGroups={dynamicGroups}
            config={config}
            onChanged={refreshHistory}
            onError={(e) => setError(e)}
            onNotice={(n) => setNotice(n)}
          />
        )}

        {tab === 'history' && (
          <HistoryTab
            broadcasts={broadcasts}
            onRefresh={refreshHistory}
          />
        )}
      </div>
    </Layout>
  )
}

function Capability({ ok, label }: { ok: boolean; label: string }) {
  return (
    <span className={`inline-flex items-center gap-1 px-2 py-1 rounded-full border ${ok
      ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
      : 'border-slate-200 bg-slate-50 text-slate-500'}`}>
      <span className={`inline-block w-1.5 h-1.5 rounded-full ${ok ? 'bg-emerald-500' : 'bg-slate-400'}`} />
      {label}
    </span>
  )
}

// --- Compose tab -----------------------------------------------------------

function ComposeTab({
  config, curated, dynamicGroups, templates, onSent, onError,
}: {
  config: MessagingConfig | null
  curated: MessageGroupSummary[]
  dynamicGroups: DynamicGroup[]
  templates: WhatsAppTemplate[]
  onSent: (msg: string) => void | Promise<void>
  onError: (e: string) => void
}) {
  const { t } = useTranslation()
  const [channel, setChannel] = useState<MessageChannel>(0)
  const [mode, setMode] = useState<SendMode>('broadcast')
  const [bodyMode, setBodyMode] = useState<ComposeBodyMode>('free-form')
  const [templateId, setTemplateId] = useState<number | ''>('')
  const [templateValues, setTemplateValues] = useState<Record<string, string>>({})
  const [recipientMode, setRecipientMode] = useState<RecipientMode>('individual')
  const [phone, setPhone] = useState('')
  const [name, setName] = useState('')
  const [customGroupId, setCustomGroupId] = useState<number | ''>('')
  const [dynamicKey, setDynamicKey] = useState<string>('')
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [sending, setSending] = useState(false)

  const selectedTemplate = useMemo(
    () => templates.find(t => t.id === templateId) ?? null,
    [templateId, templates])

  // Templates only apply to WhatsApp broadcasts. Reset to free-form on any other combination
  // so the UI doesn't drift into an invalid state.
  useEffect(() => {
    if (channel !== 1 || mode !== 'broadcast') {
      setBodyMode('free-form')
      setTemplateId('')
      setTemplateValues({})
    }
  }, [channel, mode])

  const channelAvailable = (c: MessageChannel) => c === 0 ? config?.sms : config?.whatsApp

  const recipientPreview = useMemo(() => {
    if (recipientMode === 'individual') return phone.trim() ? `1 recipient (${phone.trim()})` : 'No recipient yet'
    if (recipientMode === 'curated') {
      const g = curated.find(x => x.id === customGroupId)
      return g ? `${g.memberCount} recipients (${g.name})` : 'Pick a group'
    }
    const d = dynamicGroups.find(x => x.key === dynamicKey)
    return d ? `${d.count} recipients (${d.label})` : 'Pick a group'
  }, [recipientMode, phone, customGroupId, dynamicKey, curated, dynamicGroups])

  const target = () => {
    if (recipientMode === 'individual') {
      return { kind: 0 as const, phone: phone.trim(), name: name.trim() || null }
    }
    if (recipientMode === 'curated') {
      return { kind: 1 as const, customGroupId: customGroupId === '' ? null : Number(customGroupId) }
    }
    return { kind: 2 as const, dynamicGroupKey: dynamicKey }
  }

  const send = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!channelAvailable(channel)) { onError(`${MESSAGE_CHANNEL_LABELS[channel]} is not configured on this server.`); return }
    if (recipientMode === 'individual' && !phone.trim()) { onError('Enter a phone number.'); return }
    if (recipientMode === 'curated' && customGroupId === '') { onError('Pick a group.'); return }
    if (recipientMode === 'dynamic' && !dynamicKey) { onError('Pick a group.'); return }
    if (mode === 'group-chat' && !title.trim()) { onError('Group chat title is required.'); return }

    const usingTemplate = mode === 'broadcast' && channel === 1 && bodyMode === 'template'
    if (usingTemplate) {
      if (!selectedTemplate) { onError('Pick a template.'); return }
      const missing = selectedTemplate.variables
        .filter(v => !templateValues[v.position.toString()]?.trim())
        .map(v => v.label)
      if (missing.length) { onError(`Fill in: ${missing.join(', ')}.`); return }
    } else if (!body.trim()) {
      onError('Message body is required.'); return
    }

    setSending(true)
    try {
      if (mode === 'broadcast') {
        const payload = usingTemplate
          ? {
              channel,
              whatsAppTemplateId: selectedTemplate!.id,
              templateVariables: templateValues,
              target: target(),
            }
          : { channel, body: body.trim(), target: target() }
        const r = await Api.createBroadcast(payload)
        const ok = r.recipients.filter(x => x.status !== 4 && x.status !== 5).length
        const via = usingTemplate
          ? `${MESSAGE_CHANNEL_LABELS[channel]} template "${selectedTemplate!.name}"`
          : MESSAGE_CHANNEL_LABELS[channel]
        await onSent(`Sent to ${ok}/${r.recipients.length} via ${via}.`)
      } else {
        const r = await Api.createConversation({
          title: title.trim(),
          channel,
          participants: [],
          target: target(),
        })
        // After creating, send the first message into the conversation.
        if (body.trim()) await Api.sendToConversation(r.id, body.trim())
        await onSent(`Group chat "${r.title}" created with ${r.participants.length} participants.`)
      }
      setBody('')
      if (usingTemplate) setTemplateValues({})
    } catch (e: any) {
      onError(extractError(e))
    } finally {
      setSending(false)
    }
  }

  // Available WhatsApp templates (filtered by language? No — admin picks any.)
  const isWhatsApp = channel === 1
  const showTemplateMode = isWhatsApp && mode === 'broadcast'

  return (
    <form onSubmit={send} className="bg-white border border-slate-200 rounded-lg p-6 space-y-4">
      <div className="grid sm:grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgChannel')}</label>
          <div className="flex gap-2">
            {[0, 1].map(c => (
              <button key={c} type="button" disabled={!channelAvailable(c as MessageChannel)}
                onClick={() => setChannel(c as MessageChannel)}
                className={`px-3 py-2 rounded-md border text-sm ${channel === c
                  ? 'bg-emerald-700 text-white border-emerald-700'
                  : 'bg-white border-slate-300 text-slate-700'} disabled:opacity-40 disabled:cursor-not-allowed`}
              >{MESSAGE_CHANNEL_LABELS[c as MessageChannel]}</button>
            ))}
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgSendAs')}</label>
          <div className="flex gap-2">
            <button type="button" onClick={() => setMode('broadcast')}
              className={`px-3 py-2 rounded-md border text-sm ${mode === 'broadcast' ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}
            >{t('admin.msgFanOut')}</button>
            <button type="button"
              disabled={!config?.conversations}
              onClick={() => setMode('group-chat')}
              className={`px-3 py-2 rounded-md border text-sm ${mode === 'group-chat' ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'} disabled:opacity-40 disabled:cursor-not-allowed`}
            >{t('admin.msgGroupChat')}</button>
          </div>
          <p className="mt-1 text-xs text-slate-500">
            {mode === 'broadcast' ? t('admin.msgFanOutHelp') : t('admin.msgGroupChatHelp')}
          </p>
        </div>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgRecipient')}</label>
        <div className="flex flex-wrap gap-2 mb-3">
          {(['individual', 'curated', 'dynamic'] as const).map(k => (
            <button key={k} type="button"
              onClick={() => setRecipientMode(k)}
              className={`px-3 py-1.5 rounded-md border text-sm ${recipientMode === k ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}
            >{t(`admin.msgRecipient_${k}`)}</button>
          ))}
        </div>

        {recipientMode === 'individual' && (
          <div className="grid sm:grid-cols-2 gap-3">
            <input type="tel" value={phone} onChange={e => setPhone(e.target.value)}
              placeholder="+17025551212"
              className="border border-slate-300 rounded-md px-3 py-2 text-sm" />
            <input type="text" value={name} onChange={e => setName(e.target.value)}
              placeholder={t('admin.msgNameOptional')}
              className="border border-slate-300 rounded-md px-3 py-2 text-sm" />
          </div>
        )}
        {recipientMode === 'curated' && (
          <select value={customGroupId} onChange={e => setCustomGroupId(e.target.value === '' ? '' : Number(e.target.value))}
            className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full sm:w-96">
            <option value="">— {t('admin.msgPickGroup')} —</option>
            {curated.map(g => (
              <option key={g.id} value={g.id}>{g.name} ({g.memberCount})</option>
            ))}
          </select>
        )}
        {recipientMode === 'dynamic' && (
          <select value={dynamicKey} onChange={e => setDynamicKey(e.target.value)}
            className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full sm:w-96">
            <option value="">— {t('admin.msgPickGroup')} —</option>
            {dynamicGroups.map(d => (
              <option key={d.key} value={d.key}>{d.label} ({d.count})</option>
            ))}
          </select>
        )}
        <p className="mt-2 text-xs text-slate-500">{recipientPreview}</p>
      </div>

      {mode === 'group-chat' && (
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgGroupChatTitle')}</label>
          <input type="text" value={title} onChange={e => setTitle(e.target.value)}
            placeholder={t('admin.msgGroupChatTitlePh')}
            className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full sm:w-96" />
        </div>
      )}

      {showTemplateMode && (
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgBodyMode')}</label>
          <div className="flex gap-2">
            <button type="button" onClick={() => setBodyMode('free-form')}
              className={`px-3 py-2 rounded-md border text-sm ${bodyMode === 'free-form' ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}
            >{t('admin.msgFreeForm')}</button>
            <button type="button" onClick={() => setBodyMode('template')}
              className={`px-3 py-2 rounded-md border text-sm ${bodyMode === 'template' ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}
            >{t('admin.msgTemplate')}</button>
          </div>
          <p className="mt-1 text-xs text-slate-500">{t('admin.msgBodyModeHelp')}</p>
        </div>
      )}

      {bodyMode === 'template' ? (
        <div className="space-y-3">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgTemplate')}</label>
            <select value={templateId}
              onChange={e => {
                const id = e.target.value === '' ? '' : Number(e.target.value)
                setTemplateId(id)
                setTemplateValues({})
              }}
              className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full sm:w-96">
              <option value="">— {t('admin.msgPickTemplate')} —</option>
              {templates.map(tpl => (
                <option key={tpl.id} value={tpl.id}>
                  {tpl.name} ({tpl.language === 1 ? 'ES' : 'EN'})
                </option>
              ))}
            </select>
            {templates.length === 0 && (
              <p className="mt-1 text-xs text-rose-700">{t('admin.msgNoTemplates')}</p>
            )}
          </div>
          {selectedTemplate?.previewText && (
            <pre className="text-xs bg-slate-50 border border-slate-200 rounded p-2 whitespace-pre-wrap">{selectedTemplate.previewText}</pre>
          )}
          {selectedTemplate && selectedTemplate.variables.length > 0 && (
            <div className="space-y-2">
              <h3 className="text-sm font-medium text-slate-700">{t('admin.msgFillVariables')}</h3>
              {selectedTemplate.variables.map(v => {
                const key = v.position.toString()
                return (
                  <div key={v.id} className="grid grid-cols-[8rem_1fr] items-center gap-2">
                    <label className="text-sm text-slate-700">{v.label} <span className="text-slate-400">{`{{${v.position}}}`}</span></label>
                    <input type="text"
                      value={templateValues[key] ?? ''}
                      placeholder={v.example ?? ''}
                      onChange={e => setTemplateValues(prev => ({ ...prev, [key]: e.target.value }))}
                      className="border border-slate-300 rounded-md px-3 py-2 text-sm" />
                  </div>
                )
              })}
            </div>
          )}
        </div>
      ) : (
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgBody')}</label>
          <textarea rows={4} value={body} onChange={e => setBody(e.target.value)}
            maxLength={2000}
            className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full" />
          <p className="mt-1 text-xs text-slate-500">{body.length} / 2000</p>
        </div>
      )}

      <div>
        <button type="submit" disabled={sending}
          className="bg-emerald-700 text-white font-semibold px-5 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {sending ? t('admin.sending') : (mode === 'group-chat' ? t('admin.msgCreateAndSend') : t('admin.msgSend'))}
        </button>
      </div>
    </form>
  )
}

// --- Groups tab ------------------------------------------------------------

function GroupsTab({
  curated, onChanged, onError, onNotice,
}: {
  curated: MessageGroupSummary[]
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [selected, setSelected] = useState<MessageGroupDetail | null>(null)
  const [newName, setNewName] = useState('')
  const [newDescription, setNewDescription] = useState('')
  const [memberName, setMemberName] = useState('')
  const [memberPhone, setMemberPhone] = useState('')

  const loadGroup = async (id: number) => {
    try { setSelected(await Api.getMessagingGroup(id)) }
    catch (e: any) { onError(extractError(e)) }
  }

  const create = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newName.trim()) { onError('Name is required.'); return }
    try {
      const g = await Api.createMessagingGroup({ name: newName.trim(), description: newDescription.trim() || null })
      setNewName(''); setNewDescription('')
      await onChanged()
      await loadGroup(g.id)
      onNotice(`Created group "${g.name}".`)
    } catch (e: any) { onError(extractError(e)) }
  }

  const addMember = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selected) return
    if (!memberPhone.trim()) { onError('Phone is required.'); return }
    try {
      await Api.addMessagingGroupMember(selected.id, {
        name: memberName.trim() || null,
        phone: memberPhone.trim(),
      })
      setMemberName(''); setMemberPhone('')
      await loadGroup(selected.id)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  const removeMember = async (memberId: number) => {
    if (!selected) return
    try {
      await Api.removeMessagingGroupMember(selected.id, memberId)
      await loadGroup(selected.id)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  const importSeason = async () => {
    if (!selected) return
    try {
      const updated = await Api.importActiveSeasonIntoGroup(selected.id)
      setSelected(updated)
      await onChanged()
      onNotice(`Group now has ${updated.members.length} members.`)
    } catch (e: any) { onError(extractError(e)) }
  }

  const deleteGroup = async () => {
    if (!selected) return
    if (!confirm(`Delete group "${selected.name}"? Members will be removed.`)) return
    try {
      await Api.deleteMessagingGroup(selected.id)
      setSelected(null)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  return (
    <div className="grid lg:grid-cols-3 gap-4">
      <section className="bg-white border border-slate-200 rounded-lg p-4 lg:col-span-1 space-y-4">
        <div>
          <h2 className="font-bold text-emerald-800">{t('admin.msgGroupsHeader')}</h2>
          <ul className="mt-2 space-y-1">
            {curated.map(g => (
              <li key={g.id}>
                <button onClick={() => loadGroup(g.id)}
                  className={`w-full text-left px-2 py-1.5 rounded text-sm hover:bg-emerald-50 ${selected?.id === g.id ? 'bg-emerald-50 text-emerald-800 font-medium' : ''}`}>
                  {g.name} <span className="text-xs text-slate-500">({g.memberCount})</span>
                </button>
              </li>
            ))}
            {curated.length === 0 && <li className="text-sm text-slate-400">{t('admin.msgNoGroups')}</li>}
          </ul>
        </div>
        <form onSubmit={create} className="space-y-2 border-t border-slate-100 pt-3">
          <h3 className="text-sm font-medium text-slate-700">{t('admin.msgNewGroup')}</h3>
          <input type="text" value={newName} onChange={e => setNewName(e.target.value)}
            placeholder={t('admin.msgGroupName')}
            className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          <input type="text" value={newDescription} onChange={e => setNewDescription(e.target.value)}
            placeholder={t('admin.msgGroupDescOptional')}
            className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
          <button type="submit"
            className="bg-emerald-700 text-white text-sm font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800">
            {t('admin.msgCreateGroup')}
          </button>
        </form>
      </section>

      <section className="lg:col-span-2 space-y-4">
        {!selected && (
          <div className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-sm text-slate-500">
            {t('admin.msgSelectGroup')}
          </div>
        )}
        {selected && (
          <div className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="font-bold text-emerald-800">{selected.name}</h2>
                {selected.description && <p className="text-sm text-slate-600">{selected.description}</p>}
                <p className="text-xs text-slate-500 mt-1">{selected.members.length} {t('admin.msgMembers')}</p>
              </div>
              <div className="flex gap-2">
                <button onClick={importSeason}
                  className="text-sm border border-slate-300 rounded-md px-2 py-1 hover:bg-slate-50">
                  {t('admin.msgImportSeason')}
                </button>
                <button onClick={deleteGroup}
                  className="text-sm border border-rose-300 text-rose-700 rounded-md px-2 py-1 hover:bg-rose-50">
                  {t('admin.delete')}
                </button>
              </div>
            </div>
            <form onSubmit={addMember} className="grid sm:grid-cols-[1fr_1fr_auto] gap-2">
              <input type="text" value={memberName} onChange={e => setMemberName(e.target.value)}
                placeholder={t('admin.msgNameOptional')}
                className="border border-slate-300 rounded-md px-3 py-2 text-sm" />
              <input type="tel" value={memberPhone} onChange={e => setMemberPhone(e.target.value)}
                placeholder="+17025551212"
                className="border border-slate-300 rounded-md px-3 py-2 text-sm" />
              <button type="submit"
                className="bg-emerald-700 text-white text-sm font-semibold px-3 py-2 rounded-md hover:bg-emerald-800">
                {t('admin.msgAddMember')}
              </button>
            </form>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-500 border-b">
                  <th className="py-2 pr-4">{t('admin.msgMember')}</th>
                  <th className="py-2 pr-4">{t('admin.phone')}</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody>
                {selected.members.map(m => (
                  <tr key={m.id} className="border-b last:border-0">
                    <td className="py-2 pr-4">{m.name ?? '—'}</td>
                    <td className="py-2 pr-4">{m.phone}</td>
                    <td className="py-2 pr-4 text-right">
                      <button onClick={() => removeMember(m.id)}
                        className="text-rose-700 hover:underline">{t('admin.delete')}</button>
                    </td>
                  </tr>
                ))}
                {selected.members.length === 0 && (
                  <tr><td colSpan={3} className="py-4 text-center text-slate-400">—</td></tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}

// --- Conversations tab -----------------------------------------------------

function ConversationsTab({
  conversations, curated, dynamicGroups, config, onChanged, onError, onNotice,
}: {
  conversations: GroupConversationSummary[]
  curated: MessageGroupSummary[]
  dynamicGroups: DynamicGroup[]
  config: MessagingConfig | null
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [selected, setSelected] = useState<GroupConversationDetail | null>(null)
  const [reply, setReply] = useState('')

  const loadConversation = async (id: number) => {
    try { setSelected(await Api.getConversation(id)) }
    catch (e: any) { onError(extractError(e)) }
  }

  const send = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selected) return
    if (!reply.trim()) return
    try {
      await Api.sendToConversation(selected.id, reply.trim())
      setReply('')
      onNotice('Sent.')
    } catch (e: any) { onError(extractError(e)) }
  }

  const removeParticipant = async (participantId: number) => {
    if (!selected) return
    try {
      await Api.removeConversationParticipant(selected.id, participantId)
      await loadConversation(selected.id)
    } catch (e: any) { onError(extractError(e)) }
  }

  const remove = async () => {
    if (!selected) return
    if (!confirm(`Delete group chat "${selected.title}"? This deletes the Twilio conversation too.`)) return
    try {
      await Api.deleteConversation(selected.id)
      setSelected(null)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  if (!config?.conversations) {
    return (
      <div className="bg-white border border-dashed border-slate-300 rounded-lg p-6 text-sm text-slate-500">
        {t('admin.msgConversationsUnavailable')}
      </div>
    )
  }

  // Suppress unused-var warnings for now — curated/dynamicGroups are wired to ComposeTab,
  // but we keep them here so future "add existing group to conversation" can reuse them.
  void curated; void dynamicGroups

  return (
    <div className="grid lg:grid-cols-3 gap-4">
      <section className="bg-white border border-slate-200 rounded-lg p-4 lg:col-span-1">
        <h2 className="font-bold text-emerald-800">{t('admin.msgConversationsHeader')}</h2>
        <ul className="mt-2 space-y-1">
          {conversations.map(c => (
            <li key={c.id}>
              <button onClick={() => loadConversation(c.id)}
                className={`w-full text-left px-2 py-1.5 rounded text-sm hover:bg-emerald-50 ${selected?.id === c.id ? 'bg-emerald-50 text-emerald-800 font-medium' : ''}`}>
                <div>{c.title}</div>
                <div className="text-xs text-slate-500">
                  {MESSAGE_CHANNEL_LABELS[c.channel]} · {c.participantCount} {t('admin.msgMembers')}
                </div>
              </button>
            </li>
          ))}
          {conversations.length === 0 && <li className="text-sm text-slate-400">{t('admin.msgNoConversations')}</li>}
        </ul>
        <p className="text-xs text-slate-500 mt-3">{t('admin.msgConversationsHint')}</p>
      </section>

      <section className="lg:col-span-2 space-y-4">
        {!selected && (
          <div className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-sm text-slate-500">
            {t('admin.msgSelectConversation')}
          </div>
        )}
        {selected && (
          <div className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="font-bold text-emerald-800">{selected.title}</h2>
                <p className="text-xs text-slate-500">{MESSAGE_CHANNEL_LABELS[selected.channel]} · {selected.twilioConversationSid}</p>
              </div>
              <button onClick={remove}
                className="text-sm border border-rose-300 text-rose-700 rounded-md px-2 py-1 hover:bg-rose-50">
                {t('admin.delete')}
              </button>
            </div>

            <form onSubmit={send} className="flex gap-2">
              <input type="text" value={reply} onChange={e => setReply(e.target.value)}
                placeholder={t('admin.msgReplyPlaceholder')}
                className="flex-1 border border-slate-300 rounded-md px-3 py-2 text-sm" />
              <button type="submit"
                className="bg-emerald-700 text-white text-sm font-semibold px-3 py-2 rounded-md hover:bg-emerald-800">
                {t('admin.msgSend')}
              </button>
            </form>

            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-500 border-b">
                  <th className="py-2 pr-4">{t('admin.msgMember')}</th>
                  <th className="py-2 pr-4">{t('admin.phone')}</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody>
                {selected.participants.map(p => (
                  <tr key={p.id} className="border-b last:border-0">
                    <td className="py-2 pr-4">{p.name ?? '—'}</td>
                    <td className="py-2 pr-4">{p.phone}</td>
                    <td className="py-2 pr-4 text-right">
                      <button onClick={() => removeParticipant(p.id)}
                        className="text-rose-700 hover:underline">{t('admin.msgRemove')}</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}

// --- History tab -----------------------------------------------------------

function HistoryTab({
  broadcasts, onRefresh,
}: {
  broadcasts: BroadcastSummary[]
  onRefresh: () => void | Promise<void>
}) {
  const { t } = useTranslation()
  const [expandedId, setExpandedId] = useState<number | null>(null)
  const [expanded, setExpanded] = useState<BroadcastDetail | null>(null)

  const toggle = async (id: number) => {
    if (expandedId === id) { setExpandedId(null); setExpanded(null); return }
    try {
      setExpanded(await Api.getBroadcast(id))
      setExpandedId(id)
    } catch (e: any) { /* swallow */ void e }
  }

  return (
    <section className="bg-white border border-slate-200 rounded-lg p-4">
      <div className="flex items-center justify-between">
        <h2 className="font-bold text-emerald-800">{t('admin.msgHistoryHeader')}</h2>
        <button onClick={onRefresh} className="text-sm text-emerald-700 hover:underline">↻</button>
      </div>
      <table className="w-full text-sm mt-3">
        <thead>
          <tr className="text-left text-slate-500 border-b">
            <th className="py-2 pr-4">{t('admin.msgWhen')}</th>
            <th className="py-2 pr-4">{t('admin.msgChannel')}</th>
            <th className="py-2 pr-4">{t('admin.msgTarget')}</th>
            <th className="py-2 pr-4">{t('admin.msgDelivery')}</th>
            <th className="py-2 pr-4">{t('admin.msgBody')}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {broadcasts.map(b => (
            <Fragment key={b.id}>
              <tr className="border-b last:border-0 align-top">
                <td className="py-2 pr-4 whitespace-nowrap text-slate-500">{new Date(b.createdAt).toLocaleString()}</td>
                <td className="py-2 pr-4">{MESSAGE_CHANNEL_LABELS[b.channel]}</td>
                <td className="py-2 pr-4">{b.targetLabel ?? '—'}</td>
                <td className="py-2 pr-4 text-xs">
                  <span className="text-emerald-700">✓{b.delivered}</span>{' '}
                  <span className="text-slate-500">…{b.queued}</span>{' '}
                  <span className="text-rose-700">✕{b.failed}</span>{' '}
                  <span className="text-slate-400">/{b.total}</span>
                </td>
                <td className="py-2 pr-4 max-w-md truncate" title={b.body}>{b.body}</td>
                <td className="py-2 pr-4">
                  <button onClick={() => toggle(b.id)} className="text-emerald-700 hover:underline">
                    {expandedId === b.id ? t('admin.hide') : t('admin.details')}
                  </button>
                </td>
              </tr>
              {expandedId === b.id && expanded && (
                <tr>
                  <td colSpan={6} className="py-2 pr-4 bg-slate-50">
                    <table className="w-full text-xs">
                      <thead>
                        <tr className="text-left text-slate-500">
                          <th className="py-1 pr-4">{t('admin.msgMember')}</th>
                          <th className="py-1 pr-4">{t('admin.phone')}</th>
                          <th className="py-1 pr-4">{t('admin.status')}</th>
                          <th className="py-1 pr-4">{t('admin.msgStatusMessage')}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {expanded.recipients.map(r => (
                          <tr key={r.id}>
                            <td className="py-1 pr-4">{r.name ?? '—'}</td>
                            <td className="py-1 pr-4">{r.phone}</td>
                            <td className="py-1 pr-4">{MESSAGE_DELIVERY_LABELS[r.status]}</td>
                            <td className="py-1 pr-4 text-slate-500">{r.statusMessage ?? ''}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </td>
                </tr>
              )}
            </Fragment>
          ))}
          {broadcasts.length === 0 && (
            <tr><td colSpan={6} className="py-4 text-center text-slate-400">—</td></tr>
          )}
        </tbody>
      </table>
    </section>
  )
}

// --- Templates tab ---------------------------------------------------------

function TemplatesTab({
  templates, onChanged, onError, onNotice,
}: {
  templates: WhatsAppTemplate[]
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [editingId, setEditingId] = useState<number | 'new' | null>(null)
  const [name, setName] = useState('')
  const [contentSid, setContentSid] = useState('')
  const [language, setLanguage] = useState<Language>(0)
  const [description, setDescription] = useState('')
  const [previewText, setPreviewText] = useState('')
  const [vars, setVars] = useState<SaveTemplateVariable[]>([])

  const loadForm = (tpl: WhatsAppTemplate | null) => {
    if (tpl) {
      setEditingId(tpl.id)
      setName(tpl.name); setContentSid(tpl.contentSid); setLanguage(tpl.language)
      setDescription(tpl.description ?? ''); setPreviewText(tpl.previewText ?? '')
      setVars(tpl.variables.map(v => ({ position: v.position, label: v.label, example: v.example })))
    } else {
      setEditingId('new')
      setName(''); setContentSid(''); setLanguage(0)
      setDescription(''); setPreviewText(''); setVars([])
    }
  }

  const addVar = () => {
    const next = vars.length === 0 ? 1 : Math.max(...vars.map(v => v.position)) + 1
    setVars(prev => [...prev, { position: next, label: '', example: '' }])
  }
  const updateVar = (idx: number, patch: Partial<SaveTemplateVariable>) =>
    setVars(prev => prev.map((v, i) => i === idx ? { ...v, ...patch } : v))
  const removeVar = (idx: number) =>
    setVars(prev => prev.filter((_, i) => i !== idx))

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!name.trim()) { onError('Name is required.'); return }
    if (!contentSid.trim().startsWith('HX')) { onError('ContentSid must start with HX.'); return }
    const payload = {
      name: name.trim(),
      contentSid: contentSid.trim(),
      language,
      description: description.trim() || null,
      previewText: previewText.trim() || null,
      variables: vars.filter(v => v.label.trim()),
    }
    try {
      if (editingId === 'new' || editingId === null) {
        await Api.createWhatsAppTemplate(payload)
        onNotice(`Created template "${payload.name}".`)
      } else {
        await Api.updateWhatsAppTemplate(editingId, payload)
        onNotice(`Updated template "${payload.name}".`)
      }
      setEditingId(null)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  const remove = async () => {
    if (typeof editingId !== 'number') return
    if (!confirm(`Delete template "${name}"? Past broadcasts are unaffected.`)) return
    try {
      await Api.deleteWhatsAppTemplate(editingId)
      setEditingId(null)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  return (
    <div className="grid lg:grid-cols-3 gap-4">
      <section className="bg-white border border-slate-200 rounded-lg p-4 lg:col-span-1 space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="font-bold text-emerald-800">{t('admin.msgTemplatesHeader')}</h2>
          <button onClick={() => loadForm(null)}
            className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgNewTemplate')}</button>
        </div>
        <ul className="space-y-1">
          {templates.map(tpl => (
            <li key={tpl.id}>
              <button onClick={() => loadForm(tpl)}
                className={`w-full text-left px-2 py-1.5 rounded text-sm hover:bg-emerald-50 ${editingId === tpl.id ? 'bg-emerald-50 text-emerald-800 font-medium' : ''}`}>
                <div>{tpl.name} <span className="text-xs text-slate-400">{tpl.language === 1 ? 'ES' : 'EN'}</span></div>
                <div className="text-xs text-slate-500 font-mono truncate">{tpl.contentSid}</div>
              </button>
            </li>
          ))}
          {templates.length === 0 && <li className="text-sm text-slate-400">{t('admin.msgNoTemplates')}</li>}
        </ul>
        <p className="text-xs text-slate-500">{t('admin.msgTemplatesHint')}</p>
      </section>

      <section className="lg:col-span-2">
        {editingId === null && (
          <div className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-sm text-slate-500">
            {t('admin.msgSelectTemplate')}
          </div>
        )}
        {editingId !== null && (
          <form onSubmit={save} className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
            <div className="grid sm:grid-cols-2 gap-3">
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.msgTemplateName')}</span>
                <input type="text" value={name} onChange={e => setName(e.target.value)}
                  placeholder="practice_today"
                  className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
              </label>
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.msgTemplateContentSid')}</span>
                <input type="text" value={contentSid} onChange={e => setContentSid(e.target.value)}
                  placeholder="HX..."
                  className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm font-mono" />
              </label>
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.language')}</span>
                <select value={language} onChange={e => setLanguage(Number(e.target.value) as Language)}
                  className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
                  <option value={0}>English</option>
                  <option value={1}>Español</option>
                </select>
              </label>
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.msgTemplateDescription')}</span>
                <input type="text" value={description} onChange={e => setDescription(e.target.value)}
                  className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
              </label>
            </div>

            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgTemplatePreview')}</span>
              <textarea rows={3} value={previewText} onChange={e => setPreviewText(e.target.value)}
                placeholder={t('admin.msgTemplatePreviewPh')}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
              <span className="block text-xs text-slate-500 mt-1">{t('admin.msgTemplatePreviewHelp')}</span>
            </label>

            <div>
              <div className="flex items-center justify-between mb-2">
                <h3 className="text-sm font-medium text-slate-700">{t('admin.msgTemplateVariables')}</h3>
                <button type="button" onClick={addVar}
                  className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgTemplateAddVariable')}</button>
              </div>
              <p className="text-xs text-slate-500 mb-2">{t('admin.msgTemplateVariablesHelp')}</p>
              {vars.length === 0 && <p className="text-xs text-slate-400">{t('admin.msgTemplateNoVariables')}</p>}
              {vars.map((v, idx) => (
                <div key={idx} className="grid grid-cols-[5rem_1fr_1fr_2rem] items-center gap-2 mb-1">
                  <input type="number" min={1} value={v.position}
                    onChange={e => updateVar(idx, { position: Number(e.target.value) })}
                    className="border border-slate-300 rounded-md px-2 py-1 text-sm" />
                  <input type="text" value={v.label} placeholder={t('admin.msgTemplateVarLabel')}
                    onChange={e => updateVar(idx, { label: e.target.value })}
                    className="border border-slate-300 rounded-md px-2 py-1 text-sm" />
                  <input type="text" value={v.example ?? ''} placeholder={t('admin.msgTemplateVarExample')}
                    onChange={e => updateVar(idx, { example: e.target.value })}
                    className="border border-slate-300 rounded-md px-2 py-1 text-sm" />
                  <button type="button" onClick={() => removeVar(idx)}
                    className="text-rose-700 text-sm">✕</button>
                </div>
              ))}
            </div>

            <div className="flex items-center gap-3 pt-2 border-t border-slate-100">
              <button type="submit"
                className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
                {editingId === 'new' ? t('admin.msgCreateGroup') : t('admin.msgSave')}
              </button>
              {typeof editingId === 'number' && (
                <button type="button" onClick={remove}
                  className="text-sm border border-rose-300 text-rose-700 rounded-md px-3 py-1.5 hover:bg-rose-50">
                  {t('admin.delete')}
                </button>
              )}
              <button type="button" onClick={() => setEditingId(null)}
                className="text-sm text-slate-600 hover:underline ml-auto">{t('admin.msgCancel')}</button>
            </div>
          </form>
        )}
      </section>
    </div>
  )
}

function extractError(e: any): string {
  const status = e?.response?.status
  if (status === 401) return 'Session expired. Please reload and sign in again.'
  if (status === 403) return 'Admin role required.'
  if (e?.code === 'ERR_NETWORK') return 'Cannot reach the API.'
  return e?.response?.data?.title ?? e?.response?.data ?? e?.message ?? 'Error'
}
