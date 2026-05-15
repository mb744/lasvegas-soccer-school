import { Fragment, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Layout } from '../../components/Layout'
import { Api } from '../../api/client'
import type {
  AdHocRecipient,
  BroadcastDetail,
  BroadcastSummary,
  DynamicGroup,
  GroupConversationDetail,
  GroupConversationSummary,
  Language,
  MessageChannel,
  MessageGroupDetail,
  MessageGroupSummary,
  MessagingConfig,
  PhraseTranslation,
  SaveTemplateVariable,
  ScheduledGame,
  TeamDetail,
  TeamSummary,
  WhatsAppTemplate,
} from '../../api/types'

// Soccer-school-specific business strings for the practice_or_game template's "wear" variable.
// Keyed by template language (0 = EN, 1 = ES). The user's spec: shorts stays untranslated in
// Spanish (local convention at LVSS). Edit here if uniform colors change.
const WEAR_HOME: Record<Language, string> = {
  0: 'white jersey, blue shorts, blue socks',
  1: 'camisa blanca, shorts azules y medias azules',
}
const WEAR_AWAY: Record<Language, string> = {
  0: 'all blue',
  1: 'todo azul',
}
const GAME_VS_PREFIX: Record<Language, string> = { 0: 'Game vs', 1: 'Partido vs' }
const PRACTICE_FALLBACK: Record<Language, string> = { 0: 'Practice', 1: 'Práctica' }
import {
  MESSAGE_CHANNEL_LABELS,
  MESSAGE_DELIVERY_LABELS,
} from '../../api/types'

type Tab = 'compose' | 'groups' | 'conversations' | 'templates' | 'teams' | 'dictionary' | 'history'
type RecipientMode = 'individual' | 'curated' | 'dynamic' | 'list'
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
  const [teams, setTeams] = useState<TeamSummary[]>([])
  const [upcomingGames, setUpcomingGames] = useState<ScheduledGame[]>([])
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
  const refreshTeams = async () => {
    try { setTeams(await Api.listTeams()) }
    catch (e: any) { setError(extractError(e)) }
  }
  const refreshUpcomingGames = async () => {
    try { setUpcomingGames(await Api.listUpcomingGames(30)) }
    catch (e: any) { setError(extractError(e)) }
  }

  useEffect(() => {
    refreshConfig()
    refreshGroups()
    refreshHistory()
    refreshTemplates()
    refreshTeams()
    refreshUpcomingGames()
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
          {tabBtn('teams', t('admin.msgTabTeams'))}
          {tabBtn('dictionary', t('admin.msgTabDictionary'))}
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
            upcomingGames={upcomingGames}
            onSent={async (msg) => {
              setNotice(msg); setError(null)
              await refreshHistory()
              await refreshGroups()
            }}
            onError={(e) => { setError(e); setNotice(null) }}
          />
        )}

        {tab === 'teams' && (
          <TeamsTab
            teams={teams}
            curated={curated}
            onChanged={async () => { await refreshTeams(); await refreshUpcomingGames() }}
            onError={(e) => setError(e)}
            onNotice={(n) => setNotice(n)}
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

        {tab === 'dictionary' && (
          <DictionaryTab
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
  config, curated, dynamicGroups, templates, upcomingGames, onSent, onError,
}: {
  config: MessagingConfig | null
  curated: MessageGroupSummary[]
  dynamicGroups: DynamicGroup[]
  templates: WhatsAppTemplate[]
  upcomingGames: ScheduledGame[]
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
  const [listRaw, setListRaw] = useState('')
  const parsedList = useMemo(() => parseRecipientList(listRaw), [listRaw])
  const [title, setTitle] = useState('')
  const [bodyEn, setBodyEn] = useState('')
  const [bodyEs, setBodyEs] = useState('')
  const [defaultLang, setDefaultLang] = useState<Language>(0)
  const [previewStep, setPreviewStep] = useState<'edit' | 'confirm' | null>(null)
  const [templatePreviewOpen, setTemplatePreviewOpen] = useState(false)
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
    if (recipientMode === 'list') {
      return parsedList.length === 0 ? 'No phones parsed yet' : `${parsedList.length} recipients`
    }
    const d = dynamicGroups.find(x => x.key === dynamicKey)
    return d ? `${d.count} recipients (${d.label})` : 'Pick a group'
  }, [recipientMode, phone, customGroupId, dynamicKey, curated, dynamicGroups, parsedList])

  const target = () => {
    if (recipientMode === 'individual') {
      return { kind: 0 as const, phone: phone.trim(), name: name.trim() || null }
    }
    if (recipientMode === 'curated') {
      return { kind: 1 as const, customGroupId: customGroupId === '' ? null : Number(customGroupId) }
    }
    if (recipientMode === 'list') {
      return { kind: 3 as const, recipients: parsedList }
    }
    return { kind: 2 as const, dynamicGroupKey: dynamicKey }
  }

  /** Final validation that's shared between the "send straight" paths and the bilingual modal. */
  const validate = (): string | null => {
    if (!channelAvailable(channel)) return `${MESSAGE_CHANNEL_LABELS[channel]} is not configured on this server.`
    if (recipientMode === 'individual' && !phone.trim()) return 'Enter a phone number.'
    if (recipientMode === 'curated' && customGroupId === '') return 'Pick a group.'
    if (recipientMode === 'dynamic' && !dynamicKey) return 'Pick a group.'
    if (recipientMode === 'list' && parsedList.length === 0) return 'Paste at least one phone number.'
    if (mode === 'group-chat' && !title.trim()) return 'Group chat title is required.'
    return null
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    const err = validate()
    if (err) { onError(err); return }

    const usingTemplate = mode === 'broadcast' && channel === 1 && bodyMode === 'template'
    if (usingTemplate) {
      if (!selectedTemplate) { onError('Pick a template.'); return }
      const missing = selectedTemplate.variables
        .filter(v => !templateValues[v.label]?.trim())
        .map(v => v.label)
      if (missing.length) { onError(`Fill in: ${missing.join(', ')}.`); return }
      // Show the rendered template preview before firing the send so admin can verify the
      // variable substitution one last time.
      setTemplatePreviewOpen(true)
      return
    }

    // Free-form: require at least the EN side, then open the bilingual preview gate. Group-chat
    // skips the preview because Conversations only sends one body anyway.
    if (!bodyEn.trim() && !bodyEs.trim()) { onError('Message body is required.'); return }
    if (mode === 'group-chat') {
      await sendNow({ usingTemplate: false })
      return
    }
    setPreviewStep('edit')
  }

  const sendNow = async (args: { usingTemplate: boolean }) => {
    setSending(true)
    try {
      if (mode === 'broadcast') {
        const payload = args.usingTemplate
          ? {
              channel,
              whatsAppTemplateId: selectedTemplate!.id,
              templateVariables: templateValues,
              target: target(),
            }
          : {
              channel,
              bodyEn: bodyEn.trim() || null,
              bodyEs: bodyEs.trim() || null,
              defaultLanguage: defaultLang,
              target: target(),
            }
        const r = await Api.createBroadcast(payload)
        const ok = r.recipients.filter(x => x.status !== 4 && x.status !== 5).length
        const via = args.usingTemplate
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
        // After creating, send the first message into the conversation (English side wins for
        // group chats since Conversations is single-thread).
        const initial = bodyEn.trim() || bodyEs.trim()
        if (initial) await Api.sendToConversation(r.id, initial)
        await onSent(`Group chat "${r.title}" created with ${r.participants.length} participants.`)
      }
      setBodyEn(''); setBodyEs(''); setPreviewStep(null); setTemplatePreviewOpen(false)
      if (args.usingTemplate) setTemplateValues({})
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
    <>
    <form onSubmit={handleSubmit} className="bg-white border border-slate-200 rounded-lg p-6 space-y-4">
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
          {(['individual', 'list', 'curated', 'dynamic'] as const).map(k => (
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
        {recipientMode === 'list' && (
          <div className="space-y-2">
            <textarea rows={6} value={listRaw} onChange={e => setListRaw(e.target.value)}
              placeholder={t('admin.msgListPlaceholder')}
              className="border border-slate-300 rounded-md px-3 py-2 text-sm font-mono w-full" />
            <p className="text-xs text-slate-500">{t('admin.msgListHelp')}</p>
            {parsedList.length > 0 && (
              <details className="text-xs">
                <summary className="cursor-pointer text-emerald-700 hover:underline">{t('admin.msgListPreview', { count: parsedList.length })}</summary>
                <ul className="mt-1 space-y-0.5 text-slate-600">
                  {parsedList.map((r, i) => (
                    <li key={i}>{r.name ? <span className="text-slate-800">{r.name}</span> : null} <span className="font-mono">{r.phone}</span></li>
                  ))}
                </ul>
              </details>
            )}
          </div>
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
          {selectedTemplate && upcomingGames.length > 0 && (
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgPickGame')}</label>
              <select value=""
                onChange={e => {
                  const gameId = Number(e.target.value)
                  if (!gameId) return
                  const g = upcomingGames.find(x => x.id === gameId)
                  if (!g || !selectedTemplate) return
                  applyGameToTemplate(g, selectedTemplate, setTemplateValues)
                  // Auto-target the team's linked group if there is one.
                  if (g.messageGroupId) {
                    setRecipientMode('curated')
                    setCustomGroupId(g.messageGroupId)
                  }
                }}
                className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full sm:w-96">
                <option value="">— {t('admin.msgPickGameHint')} —</option>
                {upcomingGames.map(g => (
                  <option key={g.id} value={g.id}>{formatGameOption(g)}</option>
                ))}
              </select>
              <p className="mt-1 text-xs text-slate-500">{t('admin.msgPickGameHelp')}</p>
            </div>
          )}
          {selectedTemplate?.previewText && (
            <pre className="text-xs bg-slate-50 border border-slate-200 rounded p-2 whitespace-pre-wrap">{selectedTemplate.previewText}</pre>
          )}
          {selectedTemplate && selectedTemplate.variables.length > 0 && (
            <div className="space-y-2">
              <h3 className="text-sm font-medium text-slate-700">{t('admin.msgFillVariables')}</h3>
              {selectedTemplate.variables.map(v => {
                // Keying by Label matches the placeholder name in the approved Twilio template
                // body ({{What}}, {{When}}, ...). Twilio's Content API substitutes ContentVariables
                // by these names; positional keys would produce no substitution.
                const key = v.label
                return (
                  <div key={v.id} className="grid grid-cols-[8rem_1fr] items-center gap-2">
                    <label className="text-sm text-slate-700">{v.label} <span className="text-slate-400">{`{{${v.label}}}`}</span></label>
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
          <textarea rows={4} value={bodyEn} onChange={e => setBodyEn(e.target.value)}
            maxLength={2000}
            placeholder={t('admin.msgBodyPlaceholder')}
            className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full" />
          <p className="mt-1 text-xs text-slate-500">{bodyEn.length} / 2000 · {t('admin.msgBodyHint')}</p>
        </div>
      )}

      <div>
        <button type="submit" disabled={sending}
          className="bg-emerald-700 text-white font-semibold px-5 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {sending
            ? t('admin.sending')
            : (mode === 'group-chat'
                ? t('admin.msgCreateAndSend')
                : (bodyMode === 'free-form' ? t('admin.msgPreviewSend') : t('admin.msgSend')))}
        </button>
      </div>
    </form>
    {previewStep !== null && (
      <BilingualPreviewModal
        step={previewStep}
        bodyEn={bodyEn}
        bodyEs={bodyEs}
        defaultLang={defaultLang}
        sending={sending}
        showDefaultLangPicker={recipientMode !== 'curated'}
        onBodyEnChange={setBodyEn}
        onBodyEsChange={setBodyEs}
        onDefaultLangChange={setDefaultLang}
        onContinue={() => setPreviewStep('confirm')}
        onBack={() => setPreviewStep('edit')}
        onCancel={() => setPreviewStep(null)}
        onConfirm={() => sendNow({ usingTemplate: false })}
      />
    )}
    {templatePreviewOpen && selectedTemplate && (
      <TemplatePreviewModal
        template={selectedTemplate}
        values={templateValues}
        recipientLabel={recipientPreview}
        sending={sending}
        onCancel={() => setTemplatePreviewOpen(false)}
        onConfirm={() => sendNow({ usingTemplate: true })}
      />
    )}
    </>
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
  const [newLanguage, setNewLanguage] = useState<Language>(0)
  const [memberName, setMemberName] = useState('')
  const [memberPhone, setMemberPhone] = useState('')
  const [memberLanguage, setMemberLanguage] = useState<Language | ''>('') // '' = inherit group default

  const loadGroup = async (id: number) => {
    try { setSelected(await Api.getMessagingGroup(id)) }
    catch (e: any) { onError(extractError(e)) }
  }

  const create = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newName.trim()) { onError('Name is required.'); return }
    try {
      const g = await Api.createMessagingGroup({
        name: newName.trim(),
        description: newDescription.trim() || null,
        language: newLanguage,
      })
      setNewName(''); setNewDescription(''); setNewLanguage(0)
      await onChanged()
      await loadGroup(g.id)
      onNotice(`Created group "${g.name}".`)
    } catch (e: any) { onError(extractError(e)) }
  }

  const updateLanguage = async (lang: Language) => {
    if (!selected) return
    try {
      await Api.updateMessagingGroup(selected.id, {
        name: selected.name,
        description: selected.description,
        language: lang,
      })
      await loadGroup(selected.id)
      await onChanged()
      onNotice(`Group language set to ${lang === 1 ? 'Español' : 'English'}.`)
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
        language: memberLanguage === '' ? null : memberLanguage,
      })
      setMemberName(''); setMemberPhone(''); setMemberLanguage('')
      await loadGroup(selected.id)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  const setMemberLang = async (memberId: number, lang: Language) => {
    if (!selected) return
    try {
      await Api.updateMessagingGroupMemberLanguage(selected.id, memberId, lang)
      await loadGroup(selected.id)
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
          <select value={newLanguage} onChange={e => setNewLanguage(Number(e.target.value) as Language)}
            className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
            <option value={0}>{t('admin.msgGroupLangEn')}</option>
            <option value={1}>{t('admin.msgGroupLangEs')}</option>
          </select>
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
              <div className="flex gap-2 items-center">
                <select value={selected.language}
                  onChange={e => updateLanguage(Number(e.target.value) as Language)}
                  className="text-sm border border-slate-300 rounded-md px-2 py-1"
                  title={t('admin.msgGroupLangSelectHelp')}>
                  <option value={0}>{t('admin.msgGroupLangEn')}</option>
                  <option value={1}>{t('admin.msgGroupLangEs')}</option>
                </select>
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
            <form onSubmit={addMember} className="grid sm:grid-cols-[1fr_1fr_auto_auto] gap-2">
              <input type="text" value={memberName} onChange={e => setMemberName(e.target.value)}
                placeholder={t('admin.msgNameOptional')}
                className="border border-slate-300 rounded-md px-3 py-2 text-sm" />
              <input type="tel" value={memberPhone} onChange={e => setMemberPhone(e.target.value)}
                placeholder="+17025551212"
                className="border border-slate-300 rounded-md px-3 py-2 text-sm" />
              <select value={memberLanguage}
                onChange={e => setMemberLanguage(e.target.value === '' ? '' : Number(e.target.value) as Language)}
                className="border border-slate-300 rounded-md px-2 py-2 text-sm">
                <option value="">{t('admin.msgMemberLangDefault')}</option>
                <option value={0}>EN</option>
                <option value={1}>ES</option>
              </select>
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
                  <th className="py-2 pr-4">{t('admin.msgMemberLang')}</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody>
                {selected.members.map(m => (
                  <tr key={m.id} className="border-b last:border-0">
                    <td className="py-2 pr-4">{m.name ?? '—'}</td>
                    <td className="py-2 pr-4">{m.phone}</td>
                    <td className="py-2 pr-4">
                      <select value={m.language}
                        onChange={e => setMemberLang(m.id, Number(e.target.value) as Language)}
                        className="border border-slate-300 rounded-md px-2 py-1 text-xs">
                        <option value={0}>EN</option>
                        <option value={1}>ES</option>
                      </select>
                    </td>
                    <td className="py-2 pr-4 text-right">
                      <button onClick={() => removeMember(m.id)}
                        className="text-rose-700 hover:underline">{t('admin.delete')}</button>
                    </td>
                  </tr>
                ))}
                {selected.members.length === 0 && (
                  <tr><td colSpan={4} className="py-4 text-center text-slate-400">—</td></tr>
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
                <td className="py-2 pr-4 max-w-md">
                  {b.bodyEn && <div className="truncate" title={b.bodyEn}><span className="text-xs text-slate-400">EN:</span> {b.bodyEn}</div>}
                  {b.bodyEs && <div className="truncate" title={b.bodyEs}><span className="text-xs text-slate-400">ES:</span> {b.bodyEs}</div>}
                </td>
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

/**
 * Parses a free-form paste of phone numbers (and optional names) into a clean recipient list.
 * Handles the common paste sources for school admins:
 *   - bare phone per line: "+17025551212"
 *   - "Name, +17025551212"  /  "Name +1 (702) 555-1212"
 *   - WhatsApp's "~Maria Lopez ~+1 702 555 1212" copy-from-group-info format
 *   - 10-digit US numbers without country code (auto-prefixed with +1)
 * Output is deduped on the normalized phone.
 */
function parseRecipientList(raw: string): AdHocRecipient[] {
  if (!raw.trim()) return []
  const seen = new Set<string>()
  const out: AdHocRecipient[] = []
  // Top-level split: newlines, semicolons, or a bare tab. Commas are not split here because
  // they're often used as the name/phone separator within a single line.
  for (const line of raw.split(/[\n;]+/)) {
    const trimmed = line.trim()
    if (!trimmed) continue
    // Greedy phone-like run: optional +, then digits and phone punctuation, anchored by digits at the ends.
    const matches = [...trimmed.matchAll(/\+?\d[\d\s\-().]{6,}\d/g)]
    if (matches.length === 0) continue
    const m = matches[matches.length - 1] // rightmost phone-like sequence
    const phone = normalizePhone(m[0])
    if (!phone) continue
    if (seen.has(phone)) continue
    seen.add(phone)
    const before = trimmed.slice(0, m.index).trim()
    // Strip trailing comma/colon between name and phone, and WhatsApp's "~" prefix.
    const name = before.replace(/[,:]\s*$/, '').replace(/^~+/, '').replace(/~+/g, ' ').trim()
    out.push({ phone, name: name || null })
  }
  return out
}

function normalizePhone(input: string): string | null {
  // Strip everything except + and digits. + must be at the very start, if present.
  const cleaned = input.replace(/[^\d+]/g, '')
  if (!cleaned) return null
  if (cleaned.startsWith('+')) {
    // Already E.164-ish. Reject obvious garbage (too short).
    return cleaned.length >= 8 ? cleaned : null
  }
  const digits = cleaned.replace(/\+/g, '')
  if (digits.length === 10) return `+1${digits}`           // US 10-digit
  if (digits.length === 11 && digits.startsWith('1')) return `+${digits}` // US with leading 1
  // International without +: better to refuse than to guess.
  return digits.length >= 11 ? `+${digits}` : null
}

// --- Schedule helpers ----------------------------------------------------

function formatGameOption(g: ScheduledGame): string {
  const d = new Date(g.startsAt)
  const date = d.toLocaleDateString(undefined, { weekday: 'short', month: 'numeric', day: 'numeric' })
  const time = d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })
  const homeAway = g.isHome === true ? ' (H)' : g.isHome === false ? ' (A)' : ''
  const label = g.opponentName ? `vs ${g.opponentName}` : (g.summary?.trim() || g.teamName)
  return `${date} ${time}${homeAway} — ${label}${g.location ? ` @ ${g.location}` : ''}`
}

/**
 * Auto-fills template variable inputs from a picked game. Matching is by label substring
 * (case-insensitive) — works for the canonical practice_or_game template (What/When/Where/wear)
 * and is intentionally lenient so admins can rename labels without breaking the autofill.
 *
 * Specifically:
 *   - "what"  → "Game vs <opponent>" (or "Practice" when there's no opponent)
 *   - "when"  → locale-formatted date+time, in the template's language
 *   - "where" → game location string from GotSport (park + field)
 *   - "wear"  → home/away uniform per LVSS policy, in the template's language
 */
function applyGameToTemplate(
  game: ScheduledGame,
  template: WhatsAppTemplate,
  setValues: React.Dispatch<React.SetStateAction<Record<string, string>>>,
) {
  const lang = template.language
  const locale = lang === 1 ? 'es-US' : 'en-US'

  setValues(prev => {
    const next = { ...prev }
    for (const v of template.variables) {
      const label = v.label.toLowerCase()
      const key = v.label
      if (label.includes('what')) {
        next[key] = game.opponentName
          ? `${GAME_VS_PREFIX[lang]} ${game.opponentName}`
          : (game.summary?.trim() || PRACTICE_FALLBACK[lang])
      } else if (label.includes('when')) {
        const d = new Date(game.startsAt)
        next[key] = `${d.toLocaleDateString(locale, { weekday: 'short', month: 'numeric', day: 'numeric' })} ${d.toLocaleTimeString(locale, { hour: 'numeric', minute: '2-digit' })}`
      } else if (label.includes('where') || label.includes('location')) {
        next[key] = game.location?.trim() || ''
      } else if (label.includes('wear')) {
        if (game.isHome === true) next[key] = WEAR_HOME[lang]
        else if (game.isHome === false) next[key] = WEAR_AWAY[lang]
        // game.isHome === null (practice/training): leave for admin to type.
      }
    }
    return next
  })
}

// --- Teams tab -----------------------------------------------------------

function TeamsTab({
  teams, curated, onChanged, onError, onNotice,
}: {
  teams: TeamSummary[]
  curated: MessageGroupSummary[]
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [editingId, setEditingId] = useState<number | 'new' | null>(null)
  const [name, setName] = useState('')
  const [scheduleUrl, setScheduleUrl] = useState('')
  const [messageGroupId, setMessageGroupId] = useState<number | ''>('')
  const [detail, setDetail] = useState<TeamDetail | null>(null)
  const [syncing, setSyncing] = useState(false)

  const startNew = () => {
    setEditingId('new'); setName(''); setScheduleUrl(''); setMessageGroupId(''); setDetail(null)
  }
  const startEdit = async (id: number) => {
    try {
      const d = await Api.getTeam(id)
      setEditingId(id); setName(d.name)
      setScheduleUrl(`https://system.gotsport.com/org_event/events/${d.gotSportEventId}/schedules?team=${d.gotSportTeamId}`)
      setMessageGroupId(d.messageGroupId ?? ''); setDetail(d)
    } catch (e: any) { onError(extractError(e)) }
  }

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!name.trim()) { onError('Team name is required.'); return }
    if (!scheduleUrl.trim()) { onError('GotSport schedule URL is required.'); return }
    try {
      const payload = {
        name: name.trim(),
        scheduleUrl: scheduleUrl.trim(),
        messageGroupId: messageGroupId === '' ? null : Number(messageGroupId),
      }
      const saved = (editingId === 'new' || editingId === null)
        ? await Api.createTeam(payload)
        : await Api.updateTeam(editingId, payload)
      await onChanged()
      onNotice(`Saved team "${saved.name}".`)
      setEditingId(saved.id)
      // Reload detail so user can sync right away.
      const d = await Api.getTeam(saved.id)
      setDetail(d)
    } catch (e: any) { onError(extractError(e)) }
  }

  const remove = async () => {
    if (typeof editingId !== 'number') return
    if (!confirm(`Delete team "${name}"? Synced games are removed too. Past broadcasts are unaffected.`)) return
    try {
      await Api.deleteTeam(editingId)
      setEditingId(null); setDetail(null)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  const sync = async () => {
    if (typeof editingId !== 'number') return
    setSyncing(true); onError(''); onNotice('')
    try {
      const r = await Api.syncTeam(editingId)
      onNotice(r.message)
      await onChanged()
      const d = await Api.getTeam(editingId)
      setDetail(d)
    } catch (e: any) {
      onError(extractError(e))
    } finally { setSyncing(false) }
  }

  return (
    <div className="grid lg:grid-cols-3 gap-4">
      <section className="bg-white border border-slate-200 rounded-lg p-4 lg:col-span-1 space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="font-bold text-emerald-800">{t('admin.msgTeamsHeader')}</h2>
          <button onClick={startNew} className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgNewTeam')}</button>
        </div>
        <ul className="space-y-1">
          {teams.map(team => (
            <li key={team.id}>
              <button onClick={() => startEdit(team.id)}
                className={`w-full text-left px-2 py-1.5 rounded text-sm hover:bg-emerald-50 ${editingId === team.id ? 'bg-emerald-50 text-emerald-800 font-medium' : ''}`}>
                <div>{team.name}</div>
                <div className="text-xs text-slate-500">
                  {team.upcomingGameCount} {t('admin.msgUpcomingGames')}
                  {team.messageGroupName && <> · {team.messageGroupName}</>}
                </div>
              </button>
            </li>
          ))}
          {teams.length === 0 && <li className="text-sm text-slate-400">{t('admin.msgNoTeams')}</li>}
        </ul>
        <p className="text-xs text-slate-500">{t('admin.msgTeamsHint')}</p>
      </section>

      <section className="lg:col-span-2 space-y-4">
        {editingId === null && (
          <div className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-sm text-slate-500">
            {t('admin.msgSelectTeam')}
          </div>
        )}
        {editingId !== null && (
          <form onSubmit={save} className="bg-white border border-slate-200 rounded-lg p-4 space-y-3">
            <div className="grid sm:grid-cols-2 gap-3">
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.msgTeamName')}</span>
                <input type="text" value={name} onChange={e => setName(e.target.value)}
                  placeholder="U10 Boys"
                  className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
              </label>
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.msgTeamGroup')}</span>
                <select value={messageGroupId}
                  onChange={e => setMessageGroupId(e.target.value === '' ? '' : Number(e.target.value))}
                  className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
                  <option value="">— {t('admin.msgTeamGroupNone')} —</option>
                  {curated.map(g => (
                    <option key={g.id} value={g.id}>{g.name} ({g.memberCount})</option>
                  ))}
                </select>
              </label>
            </div>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgTeamScheduleUrl')}</span>
              <input type="url" value={scheduleUrl} onChange={e => setScheduleUrl(e.target.value)}
                placeholder="https://system.gotsport.com/org_event/events/48082/schedules?team=3764244"
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm font-mono" />
              <span className="block text-xs text-slate-500 mt-1">{t('admin.msgTeamScheduleUrlHelp')}</span>
            </label>

            <div className="flex items-center gap-3 pt-2 border-t border-slate-100">
              <button type="submit"
                className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
                {editingId === 'new' ? t('admin.msgCreateTeam') : t('admin.msgSave')}
              </button>
              {typeof editingId === 'number' && (
                <>
                  <button type="button" onClick={sync} disabled={syncing}
                    className="text-sm border border-emerald-300 text-emerald-700 rounded-md px-3 py-1.5 hover:bg-emerald-50 disabled:opacity-60">
                    {syncing ? t('admin.msgSyncing') : t('admin.msgSyncNow')}
                  </button>
                  <button type="button" onClick={remove}
                    className="text-sm border border-rose-300 text-rose-700 rounded-md px-3 py-1.5 hover:bg-rose-50">
                    {t('admin.delete')}
                  </button>
                </>
              )}
              <button type="button" onClick={() => { setEditingId(null); setDetail(null) }}
                className="text-sm text-slate-600 hover:underline ml-auto">{t('admin.msgCancel')}</button>
            </div>
            {detail?.lastSyncedAt && (
              <p className="text-xs text-slate-500">
                {t('admin.msgLastSynced')}: {new Date(detail.lastSyncedAt).toLocaleString()} — {detail.lastSyncMessage}
              </p>
            )}
          </form>
        )}

        {detail && detail.upcomingGames.length > 0 && (
          <div className="bg-white border border-slate-200 rounded-lg p-4">
            <h3 className="font-medium text-slate-700 mb-2">{t('admin.msgUpcoming')}</h3>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-500 border-b">
                  <th className="py-1 pr-4">{t('admin.msgWhen')}</th>
                  <th className="py-1 pr-4">{t('admin.msgSummary')}</th>
                  <th className="py-1 pr-4">{t('admin.msgLocation')}</th>
                </tr>
              </thead>
              <tbody>
                {detail.upcomingGames.map(g => (
                  <tr key={g.id} className="border-b last:border-0">
                    <td className="py-1 pr-4 whitespace-nowrap">{new Date(g.startsAt).toLocaleString()}</td>
                    <td className="py-1 pr-4">{g.summary ?? '—'}</td>
                    <td className="py-1 pr-4">{g.location ?? '—'}</td>
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

// --- Bilingual preview modal ---------------------------------------------

function BilingualPreviewModal({
  step, bodyEn, bodyEs, defaultLang, sending, showDefaultLangPicker,
  onBodyEnChange, onBodyEsChange, onDefaultLangChange,
  onContinue, onBack, onCancel, onConfirm,
}: {
  step: 'edit' | 'confirm'
  bodyEn: string
  bodyEs: string
  defaultLang: Language
  sending: boolean
  showDefaultLangPicker: boolean
  onBodyEnChange: (v: string) => void
  onBodyEsChange: (v: string) => void
  onDefaultLangChange: (v: Language) => void
  onContinue: () => void
  onBack: () => void
  onCancel: () => void
  onConfirm: () => void
}) {
  const { t } = useTranslation()
  const [translating, setTranslating] = useState<'enToEs' | 'esToEn' | null>(null)
  const [translationNote, setTranslationNote] = useState<string | null>(null)

  const runTranslate = async (direction: 'enToEs' | 'esToEn') => {
    const text = direction === 'enToEs' ? bodyEn : bodyEs
    if (!text.trim()) return
    setTranslating(direction); setTranslationNote(null)
    try {
      const r = await Api.translate({
        text: text.trim(),
        from: direction === 'enToEs' ? 0 : 1,
        to: direction === 'enToEs' ? 1 : 0,
      })
      if (direction === 'enToEs') onBodyEsChange(r.translated); else onBodyEnChange(r.translated)
      setTranslationNote(r.fullyTranslated
        ? t('admin.msgTranslateNoteFull', { count: r.matchedPhrases.length })
        : (r.matchedPhrases.length > 0
            ? t('admin.msgTranslateNotePartial', { count: r.matchedPhrases.length })
            : t('admin.msgTranslateNoteNone')))
    } catch (e: any) {
      setTranslationNote(extractError(e))
    } finally {
      setTranslating(null)
    }
  }

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/40 flex items-start justify-center p-4 overflow-y-auto">
      <div className="bg-white rounded-lg shadow-xl max-w-5xl w-full mt-10 p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold text-emerald-800">
            {step === 'edit' ? t('admin.msgPreviewStep1Title') : t('admin.msgPreviewStep2Title')}
          </h2>
          <button onClick={onCancel} className="text-sm text-slate-500 hover:text-slate-700">✕</button>
        </div>
        <p className="text-sm text-slate-600">
          {step === 'edit' ? t('admin.msgPreviewStep1Help') : t('admin.msgPreviewStep2Help')}
        </p>

        <div className="grid md:grid-cols-2 gap-4">
          <div>
            <div className="flex items-center justify-between mb-1">
              <label className="text-sm font-medium text-slate-700">{t('admin.msgPreviewEn')}</label>
              {step === 'edit' && (
                <button type="button" onClick={() => runTranslate('esToEn')}
                  disabled={translating !== null || !bodyEs.trim()}
                  className="text-xs text-emerald-700 hover:underline disabled:opacity-40 disabled:no-underline">
                  {translating === 'esToEn' ? t('admin.msgTranslating') : t('admin.msgTranslateFromEs')}
                </button>
              )}
            </div>
            {step === 'edit' ? (
              <textarea rows={8} value={bodyEn} onChange={e => onBodyEnChange(e.target.value)}
                maxLength={2000}
                className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            ) : (
              <pre className="w-full border border-slate-200 bg-slate-50 rounded-md px-3 py-2 text-sm whitespace-pre-wrap min-h-[8rem]">{bodyEn || '—'}</pre>
            )}
          </div>
          <div>
            <div className="flex items-center justify-between mb-1">
              <label className="text-sm font-medium text-slate-700">{t('admin.msgPreviewEs')}</label>
              {step === 'edit' && (
                <button type="button" onClick={() => runTranslate('enToEs')}
                  disabled={translating !== null || !bodyEn.trim()}
                  className="text-xs text-emerald-700 hover:underline disabled:opacity-40 disabled:no-underline">
                  {translating === 'enToEs' ? t('admin.msgTranslating') : t('admin.msgTranslateFromEn')}
                </button>
              )}
            </div>
            {step === 'edit' ? (
              <textarea rows={8} value={bodyEs} onChange={e => onBodyEsChange(e.target.value)}
                maxLength={2000}
                className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            ) : (
              <pre className="w-full border border-slate-200 bg-slate-50 rounded-md px-3 py-2 text-sm whitespace-pre-wrap min-h-[8rem]">{bodyEs || '—'}</pre>
            )}
          </div>
        </div>

        {translationNote && step === 'edit' && (
          <div className="text-xs text-slate-600 bg-slate-50 border border-slate-200 rounded p-2">{translationNote}</div>
        )}

        {showDefaultLangPicker && (
          <div className="text-sm">
            <label className="font-medium text-slate-700 mr-3">{t('admin.msgDefaultLang')}</label>
            {step === 'edit' ? (
              <select value={defaultLang} onChange={e => onDefaultLangChange(Number(e.target.value) as Language)}
                className="border border-slate-300 rounded-md px-2 py-1 text-sm">
                <option value={0}>English</option>
                <option value={1}>Español</option>
              </select>
            ) : (
              <span className="text-slate-700">{defaultLang === 1 ? 'Español' : 'English'}</span>
            )}
            <span className="ml-2 text-xs text-slate-500">{t('admin.msgDefaultLangHelp')}</span>
          </div>
        )}

        <div className="flex items-center gap-3 pt-3 border-t border-slate-100">
          {step === 'edit' ? (
            <>
              <button onClick={onContinue}
                className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
                {t('admin.msgPreviewContinue')}
              </button>
              <button onClick={onCancel}
                className="text-sm text-slate-600 hover:underline">{t('admin.msgCancel')}</button>
            </>
          ) : (
            <>
              <button onClick={onConfirm} disabled={sending}
                className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                {sending ? t('admin.sending') : t('admin.msgPreviewConfirmSend')}
              </button>
              <button onClick={onBack} disabled={sending}
                className="text-sm border border-slate-300 rounded-md px-3 py-1.5 hover:bg-slate-50">
                {t('admin.msgPreviewBack')}
              </button>
              <button onClick={onCancel} disabled={sending}
                className="text-sm text-slate-600 hover:underline ml-auto">{t('admin.msgCancel')}</button>
            </>
          )}
        </div>
      </div>
    </div>
  )
}

// --- Template preview modal ----------------------------------------------

function TemplatePreviewModal({
  template, values, recipientLabel, sending, onCancel, onConfirm,
}: {
  template: WhatsAppTemplate
  values: Record<string, string>
  recipientLabel: string
  sending: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
  const { t } = useTranslation()

  // Two render sources: the primary template the admin picked, and (when it exists) the auto-paired
  // opposite-language template. WhatsApp templates are language-locked at approval time, so the
  // ES recipient flow needs a separate approved Spanish template — that's what `paired` represents.
  const renderWith = (text: string | null, vars: { label: string }[]): string => {
    if (!text) return vars.map(v => `${v.label}: ${values[v.label] ?? ''}`).join('\n')
    let out = text
    for (const v of vars) out = out.split(`{{${v.label}}}`).join(values[v.label] ?? '')
    return out
  }
  const primary = useMemo(() => renderWith(template.previewText, template.variables),
    [template, values])
  const pairRendered = useMemo(() => {
    if (!template.paired) return null
    // Pair uses the same labels by convention — fall back to primary variables if the pair didn't
    // explicitly define any (e.g. user hasn't populated them yet via the Templates tab).
    const vars = template.paired.variables.length > 0 ? template.paired.variables : template.variables
    return renderWith(template.paired.previewText, vars)
  }, [template, values])

  const langLabel = (lang: Language) => lang === 1 ? 'Español' : 'English'
  const hasPair = template.paired != null
  const modalWidth = hasPair ? 'max-w-5xl' : 'max-w-2xl'

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/40 flex items-start justify-center p-4 overflow-y-auto">
      <div className={`bg-white rounded-lg shadow-xl ${modalWidth} w-full mt-10 p-6 space-y-4`}>
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-bold text-emerald-800">{t('admin.msgTemplateSendPreviewTitle')}</h2>
          <button onClick={onCancel} className="text-sm text-slate-500 hover:text-slate-700">✕</button>
        </div>
        <p className="text-sm text-slate-600">{t('admin.msgTemplateSendPreviewHelp')}</p>

        <div className="text-xs text-slate-500 flex flex-wrap gap-x-4">
          <span><strong className="text-slate-700">{t('admin.msgPreviewTemplateLabel')}:</strong> {template.name} ({langLabel(template.language)})</span>
          {template.paired && (
            <span><strong className="text-slate-700">{t('admin.msgPreviewPairedLabel')}:</strong> {template.paired.name} ({langLabel(template.paired.language)})</span>
          )}
          <span><strong className="text-slate-700">{t('admin.msgPreviewRecipientLabel')}:</strong> {recipientLabel}</span>
        </div>

        {!hasPair && (
          <div className="text-xs text-amber-800 bg-amber-50 border border-amber-200 rounded p-2">
            {template.language === 0
              ? t('admin.msgTemplateNoSpanishPair', { name: `${template.name}_es` })
              : t('admin.msgTemplateNoEnglishPair', { name: `${template.name}_en` })}
          </div>
        )}

        <div className={hasPair ? 'grid md:grid-cols-2 gap-4' : ''}>
          <div>
            <div className="text-xs font-medium text-slate-700 mb-1">
              {langLabel(template.language)} <span className="text-slate-400">— {template.name}</span>
            </div>
            <pre className="w-full border border-slate-200 bg-slate-50 rounded-md px-3 py-2 text-sm whitespace-pre-wrap min-h-[6rem]">{primary}</pre>
          </div>
          {template.paired && pairRendered !== null && (
            <div>
              <div className="text-xs font-medium text-slate-700 mb-1">
                {langLabel(template.paired.language)} <span className="text-slate-400">— {template.paired.name}</span>
              </div>
              <pre className="w-full border border-slate-200 bg-slate-50 rounded-md px-3 py-2 text-sm whitespace-pre-wrap min-h-[6rem]">{pairRendered}</pre>
            </div>
          )}
        </div>

        <div>
          <div className="text-xs font-medium text-slate-700 mb-1">{t('admin.msgPreviewVariables')}</div>
          <table className="w-full text-xs">
            <tbody>
              {template.variables.map(v => (
                <tr key={v.id} className="border-b last:border-0">
                  <td className="py-1 pr-3 text-slate-500">{v.label}</td>
                  <td className="py-1 pr-3 font-mono">{values[v.label] ?? ''}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex items-center gap-3 pt-3 border-t border-slate-100">
          <button onClick={onConfirm} disabled={sending}
            className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
            {sending ? t('admin.sending') : t('admin.msgPreviewConfirmSend')}
          </button>
          <button onClick={onCancel} disabled={sending}
            className="text-sm text-slate-600 hover:underline ml-auto">{t('admin.msgCancel')}</button>
        </div>
      </div>
    </div>
  )
}

// --- Dictionary tab ------------------------------------------------------

function DictionaryTab({
  onError, onNotice,
}: {
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [entries, setEntries] = useState<PhraseTranslation[]>([])
  const [editingId, setEditingId] = useState<number | 'new' | null>(null)
  const [english, setEnglish] = useState('')
  const [spanish, setSpanish] = useState('')

  const load = async () => {
    try { setEntries(await Api.listPhraseTranslations()) }
    catch (e: any) { onError(extractError(e)) }
  }
  useEffect(() => { load() }, [])

  const startNew = () => { setEditingId('new'); setEnglish(''); setSpanish('') }
  const startEdit = (p: PhraseTranslation) => { setEditingId(p.id); setEnglish(p.english); setSpanish(p.spanish) }
  const cancel = () => { setEditingId(null) }

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!english.trim() || !spanish.trim()) { onError('Both English and Spanish required.'); return }
    try {
      const payload = { english: english.trim(), spanish: spanish.trim() }
      if (editingId === 'new') await Api.createPhraseTranslation(payload)
      else if (typeof editingId === 'number') await Api.updatePhraseTranslation(editingId, payload)
      setEditingId(null)
      await load()
      onNotice(t('admin.msgDictionarySaved'))
    } catch (e: any) { onError(extractError(e)) }
  }
  const remove = async (id: number) => {
    if (!confirm('Delete this translation?')) return
    try { await Api.deletePhraseTranslation(id); await load() }
    catch (e: any) { onError(extractError(e)) }
  }

  return (
    <section className="bg-white border border-slate-200 rounded-lg p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-bold text-emerald-800">{t('admin.msgDictionaryHeader')}</h2>
          <p className="text-xs text-slate-500 mt-1">{t('admin.msgDictionaryHint')}</p>
        </div>
        <button onClick={startNew}
          className="bg-emerald-700 text-white text-sm font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800">
          + {t('admin.msgDictionaryAdd')}
        </button>
      </div>

      {editingId !== null && (
        <form onSubmit={save} className="border border-slate-200 rounded p-3 grid sm:grid-cols-[1fr_1fr_auto] gap-2 items-start">
          <input type="text" value={english} onChange={e => setEnglish(e.target.value)}
            placeholder="English phrase"
            className="border border-slate-300 rounded-md px-3 py-2 text-sm" autoFocus />
          <input type="text" value={spanish} onChange={e => setSpanish(e.target.value)}
            placeholder="Spanish phrase"
            className="border border-slate-300 rounded-md px-3 py-2 text-sm" />
          <div className="flex gap-2">
            <button type="submit"
              className="bg-emerald-700 text-white text-sm font-semibold px-3 py-2 rounded-md hover:bg-emerald-800">
              {editingId === 'new' ? t('admin.msgCreateGroup') : t('admin.msgSave')}
            </button>
            <button type="button" onClick={cancel}
              className="text-sm text-slate-600 hover:underline">{t('admin.msgCancel')}</button>
          </div>
        </form>
      )}

      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-slate-500 border-b">
            <th className="py-2 pr-4">{t('admin.msgDictEn')}</th>
            <th className="py-2 pr-4">{t('admin.msgDictEs')}</th>
            <th className="py-2 pr-4"></th>
          </tr>
        </thead>
        <tbody>
          {entries.map(p => (
            <tr key={p.id} className="border-b last:border-0">
              <td className="py-2 pr-4">{p.english}</td>
              <td className="py-2 pr-4">{p.spanish}</td>
              <td className="py-2 pr-4 text-right whitespace-nowrap">
                <button onClick={() => startEdit(p)}
                  className="text-emerald-700 hover:underline">{t('admin.details')}</button>
                <span className="mx-2 text-slate-300">|</span>
                <button onClick={() => remove(p.id)}
                  className="text-rose-700 hover:underline">{t('admin.delete')}</button>
              </td>
            </tr>
          ))}
          {entries.length === 0 && (
            <tr><td colSpan={3} className="py-4 text-center text-slate-400">{t('admin.msgDictionaryEmpty')}</td></tr>
          )}
        </tbody>
      </table>
    </section>
  )
}

function extractError(e: any): string {
  const status = e?.response?.status
  if (status === 401) return 'Session expired. Please reload and sign in again.'
  if (status === 403) return 'Admin role required.'
  if (e?.code === 'ERR_NETWORK') return 'Cannot reach the API.'
  return e?.response?.data?.title ?? e?.response?.data ?? e?.message ?? 'Error'
}
