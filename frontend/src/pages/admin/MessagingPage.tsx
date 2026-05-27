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
  EventRecipient,
  GroupConversationDetail,
  GroupConversationSummary,
  InboundMessage,
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
  TemplatePreviewResponse,
  TemplatePreviewSide,
  WhatsAppTemplate,
  EmailTemplate,
  ThreadSummary,
  ThreadDetail,
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

type Tab = 'compose' | 'inbox' | 'groups' | 'conversations' | 'templates' | 'teams' | 'dictionary' | 'history' | 'settings'
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
  const [emailTemplates, setEmailTemplates] = useState<EmailTemplate[]>([])
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
    try {
      const [wa, em] = await Promise.all([Api.listWhatsAppTemplates(), Api.listEmailTemplates()])
      setTemplates(wa); setEmailTemplates(em)
    }
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
            <Capability ok={config.email} label="Email" />
            <Capability ok={config.conversations} label={t('admin.msgGroupChat')} />
          </div>
        )}

        <div className="flex gap-1 border-b border-slate-200">
          {tabBtn('compose', t('admin.msgTabCompose'))}
          {tabBtn('inbox', t('admin.msgTabInbox'))}
          {tabBtn('groups', t('admin.msgTabGroups'))}
          {tabBtn('conversations', t('admin.msgTabConversations'))}
          {tabBtn('templates', t('admin.msgTabTemplates'))}
          {tabBtn('teams', t('admin.msgTabTeams'))}
          {tabBtn('dictionary', t('admin.msgTabDictionary'))}
          {tabBtn('history', t('admin.msgTabHistory'))}
          {tabBtn('settings', t('admin.msgTabSettings'))}
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
            emailTemplates={emailTemplates}
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
            emailTemplates={emailTemplates}
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

        {tab === 'settings' && (
          <SettingsTab
            onError={(e) => setError(e)}
            onNotice={(n) => setNotice(n)}
          />
        )}

        {tab === 'inbox' && (
          <InboxTab
            config={config}
            onError={(e) => setError(e)}
            onNotice={(n) => setNotice(n)}
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
  config, curated, dynamicGroups, templates, emailTemplates, upcomingGames, onSent, onError,
}: {
  config: MessagingConfig | null
  curated: MessageGroupSummary[]
  dynamicGroups: DynamicGroup[]
  templates: WhatsAppTemplate[]
  emailTemplates: EmailTemplate[]
  upcomingGames: ScheduledGame[]
  onSent: (msg: string) => void | Promise<void>
  onError: (e: string) => void
}) {
  const { t } = useTranslation()
  const [channel, setChannel] = useState<MessageChannel>(0)
  const [mode, setMode] = useState<SendMode>('broadcast')
  const [bodyMode, setBodyMode] = useState<ComposeBodyMode>('free-form')
  const [templateId, setTemplateId] = useState<number | ''>('')
  const [emailTemplateId, setEmailTemplateId] = useState<number | ''>('')
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
  const [subjectEn, setSubjectEn] = useState('')
  const [subjectEs, setSubjectEs] = useState('')
  const [defaultLang, setDefaultLang] = useState<Language>(0)
  const [pickedEventId, setPickedEventId] = useState<number | null>(null)
  const [previewStep, setPreviewStep] = useState<'edit' | 'confirm' | null>(null)
  const [templatePreviewOpen, setTemplatePreviewOpen] = useState(false)
  const [sending, setSending] = useState(false)

  const selectedTemplate = useMemo(
    () => templates.find(t => t.id === templateId) ?? null,
    [templateId, templates])
  const selectedEmailTemplate = useMemo(
    () => emailTemplates.find(t => t.id === emailTemplateId) ?? null,
    [emailTemplateId, emailTemplates])

  // Templates apply to WhatsApp (Content) and Email (admin-managed) broadcasts. Reset to free-form
  // on any other combination so the UI doesn't drift into an invalid state.
  useEffect(() => {
    if (mode !== 'broadcast' || (channel !== 1 && channel !== 2)) {
      setBodyMode('free-form')
      setTemplateId('')
      setEmailTemplateId('')
      setTemplateValues({})
    } else if (channel === 1) {
      setEmailTemplateId('')
    } else if (channel === 2) {
      setTemplateId('')
    }
  }, [channel, mode])

  const channelAvailable = (c: MessageChannel) =>
    c === 0 ? config?.sms : c === 1 ? config?.whatsApp : config?.email
  const isWhatsAppChannel = channel === 1
  const isEmailChannel = channel === 2

  // True once the admin has put anything into the recipient picker. Used to suppress the game
  // picker's auto-flip to the linked group — if the admin already picked an individual/group/list,
  // we shouldn't silently override that just because they want the game's variables autofilled.
  const hasRecipientInput = () =>
    phone.trim() !== '' ||
    customGroupId !== '' ||
    dynamicKey !== '' ||
    listRaw.trim() !== ''

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
      // For email channel, route through ad-hoc list so we can carry the email — the Individual
      // target only carries a phone.
      if (isEmailChannel) {
        return {
          kind: 3 as const,
          recipients: [{ phone: '', name: name.trim() || null, email: phone.trim() }],
        }
      }
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
    if (recipientMode === 'individual') {
      if (isEmailChannel && !phone.trim()) return 'Enter an email address.'
      if (!isEmailChannel && !phone.trim()) return 'Enter a phone number.'
    }
    if (recipientMode === 'curated' && customGroupId === '') return 'Pick a group.'
    if (recipientMode === 'dynamic' && !dynamicKey) return 'Pick a group.'
    if (recipientMode === 'list' && parsedList.length === 0) return 'Paste at least one recipient.'
    if (mode === 'group-chat' && !title.trim()) return 'Group chat title is required.'
    if (mode === 'group-chat' && isEmailChannel) return 'Group chat is only available for SMS or WhatsApp.'
    return null
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    const err = validate()
    if (err) { onError(err); return }

    const usingWhatsAppTemplate = mode === 'broadcast' && isWhatsAppChannel && bodyMode === 'template'
    const usingEmailTemplate = mode === 'broadcast' && isEmailChannel && bodyMode === 'template'
    if (usingWhatsAppTemplate) {
      if (!selectedTemplate) { onError('Pick a template.'); return }
      const missing = selectedTemplate.variables
        .filter(v => !templateValues[v.position.toString()]?.trim())
        .map(v => v.label)
      if (missing.length) { onError(`Fill in: ${missing.join(', ')}.`); return }
      setTemplatePreviewOpen(true)
      return
    }
    if (usingEmailTemplate) {
      if (!selectedEmailTemplate) { onError('Pick an email template.'); return }
      const missing = selectedEmailTemplate.variables
        .filter(v => !templateValues[v.position.toString()]?.trim())
        .map(v => v.label)
      if (missing.length) { onError(`Fill in: ${missing.join(', ')}.`); return }
      await sendNow({ usingTemplate: true })
      return
    }

    // Free-form: require at least the EN side, then open the bilingual preview gate. Group-chat
    // skips the preview because Conversations only sends one body anyway. Email also requires
    // a subject.
    if (!bodyEn.trim() && !bodyEs.trim()) { onError('Message body is required.'); return }
    if (isEmailChannel && !subjectEn.trim() && !subjectEs.trim()) {
      onError('Subject is required for email.'); return
    }
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
          ? (isEmailChannel
            ? {
                channel,
                emailTemplateId: selectedEmailTemplate!.id,
                templateVariables: templateValues,
                scheduledGameId: pickedEventId,
                target: target(),
              }
            : {
                channel,
                whatsAppTemplateId: selectedTemplate!.id,
                templateVariables: templateValues,
                scheduledGameId: pickedEventId,
                target: target(),
              })
          : {
              channel,
              bodyEn: bodyEn.trim() || null,
              bodyEs: bodyEs.trim() || null,
              subjectEn: isEmailChannel ? (subjectEn.trim() || null) : null,
              subjectEs: isEmailChannel ? (subjectEs.trim() || null) : null,
              defaultLanguage: defaultLang,
              scheduledGameId: pickedEventId,
              target: target(),
            }
        const r = await Api.createBroadcast(payload)
        const ok = r.recipients.filter(x => x.status !== 4 && x.status !== 5).length
        const via = args.usingTemplate
          ? (isEmailChannel
              ? `Email template "${selectedEmailTemplate!.name}"`
              : `${MESSAGE_CHANNEL_LABELS[channel]} template "${selectedTemplate!.name}"`)
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
      setBodyEn(''); setBodyEs(''); setSubjectEn(''); setSubjectEs('')
      setPreviewStep(null); setTemplatePreviewOpen(false)
      setPickedEventId(null)
      if (args.usingTemplate) setTemplateValues({})
    } catch (e: any) {
      onError(extractError(e))
    } finally {
      setSending(false)
    }
  }

  // Template mode is available for WhatsApp (Twilio Content) and Email (admin-managed) broadcasts.
  const showTemplateMode = (isWhatsAppChannel || isEmailChannel) && mode === 'broadcast'

  return (
    <>
    <form onSubmit={handleSubmit} className="bg-white border border-slate-200 rounded-lg p-6 space-y-4">
      <div className="grid sm:grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgChannel')}</label>
          <div className="flex gap-2">
            {[0, 1, 2].map(c => (
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
            <input type={isEmailChannel ? 'email' : 'tel'}
              value={phone} onChange={e => setPhone(e.target.value)}
              placeholder={isEmailChannel ? 'parent@example.com' : '+17025551212'}
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
          {isEmailChannel ? (
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgTemplate')}</label>
              <select value={emailTemplateId}
                onChange={e => {
                  const id = e.target.value === '' ? '' : Number(e.target.value)
                  setEmailTemplateId(id)
                  setTemplateValues({})
                }}
                className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full sm:w-96">
                <option value="">— {t('admin.msgPickTemplate')} —</option>
                {emailTemplates.map(tpl => (
                  <option key={tpl.id} value={tpl.id}>
                    {tpl.name} ({tpl.language === 1 ? 'ES' : 'EN'})
                  </option>
                ))}
              </select>
              {emailTemplates.length === 0 && (
                <p className="mt-1 text-xs text-rose-700">{t('admin.msgNoTemplates')}</p>
              )}
            </div>
          ) : (
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
          )}
          {upcomingGames.length > 0 && (
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgPickGame')}</label>
              <select value=""
                onChange={e => {
                  const gameId = Number(e.target.value)
                  if (!gameId) return
                  const g = upcomingGames.find(x => x.id === gameId)
                  if (!g) return
                  // Auto-select a template by Kind (Practice → practice_*, Game → game_*) if the
                  // admin hasn't already picked one, so a single dropdown pick fills variables AND
                  // wires the right template. Use the freshly-picked template for the autofill so
                  // we don't race the React state update.
                  let activeTemplate = selectedTemplate
                  if (!activeTemplate || ((activeTemplate.name.toLowerCase().includes('game')) !== (g.kind === 0))) {
                    const autoId = pickTemplateForEvent(g, templates)
                    if (autoId !== '') {
                      setTemplateId(autoId)
                      activeTemplate = templates.find(x => x.id === autoId) ?? null
                    }
                  }
                  if (activeTemplate) {
                    applyGameToTemplate(g, activeTemplate, setTemplateValues)
                  }
                  // Record event-id on this compose state so the broadcast links back to it —
                  // unlocks the cancellation-notification flow finding "who got reminded".
                  setPickedEventId(g.id)
                  // Auto-target the team's linked group only when the admin hasn't already picked
                  // a recipient. Otherwise we'd silently override an explicit Individual / list /
                  // dynamic-group choice when the admin just wanted the variables autofilled.
                  if (g.messageGroupId && !hasRecipientInput()) {
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
          {isEmailChannel && selectedEmailTemplate && (
            <div className="space-y-2">
              <pre className="text-xs bg-slate-50 border border-slate-200 rounded p-2 whitespace-pre-wrap">
{`Subject: ${selectedEmailTemplate.subject}\n\n${selectedEmailTemplate.body}`}
              </pre>
            </div>
          )}
          {isEmailChannel && selectedEmailTemplate && selectedEmailTemplate.variables.length > 0 && (
            <div className="space-y-2">
              <h3 className="text-sm font-medium text-slate-700">{t('admin.msgFillVariables')}</h3>
              {selectedEmailTemplate.variables.map(v => {
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
          {!isEmailChannel && selectedTemplate?.previewText && (
            <pre className="text-xs bg-slate-50 border border-slate-200 rounded p-2 whitespace-pre-wrap">{selectedTemplate.previewText}</pre>
          )}
          {!isEmailChannel && selectedTemplate && selectedTemplate.variables.length > 0 && (
            <div className="space-y-2">
              <h3 className="text-sm font-medium text-slate-700">{t('admin.msgFillVariables')}</h3>
              {selectedTemplate.variables.map(v => {
                // Approved templates use positional placeholders ({{1}}, {{2}}, ...). Twilio's
                // Content API substitutes ContentVariables by these numeric keys, so the values
                // dict must be keyed by Position as a string.
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
        <div className="space-y-3">
          {upcomingGames.length > 0 && (
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgPickGame')}</label>
              <select value=""
                onChange={e => {
                  const gameId = Number(e.target.value)
                  if (!gameId) return
                  const g = upcomingGames.find(x => x.id === gameId)
                  if (!g) return
                  applyGameToFreeForm(g, setBodyEn, setBodyEs)
                  setPickedEventId(g.id)
                  // Same rule as the template path: only auto-target the linked group when the
                  // admin hasn't already chosen a recipient.
                  if (g.messageGroupId && !hasRecipientInput()) {
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
              <p className="mt-1 text-xs text-slate-500">{t('admin.msgPickGameFreeFormHelp')}</p>
            </div>
          )}
          {isEmailChannel && (
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgSubject')}</label>
              <input type="text" value={subjectEn} onChange={e => setSubjectEn(e.target.value)}
                maxLength={256}
                placeholder={t('admin.msgSubjectPlaceholder')}
                className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full" />
              <p className="mt-1 text-xs text-slate-500">{t('admin.msgSubjectHint')}</p>
            </div>
          )}
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgBody')}</label>
            <textarea rows={isEmailChannel ? 8 : 4} value={bodyEn} onChange={e => setBodyEn(e.target.value)}
              maxLength={isEmailChannel ? 8000 : 2000}
              placeholder={t('admin.msgBodyPlaceholder')}
              className="border border-slate-300 rounded-md px-3 py-2 text-sm w-full" />
            <p className="mt-1 text-xs text-slate-500">{bodyEn.length} / {isEmailChannel ? 8000 : 2000} · {t('admin.msgBodyHint')}</p>
          </div>
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
  const [memberEmail, setMemberEmail] = useState('')
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
        email: memberEmail.trim() || null,
        language: memberLanguage === '' ? null : memberLanguage,
      })
      setMemberName(''); setMemberPhone(''); setMemberEmail(''); setMemberLanguage('')
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
            <form onSubmit={addMember} className="grid sm:grid-cols-[1fr_1fr_1fr_auto_auto] gap-2">
              <input type="text" value={memberName} onChange={e => setMemberName(e.target.value)}
                placeholder={t('admin.msgNameOptional')}
                className="border border-slate-300 rounded-md px-3 py-2 text-sm" />
              <input type="tel" value={memberPhone} onChange={e => setMemberPhone(e.target.value)}
                placeholder="+17025551212"
                className="border border-slate-300 rounded-md px-3 py-2 text-sm" />
              <input type="email" value={memberEmail} onChange={e => setMemberEmail(e.target.value)}
                placeholder={t('admin.msgEmailOptional')}
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
                  <th className="py-2 pr-4">{t('admin.email')}</th>
                  <th className="py-2 pr-4">{t('admin.msgMemberLang')}</th>
                  <th className="py-2 pr-4"></th>
                </tr>
              </thead>
              <tbody>
                {selected.members.map(m => (
                  <tr key={m.id} className="border-b last:border-0">
                    <td className="py-2 pr-4">{m.name ?? '—'}</td>
                    <td className="py-2 pr-4">{m.phone}</td>
                    <td className="py-2 pr-4">{m.email ?? '—'}</td>
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
                  <tr><td colSpan={5} className="py-4 text-center text-slate-400">—</td></tr>
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

// --- Inbox tab (per-phone conversation threads with reply) -----------------

function InboxTab({
  config, onError, onNotice,
}: {
  config: MessagingConfig | null
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [threads, setThreads] = useState<ThreadSummary[]>([])
  const [selectedPhone, setSelectedPhone] = useState<string | null>(null)
  const [thread, setThread] = useState<ThreadDetail | null>(null)
  const [loadingThread, setLoadingThread] = useState(false)
  const [replyChannel, setReplyChannel] = useState<MessageChannel>(0)
  const [replyBody, setReplyBody] = useState('')
  const [sending, setSending] = useState(false)

  const refresh = async () => {
    try { setThreads(await Api.listThreads()) }
    catch (e: any) { onError(extractError(e)) }
  }

  useEffect(() => { refresh() }, [])

  const openThread = async (phone: string) => {
    setSelectedPhone(phone)
    setLoadingThread(true)
    setThread(null)
    try {
      const d = await Api.getThread(phone)
      setThread(d)
      // Default the reply channel to whatever the most recent message used. Falls back to SMS.
      const last = d.messages[d.messages.length - 1]
      if (last) setReplyChannel(last.channel)
    } catch (e: any) {
      onError(extractError(e))
    } finally {
      setLoadingThread(false)
    }
  }

  const channelAvailable = (c: MessageChannel) =>
    c === 0 ? config?.sms : c === 1 ? config?.whatsApp : config?.email

  const send = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedPhone) return
    if (!replyBody.trim()) return
    if (replyChannel === 2) { onError(t('admin.msgInboxNoEmailReply')); return }
    setSending(true)
    try {
      const msg = await Api.sendThreadReply(selectedPhone, { channel: replyChannel, body: replyBody.trim() })
      // Optimistically append the new outbound to the open thread so the admin sees it immediately.
      setThread(prev => prev ? { ...prev, messages: [...prev.messages, msg] } : prev)
      setReplyBody('')
      await refresh()
      onNotice(t('admin.msgInboxReplySent'))
    } catch (e: any) {
      onError(extractError(e))
    } finally {
      setSending(false)
    }
  }

  return (
    <div className="grid lg:grid-cols-3 gap-4">
      <section className="bg-white border border-slate-200 rounded-lg p-4 lg:col-span-1">
        <div className="flex items-center justify-between mb-2">
          <h2 className="font-bold text-emerald-800">{t('admin.msgInboxHeader')}</h2>
          <button onClick={refresh} className="text-sm text-emerald-700 hover:underline">↻</button>
        </div>
        <ul className="space-y-1 max-h-[60vh] overflow-y-auto">
          {threads.map(thr => (
            <li key={thr.phone}>
              <button onClick={() => openThread(thr.phone)}
                className={`w-full text-left px-2 py-2 rounded text-sm hover:bg-emerald-50 ${selectedPhone === thr.phone ? 'bg-emerald-50 text-emerald-800 font-medium' : ''}`}>
                <div className="flex items-center justify-between">
                  <span className="font-medium">{thr.name ?? thr.phone}</span>
                  {!thr.parentRegistered && (
                    <span className="text-[10px] uppercase tracking-wide bg-amber-100 text-amber-800 px-1.5 py-0.5 rounded">
                      {t('admin.msgInboxUnregistered')}
                    </span>
                  )}
                </div>
                <div className="text-xs text-slate-500 font-mono">{thr.phone}</div>
                {thr.lastBody && (
                  <div className="text-xs text-slate-600 line-clamp-2 mt-0.5">
                    <span className="text-slate-400">{thr.lastDirection === 0 ? '← ' : '→ '}</span>
                    {thr.lastBody}
                  </div>
                )}
                <div className="text-xs text-slate-400 mt-0.5">
                  {new Date(thr.lastAt).toLocaleString()}
                </div>
              </button>
            </li>
          ))}
          {threads.length === 0 && (
            <li className="text-sm text-slate-400 py-4 text-center">{t('admin.msgInboxEmpty')}</li>
          )}
        </ul>
      </section>

      <section className="lg:col-span-2">
        {!selectedPhone && (
          <div className="bg-white border border-dashed border-slate-300 rounded-lg p-8 text-center text-sm text-slate-500">
            {t('admin.msgInboxSelect')}
          </div>
        )}
        {selectedPhone && (
          <div className="bg-white border border-slate-200 rounded-lg flex flex-col" style={{ minHeight: '60vh' }}>
            <div className="border-b border-slate-100 px-4 py-3 flex items-start justify-between">
              <div>
                <div className="font-bold text-emerald-800">{thread?.name ?? selectedPhone}</div>
                <div className="text-xs text-slate-500 font-mono">{selectedPhone}</div>
                {thread && !thread.parentRegistered && (
                  <div className="text-[10px] uppercase tracking-wide bg-amber-100 text-amber-800 px-1.5 py-0.5 rounded inline-block mt-1">
                    {t('admin.msgInboxUnregistered')}
                  </div>
                )}
              </div>
              <button onClick={() => openThread(selectedPhone)} className="text-sm text-emerald-700 hover:underline">↻</button>
            </div>

            <div className="flex-1 overflow-y-auto px-4 py-3 space-y-2 bg-slate-50" style={{ maxHeight: '50vh' }}>
              {loadingThread && <div className="text-sm text-slate-500">{t('common.loading')}</div>}
              {!loadingThread && thread?.messages.map((m, idx) => (
                <div key={idx} className={`flex ${m.direction === 1 ? 'justify-end' : 'justify-start'}`}>
                  <div className={`max-w-[75%] rounded-lg px-3 py-2 text-sm whitespace-pre-wrap break-words ${m.direction === 1
                    ? 'bg-emerald-600 text-white'
                    : 'bg-white border border-slate-200 text-slate-800'}`}>
                    <div>{m.body || '—'}</div>
                    <div className={`text-[10px] mt-1 ${m.direction === 1 ? 'text-emerald-100' : 'text-slate-400'}`}>
                      {MESSAGE_CHANNEL_LABELS[m.channel]} · {new Date(m.at).toLocaleString()}
                      {m.direction === 1 && m.status !== null && m.status !== undefined && (
                        <span> · {MESSAGE_DELIVERY_LABELS[m.status]}</span>
                      )}
                    </div>
                  </div>
                </div>
              ))}
              {!loadingThread && thread?.messages.length === 0 && (
                <div className="text-sm text-slate-400 text-center py-8">{t('admin.msgInboxNoMessages')}</div>
              )}
            </div>

            <form onSubmit={send} className="border-t border-slate-100 p-3 space-y-2">
              <div className="flex items-center gap-2">
                <label className="text-xs text-slate-600">{t('admin.msgChannel')}:</label>
                {[0, 1].map(c => (
                  <button key={c} type="button"
                    disabled={!channelAvailable(c as MessageChannel)}
                    onClick={() => setReplyChannel(c as MessageChannel)}
                    className={`px-2 py-1 rounded text-xs border ${replyChannel === c
                      ? 'bg-emerald-700 text-white border-emerald-700'
                      : 'bg-white text-slate-700 border-slate-300'} disabled:opacity-40 disabled:cursor-not-allowed`}
                  >{MESSAGE_CHANNEL_LABELS[c as MessageChannel]}</button>
                ))}
              </div>
              <div className="flex items-end gap-2">
                <textarea rows={2} value={replyBody}
                  onChange={e => setReplyBody(e.target.value)}
                  placeholder={t('admin.msgInboxReplyPlaceholder')}
                  maxLength={2000}
                  className="flex-1 border border-slate-300 rounded-md px-3 py-2 text-sm" />
                <button type="submit" disabled={sending || !replyBody.trim()}
                  className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                  {sending ? t('admin.sending') : t('admin.msgInboxSend')}
                </button>
              </div>
            </form>
          </div>
        )}
      </section>
    </div>
  )
}

// --- Settings tab ----------------------------------------------------------

function SettingsTab({
  onError, onNotice,
}: {
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [autoReplyEnabled, setAutoReplyEnabled] = useState(true)
  const [autoReplyTextEn, setAutoReplyTextEn] = useState('')
  const [autoReplyTextEs, setAutoReplyTextEs] = useState('')
  const [updatedAt, setUpdatedAt] = useState<string | null>(null)
  const [loaded, setLoaded] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    Api.getMessagingSettings()
      .then(s => {
        setAutoReplyEnabled(s.autoReplyEnabled)
        setAutoReplyTextEn(s.autoReplyTextEn)
        setAutoReplyTextEs(s.autoReplyTextEs)
        setUpdatedAt(s.updatedAt)
        setLoaded(true)
      })
      .catch(e => onError(extractError(e)))
  }, [])

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!autoReplyTextEn.trim() || !autoReplyTextEs.trim()) {
      onError(t('admin.msgSettingsBodiesRequired'))
      return
    }
    setSaving(true)
    try {
      const s = await Api.updateMessagingSettings({
        autoReplyEnabled,
        autoReplyTextEn: autoReplyTextEn.trim(),
        autoReplyTextEs: autoReplyTextEs.trim(),
      })
      setUpdatedAt(s.updatedAt)
      onNotice(t('admin.msgSettingsSaved'))
    } catch (e: any) {
      onError(extractError(e))
    } finally {
      setSaving(false)
    }
  }

  if (!loaded) {
    return <div className="text-sm text-slate-500">{t('common.loading')}</div>
  }

  return (
    <form onSubmit={save} className="bg-white border border-slate-200 rounded-lg p-6 space-y-4 max-w-3xl">
      <div>
        <h2 className="font-bold text-emerald-800">{t('admin.msgSettingsAutoReplyHeader')}</h2>
        <p className="text-xs text-slate-500 mt-1">{t('admin.msgSettingsAutoReplyHelp')}</p>
      </div>

      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" className="w-4 h-4"
          checked={autoReplyEnabled}
          onChange={e => setAutoReplyEnabled(e.target.checked)}
        />
        <span className="text-slate-700">{t('admin.msgSettingsAutoReplyEnabled')}</span>
      </label>

      <label className="block text-sm">
        <span className="font-medium text-slate-700">{t('admin.msgSettingsAutoReplyEn')}</span>
        <textarea rows={4} value={autoReplyTextEn}
          onChange={e => setAutoReplyTextEn(e.target.value)}
          maxLength={2000}
          className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
        <span className="block text-xs text-slate-500 mt-0.5">{autoReplyTextEn.length} / 2000</span>
      </label>

      <label className="block text-sm">
        <span className="font-medium text-slate-700">{t('admin.msgSettingsAutoReplyEs')}</span>
        <textarea rows={4} value={autoReplyTextEs}
          onChange={e => setAutoReplyTextEs(e.target.value)}
          maxLength={2000}
          className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
        <span className="block text-xs text-slate-500 mt-0.5">{autoReplyTextEs.length} / 2000</span>
      </label>

      <div className="flex items-center justify-between pt-2 border-t border-slate-100">
        <button type="submit" disabled={saving}
          className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800 disabled:opacity-60">
          {saving ? t('register.submitting') : t('admin.save')}
        </button>
        {updatedAt && (
          <span className="text-xs text-slate-500">
            {t('admin.msgSettingsLastUpdated', { when: new Date(updatedAt).toLocaleString() })}
          </span>
        )}
      </div>
    </form>
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
  const [inbound, setInbound] = useState<InboundMessage[]>([])

  const loadInbound = async () => {
    try { setInbound(await Api.listInboundMessages()) }
    catch (e: any) { void e /* surface errors elsewhere if needed */ }
  }
  useEffect(() => { loadInbound() }, [])

  const refreshBoth = async () => {
    await Promise.all([onRefresh(), loadInbound()])
  }

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

      <div className="mt-6">
        <div className="flex items-center justify-between mb-2">
          <h3 className="font-medium text-slate-700">{t('admin.msgInboundHeader')}</h3>
          <button onClick={refreshBoth} className="text-sm text-emerald-700 hover:underline">↻</button>
        </div>
        <p className="text-xs text-slate-500 mb-2">{t('admin.msgInboundHelp')}</p>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-slate-500 border-b">
              <th className="py-2 pr-4">{t('admin.msgWhen')}</th>
              <th className="py-2 pr-4">{t('admin.msgChannel')}</th>
              <th className="py-2 pr-4">{t('admin.msgInboundFrom')}</th>
              <th className="py-2 pr-4">{t('admin.msgBody')}</th>
              <th className="py-2 pr-4">{t('admin.msgInboundReplyTo')}</th>
            </tr>
          </thead>
          <tbody>
            {inbound.map(m => (
              <tr key={m.id} className="border-b last:border-0 align-top">
                <td className="py-2 pr-4 whitespace-nowrap text-slate-500">{new Date(m.receivedAt).toLocaleString()}</td>
                <td className="py-2 pr-4">{MESSAGE_CHANNEL_LABELS[m.channel]}</td>
                <td className="py-2 pr-4 font-mono text-xs">{m.fromPhone}</td>
                <td className="py-2 pr-4 max-w-xl whitespace-pre-wrap break-words">{m.body ?? '—'}</td>
                <td className="py-2 pr-4 max-w-xs text-xs text-slate-500">
                  {m.broadcastId
                    ? (<><span className="text-slate-700 font-medium">#{m.broadcastId}</span>{m.broadcastSummary ? <> · <span className="line-clamp-2">{m.broadcastSummary}</span></> : null}</>)
                    : <span className="text-slate-400">—</span>}
                </td>
              </tr>
            ))}
            {inbound.length === 0 && (
              <tr><td colSpan={5} className="py-4 text-center text-slate-400">{t('admin.msgInboundEmpty')}</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </section>
  )
}

// --- Templates tab ---------------------------------------------------------

function TemplatesTab({
  templates, emailTemplates, onChanged, onError, onNotice,
}: {
  templates: WhatsAppTemplate[]
  emailTemplates: EmailTemplate[]
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [kind, setKind] = useState<'whatsapp' | 'email'>('whatsapp')
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
    <div className="space-y-4">
      <div className="flex gap-2">
        <button type="button" onClick={() => { setKind('whatsapp'); setEditingId(null) }}
          className={`px-3 py-2 rounded-md border text-sm ${kind === 'whatsapp' ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}
        >{t('admin.msgTemplateKindWhatsApp')}</button>
        <button type="button" onClick={() => { setKind('email'); setEditingId(null) }}
          className={`px-3 py-2 rounded-md border text-sm ${kind === 'email' ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}
        >{t('admin.msgTemplateKindEmail')}</button>
      </div>

      {kind === 'email' ? (
        <EmailTemplatesSection
          emailTemplates={emailTemplates}
          onChanged={onChanged}
          onError={onError}
          onNotice={onNotice}
        />
      ) : (
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
      )}
    </div>
  )
}

// --- Email templates section (rendered inside TemplatesTab when kind=email) -

function EmailTemplatesSection({
  emailTemplates, onChanged, onError, onNotice,
}: {
  emailTemplates: EmailTemplate[]
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  const [editingId, setEditingId] = useState<number | 'new' | null>(null)
  const [name, setName] = useState('')
  const [language, setLanguage] = useState<Language>(0)
  const [description, setDescription] = useState('')
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')
  const [vars, setVars] = useState<SaveTemplateVariable[]>([])

  const loadForm = (tpl: EmailTemplate | null) => {
    if (tpl) {
      setEditingId(tpl.id)
      setName(tpl.name); setLanguage(tpl.language)
      setDescription(tpl.description ?? ''); setSubject(tpl.subject); setBody(tpl.body)
      setVars(tpl.variables.map(v => ({ position: v.position, label: v.label, example: v.example })))
    } else {
      setEditingId('new')
      setName(''); setLanguage(0)
      setDescription(''); setSubject(''); setBody(''); setVars([])
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
    if (!subject.trim()) { onError('Subject is required.'); return }
    if (!body.trim()) { onError('Body is required.'); return }
    const payload = {
      name: name.trim(),
      language,
      description: description.trim() || null,
      subject: subject.trim(),
      body,
      variables: vars.filter(v => v.label.trim()),
    }
    try {
      if (editingId === 'new' || editingId === null) {
        await Api.createEmailTemplate(payload)
        onNotice(`Created email template "${payload.name}".`)
      } else {
        await Api.updateEmailTemplate(editingId, payload)
        onNotice(`Updated email template "${payload.name}".`)
      }
      setEditingId(null)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  const remove = async () => {
    if (typeof editingId !== 'number') return
    if (!confirm(`Delete email template "${name}"? Past broadcasts are unaffected.`)) return
    try {
      await Api.deleteEmailTemplate(editingId)
      setEditingId(null)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  return (
    <div className="grid lg:grid-cols-3 gap-4">
      <section className="bg-white border border-slate-200 rounded-lg p-4 lg:col-span-1 space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="font-bold text-emerald-800">{t('admin.msgEmailTemplatesHeader')}</h2>
          <button onClick={() => loadForm(null)}
            className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgNewTemplate')}</button>
        </div>
        <ul className="space-y-1">
          {emailTemplates.map(tpl => (
            <li key={tpl.id}>
              <button onClick={() => loadForm(tpl)}
                className={`w-full text-left px-2 py-1.5 rounded text-sm hover:bg-emerald-50 ${editingId === tpl.id ? 'bg-emerald-50 text-emerald-800 font-medium' : ''}`}>
                <div>{tpl.name} <span className="text-xs text-slate-400">{tpl.language === 1 ? 'ES' : 'EN'}</span></div>
                <div className="text-xs text-slate-500 truncate">{tpl.subject}</div>
              </button>
            </li>
          ))}
          {emailTemplates.length === 0 && <li className="text-sm text-slate-400">{t('admin.msgNoTemplates')}</li>}
        </ul>
        <p className="text-xs text-slate-500">{t('admin.msgEmailTemplatesHint')}</p>
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
                  placeholder="practice_reminder"
                  className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
              </label>
              <label className="block text-sm">
                <span className="font-medium text-slate-700">{t('admin.language')}</span>
                <select value={language} onChange={e => setLanguage(Number(e.target.value) as Language)}
                  className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
                  <option value={0}>English</option>
                  <option value={1}>Español</option>
                </select>
              </label>
              <label className="block text-sm sm:col-span-2">
                <span className="font-medium text-slate-700">{t('admin.msgTemplateDescription')}</span>
                <input type="text" value={description} onChange={e => setDescription(e.target.value)}
                  className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
              </label>
            </div>

            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgSubject')}</span>
              <input type="text" value={subject} onChange={e => setSubject(e.target.value)}
                placeholder={t('admin.msgEmailSubjectTemplatePh')}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>

            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgBody')}</span>
              <textarea rows={10} value={body} onChange={e => setBody(e.target.value)}
                placeholder={t('admin.msgEmailBodyTemplatePh')}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm font-mono" />
              <span className="block text-xs text-slate-500 mt-1">{t('admin.msgEmailTemplatePlaceholderHelp')}</span>
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
                {editingId === 'new' ? t('admin.msgSave') : t('admin.msgSave')}
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
    // Email-first detection: if the line contains an email-shaped token, treat the line as an
    // email recipient (with optional preceding name). Otherwise fall back to phone parsing.
    const emailMatch = trimmed.match(/\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b/)
    if (emailMatch) {
      const email = emailMatch[0]
      const key = `email:${email.toLowerCase()}`
      if (seen.has(key)) continue
      seen.add(key)
      const before = trimmed.slice(0, emailMatch.index).trim()
      const name = before.replace(/[,:]\s*$/, '').replace(/^~+/, '').replace(/~+/g, ' ').trim()
      out.push({ phone: '', name: name || null, email })
      continue
    }
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

// --- Team schedule (admin-managed games + practices) --------------------

function TeamScheduleSection({
  teamId, games, onChanged, onError, onNotice,
}: {
  teamId: number
  games: ScheduledGame[]
  onChanged: () => Promise<void> | void
  onError: (e: string) => void
  onNotice: (n: string) => void
}) {
  const { t } = useTranslation()
  // Unified, time-sorted list. Games and practices show in the same table; admin distinguishes
  // by the Kind badge on each row.
  const events = useMemo(
    () => [...games].sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime()),
    [games])

  // `editingId` discriminator:
  //   'new-practice' | 'new-game' | 'series' → opening one of the three new forms
  //   number → editing an existing row (Kind taken from the row)
  //   null → no form open
  const [editingId, setEditingId] = useState<number | 'new-practice' | 'new-game' | 'series' | null>(null)
  const [editingKind, setEditingKind] = useState<'practice' | 'game'>('practice')
  // Game-specific fields (only meaningful when the active form is a game form):
  const [opponentName, setOpponentName] = useState('')
  const [isHome, setIsHome] = useState<boolean | null>(null)
  const [startsAt, setStartsAt] = useState('')      // datetime-local format (local time)
  const [endsAt, setEndsAt] = useState('')
  const [location, setLocation] = useState('')
  const [summary, setSummary] = useState('')

  // Recurring-series form state (only used when editingId === 'series'):
  const [seriesStartDate, setSeriesStartDate] = useState('')
  const [seriesEndDate, setSeriesEndDate] = useState('')
  const [seriesStartTime, setSeriesStartTime] = useState('17:00')
  const [seriesEndTime, setSeriesEndTime] = useState('')
  const [seriesDays, setSeriesDays] = useState<Set<number>>(new Set())

  const startNewPractice = () => {
    setEditingId('new-practice'); setEditingKind('practice')
    setStartsAt(''); setEndsAt(''); setLocation(''); setSummary('')
    setOpponentName(''); setIsHome(null)
  }
  const startNewGame = () => {
    setEditingId('new-game'); setEditingKind('game')
    setStartsAt(''); setEndsAt(''); setLocation(''); setSummary('')
    setOpponentName(''); setIsHome(null)
  }
  const startSeries = () => {
    setEditingId('series'); setEditingKind('practice')
    setSeriesStartDate(''); setSeriesEndDate('')
    setSeriesStartTime('17:00'); setSeriesEndTime('')
    setSeriesDays(new Set()); setLocation(''); setSummary('')
  }
  const toggleDay = (d: number) => {
    setSeriesDays(prev => {
      const next = new Set(prev)
      if (next.has(d)) next.delete(d); else next.add(d)
      return next
    })
  }
  const startEdit = (ev: ScheduledGame) => {
    setEditingId(ev.id)
    setEditingKind(ev.kind === 0 ? 'game' : 'practice')
    // datetime-local wants YYYY-MM-DDTHH:mm in local time (no timezone). Strip seconds/ms.
    setStartsAt(toDateTimeLocal(ev.startsAt))
    setEndsAt(ev.endsAt ? toDateTimeLocal(ev.endsAt) : '')
    setLocation(ev.location ?? '')
    setSummary(ev.summary ?? '')
    setOpponentName(ev.opponentName ?? '')
    setIsHome(ev.isHome)
  }

  const save = async (e: React.FormEvent) => {
    e.preventDefault()
    if (editingId === 'series') {
      if (!seriesStartDate || !seriesEndDate) { onError('Start and end date are required.'); return }
      if (seriesDays.size === 0) { onError('Pick at least one day of the week.'); return }
      try {
        const r = await Api.createPracticeSeries(teamId, {
          startDate: seriesStartDate,
          endDate: seriesEndDate,
          startTime: seriesStartTime,
          endTime: seriesEndTime || null,
          daysOfWeek: Array.from(seriesDays).sort(),
          location: location.trim() || null,
          summary: summary.trim() || null,
        })
        setEditingId(null)
        await onChanged()
        onNotice(t('admin.msgPracticeSeriesCreated', { count: r.count }))
      } catch (e: any) { onError(extractError(e)) }
      return
    }
    if (!startsAt) { onError('Start date/time is required.'); return }
    try {
      const startsAtIso = new Date(startsAt).toISOString()
      const endsAtIso = endsAt ? new Date(endsAt).toISOString() : null
      const trimmedLocation = location.trim() || null
      const trimmedSummary = summary.trim() || null
      if (editingKind === 'game') {
        const payload = {
          startsAt: startsAtIso,
          endsAt: endsAtIso,
          opponentName: opponentName.trim() || null,
          isHome,
          location: trimmedLocation,
          summary: trimmedSummary,
        }
        if (editingId === 'new-game') await Api.createGame(teamId, payload)
        else if (typeof editingId === 'number') await Api.updateGame(editingId, payload)
        onNotice(t('admin.msgGameSaved'))
      } else {
        const payload = {
          startsAt: startsAtIso,
          endsAt: endsAtIso,
          location: trimmedLocation,
          summary: trimmedSummary,
        }
        if (editingId === 'new-practice') await Api.createPractice(teamId, payload)
        else if (typeof editingId === 'number') await Api.updatePractice(editingId, payload)
        onNotice(t('admin.msgPracticeSaved'))
      }
      setEditingId(null)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  const remove = async (ev: ScheduledGame) => {
    const label = ev.kind === 0 ? 'game' : 'practice'
    if (!confirm(`Delete this ${label} on ${new Date(ev.startsAt).toLocaleString()}?`)) return
    try {
      if (ev.kind === 0) await Api.deleteGame(ev.id)
      else await Api.deletePractice(ev.id)
      await onChanged()
    } catch (e: any) { onError(extractError(e)) }
  }

  const cancel = async (ev: ScheduledGame) => {
    const label = ev.kind === 0 ? 'game' : 'practice'
    if (!confirm(`Cancel this ${label} on ${new Date(ev.startsAt).toLocaleString()}? You'll be able to notify parents who already received the reminder.`)) return
    try {
      if (ev.kind === 0) await Api.cancelGame(ev.id)
      else await Api.cancelPractice(ev.id)
      await onChanged()
      onNotice(t('admin.msgPracticeCancelled'))
    } catch (e: any) { onError(extractError(e)) }
  }

  const [notifyState, setNotifyState] = useState<{
    practice: ScheduledGame
    recipients: EventRecipient[]
    bodyEn: string
    bodyEs: string
  } | null>(null)
  const [sendingNotify, setSendingNotify] = useState(false)

  const startNotify = async (p: ScheduledGame) => {
    try {
      const recipients = await Api.listEventRecipients(p.id)
      const when = new Date(p.startsAt).toLocaleString()
      const where = p.location ? ` at ${p.location}` : ''
      setNotifyState({
        practice: p,
        recipients,
        bodyEn: `The practice scheduled for ${when}${where} has been cancelled. Sorry for the late notice.`,
        bodyEs: `La práctica programada para ${when}${where ? ` en ${p.location}` : ''} ha sido cancelada. Disculpe el aviso tardío.`,
      })
    } catch (e: any) { onError(extractError(e)) }
  }

  const sendNotify = async () => {
    if (!notifyState) return
    if (notifyState.recipients.length === 0) {
      onError(t('admin.msgNotifyNoRecipients'))
      return
    }
    setSendingNotify(true)
    try {
      await Api.createBroadcast({
        channel: 0, // SMS — free-form cancellation works anytime without a template
        bodyEn: notifyState.bodyEn.trim() || null,
        bodyEs: notifyState.bodyEs.trim() || null,
        scheduledGameId: notifyState.practice.id,
        target: {
          kind: 3, // AdHocList
          recipients: notifyState.recipients.map(r => ({ phone: r.phone, name: r.name })),
        },
      })
      onNotice(t('admin.msgCancellationSent', { count: notifyState.recipients.length }))
      setNotifyState(null)
    } catch (e: any) { onError(extractError(e)) }
    finally { setSendingNotify(false) }
  }

  return (
    <div className="bg-white border border-slate-200 rounded-lg p-4 space-y-4">
      <div>
        <div className="flex items-center justify-between mb-2 flex-wrap gap-2">
          <h3 className="font-medium text-slate-700">{t('admin.msgTeamScheduleHeader')}</h3>
          {editingId === null && (
            <div className="flex gap-3 flex-wrap">
              <button onClick={startNewGame}
                className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddGame')}</button>
              <button onClick={startNewPractice}
                className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddPractice')}</button>
              <button onClick={startSeries}
                className="text-sm text-emerald-700 hover:underline">+ {t('admin.msgAddPracticeSeries')}</button>
            </div>
          )}
        </div>

        {editingId !== null && editingId !== 'series' && (
          <form onSubmit={save} className="border border-slate-200 rounded p-3 grid sm:grid-cols-2 gap-2 mb-3">
            <div className="sm:col-span-2 text-xs uppercase tracking-wide text-slate-500">
              {editingKind === 'game' ? t('admin.msgFormGame') : t('admin.msgFormPractice')}
            </div>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgPracticeStart')}</span>
              <input type="datetime-local" value={startsAt} onChange={e => setStartsAt(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgPracticeEnd')}</span>
              <input type="datetime-local" value={endsAt} onChange={e => setEndsAt(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            {editingKind === 'game' && (
              <>
                <label className="block text-sm">
                  <span className="font-medium text-slate-700">{t('admin.msgGameOpponent')}</span>
                  <input type="text" value={opponentName} onChange={e => setOpponentName(e.target.value)}
                    placeholder="PRIME SC B17 White"
                    className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
                </label>
                <label className="block text-sm">
                  <span className="font-medium text-slate-700">{t('admin.msgGameHomeAway')}</span>
                  <select
                    value={isHome === null ? '' : isHome ? 'home' : 'away'}
                    onChange={e => setIsHome(e.target.value === '' ? null : e.target.value === 'home')}
                    className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm">
                    <option value="">— {t('admin.msgGameHomeAwayUnknown')} —</option>
                    <option value="home">{t('admin.msgGameHome')}</option>
                    <option value="away">{t('admin.msgGameAway')}</option>
                  </select>
                </label>
              </>
            )}
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.msgLocation')}</span>
              <input type="text" value={location} onChange={e => setLocation(e.target.value)}
                placeholder="Sunset Park, field 3"
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.msgPracticeLabel')}</span>
              <input type="text" value={summary} onChange={e => setSummary(e.target.value)}
                placeholder={editingKind === 'game' ? 'Game' : 'Practice'}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <div className="sm:col-span-2 flex items-center gap-3 pt-2">
              <button type="submit"
                className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
                {editingId === 'new-practice' ? t('admin.msgAddPractice') :
                 editingId === 'new-game' ? t('admin.msgAddGame') :
                 t('admin.msgSave')}
              </button>
              <button type="button" onClick={() => setEditingId(null)}
                className="text-sm text-slate-600 hover:underline">{t('admin.msgCancel')}</button>
            </div>
          </form>
        )}

        {editingId === 'series' && (
          <form onSubmit={save} className="border border-slate-200 rounded p-3 grid sm:grid-cols-2 gap-2 mb-3">
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgSeriesStartDate')}</span>
              <input type="date" value={seriesStartDate} onChange={e => setSeriesStartDate(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgSeriesEndDate')}</span>
              <input type="date" value={seriesEndDate} onChange={e => setSeriesEndDate(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgSeriesStartTime')}</span>
              <input type="time" value={seriesStartTime} onChange={e => setSeriesStartTime(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm">
              <span className="font-medium text-slate-700">{t('admin.msgSeriesEndTime')}</span>
              <input type="time" value={seriesEndTime} onChange={e => setSeriesEndTime(e.target.value)}
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <div className="sm:col-span-2">
              <span className="block text-sm font-medium text-slate-700 mb-1">{t('admin.msgSeriesDays')}</span>
              <div className="flex flex-wrap gap-1">
                {[
                  { d: 0, label: t('admin.msgDaySun') },
                  { d: 1, label: t('admin.msgDayMon') },
                  { d: 2, label: t('admin.msgDayTue') },
                  { d: 3, label: t('admin.msgDayWed') },
                  { d: 4, label: t('admin.msgDayThu') },
                  { d: 5, label: t('admin.msgDayFri') },
                  { d: 6, label: t('admin.msgDaySat') },
                ].map(({ d, label }) => (
                  <button key={d} type="button" onClick={() => toggleDay(d)}
                    className={`text-xs px-2 py-1 rounded border ${seriesDays.has(d) ? 'bg-emerald-700 text-white border-emerald-700' : 'bg-white border-slate-300 text-slate-700'}`}>
                    {label}
                  </button>
                ))}
              </div>
            </div>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.msgLocation')}</span>
              <input type="text" value={location} onChange={e => setLocation(e.target.value)}
                placeholder="Sunset Park, field 3"
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <label className="block text-sm sm:col-span-2">
              <span className="font-medium text-slate-700">{t('admin.msgPracticeLabel')}</span>
              <input type="text" value={summary} onChange={e => setSummary(e.target.value)}
                placeholder="Practice"
                className="mt-1 w-full border border-slate-300 rounded-md px-3 py-2 text-sm" />
            </label>
            <div className="sm:col-span-2 flex items-center gap-3 pt-2">
              <button type="submit"
                className="bg-emerald-700 text-white text-sm font-semibold px-4 py-2 rounded-md hover:bg-emerald-800">
                {t('admin.msgCreateSeries')}
              </button>
              <button type="button" onClick={() => setEditingId(null)}
                className="text-sm text-slate-600 hover:underline">{t('admin.msgCancel')}</button>
            </div>
          </form>
        )}

        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-slate-500 border-b">
              <th className="py-1 pr-4">{t('admin.msgWhen')}</th>
              <th className="py-1 pr-4">{t('admin.msgKind')}</th>
              <th className="py-1 pr-4">{t('admin.msgSummary')}</th>
              <th className="py-1 pr-4">{t('admin.msgLocation')}</th>
              <th className="py-1 pr-4"></th>
            </tr>
          </thead>
          <tbody>
            {events.map(ev => {
              const isGame = ev.kind === 0
              const summary = isGame
                ? (ev.opponentName ? `vs ${ev.opponentName}` : (ev.summary ?? 'Game'))
                : (ev.summary ?? 'Practice')
              const homeAway = isGame && ev.isHome === true ? ' (H)' : isGame && ev.isHome === false ? ' (A)' : ''
              const kindBadgeClass = isGame ? 'bg-blue-100 text-blue-800' : 'bg-purple-100 text-purple-800'
              return (
                <tr key={ev.id} className={`border-b last:border-0 ${ev.isCancelled ? 'text-slate-400 line-through' : ''}`}>
                  <td className="py-1 pr-4 whitespace-nowrap">{new Date(ev.startsAt).toLocaleString()}</td>
                  <td className="py-1 pr-4 no-underline">
                    <span className={`inline-block text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded ${kindBadgeClass}`}>
                      {isGame ? t('admin.msgKindGame') : t('admin.msgKindPractice')}
                    </span>
                  </td>
                  <td className="py-1 pr-4">
                    {summary}{homeAway}
                    {ev.seriesId && <span className="ml-2 inline-block text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-slate-100 text-slate-600 no-underline">{t('admin.msgSeriesBadge')}</span>}
                    {ev.isCancelled && <span className="ml-2 inline-block text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded bg-rose-100 text-rose-700 no-underline">{t('admin.msgCancelledBadge')}</span>}
                  </td>
                  <td className="py-1 pr-4">{ev.location ?? '—'}</td>
                  <td className="py-1 pr-4 text-right whitespace-nowrap no-underline">
                    {!ev.isCancelled && (
                      <>
                        <button onClick={() => startEdit(ev)}
                          className="text-emerald-700 hover:underline">{t('admin.details')}</button>
                        <span className="mx-2 text-slate-300">|</span>
                        <button onClick={() => cancel(ev)}
                          className="text-amber-700 hover:underline">{t('admin.msgCancelPractice')}</button>
                        <span className="mx-2 text-slate-300">|</span>
                        <button onClick={() => remove(ev)}
                          className="text-rose-700 hover:underline">{t('admin.delete')}</button>
                      </>
                    )}
                    {ev.isCancelled && (
                      <button onClick={() => startNotify(ev)}
                        className="text-emerald-700 hover:underline">{t('admin.msgNotifyParents')}</button>
                    )}
                  </td>
                </tr>
              )
            })}
            {events.length === 0 && (
              <tr><td colSpan={5} className="py-3 text-center text-slate-400">{t('admin.msgScheduleEmpty')}</td></tr>
            )}
          </tbody>
        </table>

        {notifyState && (
          <div className="mt-3 border border-amber-300 bg-amber-50 rounded p-3 space-y-2">
            <div className="text-sm">
              <strong>{t('admin.msgNotifyHeader', { count: notifyState.recipients.length })}</strong>
              <div className="text-xs text-slate-600 mt-1">{t('admin.msgNotifyHelp')}</div>
            </div>
            <div className="grid md:grid-cols-2 gap-2">
              <label className="block text-xs">
                <span className="font-medium text-slate-700">English</span>
                <textarea rows={3} value={notifyState.bodyEn}
                  onChange={e => setNotifyState(s => s ? { ...s, bodyEn: e.target.value } : s)}
                  className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
              </label>
              <label className="block text-xs">
                <span className="font-medium text-slate-700">Español</span>
                <textarea rows={3} value={notifyState.bodyEs}
                  onChange={e => setNotifyState(s => s ? { ...s, bodyEs: e.target.value } : s)}
                  className="mt-1 w-full border border-slate-300 rounded-md px-2 py-1 text-sm" />
              </label>
            </div>
            <div className="flex items-center gap-3">
              <button onClick={sendNotify} disabled={sendingNotify}
                className="bg-emerald-700 text-white text-sm font-semibold px-3 py-1.5 rounded-md hover:bg-emerald-800 disabled:opacity-60">
                {sendingNotify ? t('admin.sending') : t('admin.msgSendCancellation')}
              </button>
              <button onClick={() => setNotifyState(null)}
                className="text-sm text-slate-600 hover:underline">{t('admin.msgCancel')}</button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

/** Format a UTC ISO string as a local-time value usable in &lt;input type="datetime-local"&gt;.
 *  datetime-local has no timezone; this strips to local YYYY-MM-DDTHH:mm. */
function toDateTimeLocal(iso: string): string {
  const d = new Date(iso)
  const pad = (n: number) => n.toString().padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

// --- Schedule helpers ----------------------------------------------------

function formatGameOption(g: ScheduledGame): string {
  const d = new Date(g.startsAt)
  const date = d.toLocaleDateString(undefined, { weekday: 'short', month: 'numeric', day: 'numeric' })
  const time = d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })
  // Practice events get a clear prefix so admin doesn't confuse them with games. Games show
  // home/away + opponent. The Compose picker uses the Kind to also auto-select the right template.
  if (g.kind === 1) {
    return `[Practice] ${date} ${time}${g.location ? ` @ ${g.location}` : ''}`
  }
  const homeAway = g.isHome === true ? ' (H)' : g.isHome === false ? ' (A)' : ''
  const label = g.opponentName ? `vs ${g.opponentName}` : (g.summary?.trim() || g.teamName)
  return `${date} ${time}${homeAway} — ${label}${g.location ? ` @ ${g.location}` : ''}`
}

/**
 * When the admin picks an event in Compose, pick the matching template automatically based on
 * Kind: a Practice event needs a practice-flavored template; a Game event needs a game-flavored
 * one. Within a kind we prefer English (the pair-routing layer picks ES at send time when
 * recipients prefer it). Returns the template ID or '' if no kind-matching template exists.
 */
function pickTemplateForEvent(
  event: ScheduledGame,
  templates: WhatsAppTemplate[],
): number | '' {
  const kindMatch = (t: WhatsAppTemplate): boolean => {
    const n = t.name.toLowerCase()
    return event.kind === 1 ? n.includes('practice') : n.includes('game')
  }
  const englishFirst = (a: WhatsAppTemplate, b: WhatsAppTemplate) => a.language - b.language
  const match = templates.filter(kindMatch).sort(englishFirst)[0]
  return match?.id ?? ''
}

/**
 * Auto-fills both bodyEn and bodyEs with a full sentence derived from a picked game. Used by the
 * free-form Compose path where there's no template to slot values into — admin gets a complete
 * draft they can tweak in the bilingual preview modal. Mirrors the same EN/ES wording the
 * canonical practice_or_game template uses so messages stay consistent across the two send modes.
 */
function applyGameToFreeForm(
  game: ScheduledGame,
  setBodyEn: (v: string) => void,
  setBodyEs: (v: string) => void,
) {
  const d = new Date(game.startsAt)
  const fmt = (locale: string) =>
    `${d.toLocaleDateString(locale, { weekday: 'short', month: 'numeric', day: 'numeric' })} ${d.toLocaleTimeString(locale, { hour: 'numeric', minute: '2-digit' })}`

  const buildLine = (lang: Language): string => {
    const what = game.opponentName
      ? `${GAME_VS_PREFIX[lang]} ${game.opponentName}`
      : (game.summary?.trim() || PRACTICE_FALLBACK[lang])
    const when = fmt(lang === 1 ? 'es-US' : 'en-US')
    const where = game.location?.trim() || ''
    const wear = game.isHome === true ? WEAR_HOME[lang]
               : game.isHome === false ? WEAR_AWAY[lang]
               : null

    // Localized scaffolding ("on/at" → "el/en", "Wear:" → "Vestimenta:") so the free-form text
    // reads naturally in the recipient's language.
    if (lang === 1) {
      const main = where ? `${what} el ${when} en ${where}.` : `${what} el ${when}.`
      return wear ? `${main} Vestimenta: ${wear}.` : main
    }
    const enMain = where ? `${what} on ${when} at ${where}.` : `${what} on ${when}.`
    return wear ? `${enMain} Wear: ${wear}.` : enMain
  }

  setBodyEn(buildLine(0))
  setBodyEs(buildLine(1))
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
      // Twilio expects positional keys ("1", "2", ...) so the values dict is keyed by Position.
      const key = v.position.toString()
      if (label.includes('opponent')) {
        next[key] = game.opponentName ?? ''
      } else if (label.includes('what')) {
        // Legacy support for the older single-template (`practice_or_game`) that used a "what"
        // variable combining opponent + practice/game distinction.
        next[key] = game.opponentName
          ? `${GAME_VS_PREFIX[lang]} ${game.opponentName}`
          : (game.summary?.trim() || PRACTICE_FALLBACK[lang])
      } else if (label.includes('when')) {
        const d = new Date(game.startsAt)
        next[key] = `${d.toLocaleDateString(locale, { weekday: 'short', month: 'numeric', day: 'numeric' })} ${d.toLocaleTimeString(locale, { hour: 'numeric', minute: '2-digit' })}`
      } else if (label.includes('where') || label.includes('location')) {
        next[key] = game.location?.trim() || ''
      } else if (label.includes('uniform') || label.includes('wear')) {
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

        {detail && (
          <TeamScheduleSection
            teamId={detail.id}
            games={detail.upcomingGames}
            onChanged={async () => {
              // Reload the team detail so the upcoming list reflects the new/edited/deleted practice.
              const d = await Api.getTeam(detail.id)
              setDetail(d)
            }}
            onError={onError}
            onNotice={onNotice}
          />
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
  const [preview, setPreview] = useState<TemplatePreviewResponse | null>(null)
  const [previewError, setPreviewError] = useState<string | null>(null)

  // Backend renders both sides so the "Spanish recipient gets English template with translated
  // values" fallback shows what will actually deliver. Single source of truth for what the
  // recipient sees — no client-side guessing.
  useEffect(() => {
    let cancelled = false
    Api.templatePreview({ templateId: template.id, values })
      .then(r => { if (!cancelled) setPreview(r) })
      .catch(e => { if (!cancelled) setPreviewError(extractError(e)) })
    return () => { cancelled = true }
  }, [template.id, values])

  const langLabel = (lang: Language) => lang === 1 ? 'Español' : 'English'

  const renderSide = (side: TemplatePreviewSide) => {
    const sourceLabel =
      side.source === 0 ? t('admin.msgPreviewSourceApproved') :
      side.source === 1 ? t('admin.msgPreviewSourceTranslated') :
      t('admin.msgPreviewSourceUnavailable')
    const sourceClass =
      side.source === 0 ? 'bg-emerald-50 text-emerald-800 border-emerald-200' :
      side.source === 1 ? 'bg-amber-50 text-amber-800 border-amber-200' :
      'bg-slate-50 text-slate-500 border-slate-200'

    return (
      <div>
        <div className="flex items-center justify-between mb-1">
          <div className="text-xs font-medium text-slate-700">
            {langLabel(side.language)} <span className="text-slate-400">— {side.templateName}</span>
          </div>
          <span className={`text-[10px] uppercase tracking-wide px-2 py-0.5 rounded border ${sourceClass}`}>{sourceLabel}</span>
        </div>
        <pre className="w-full border border-slate-200 bg-slate-50 rounded-md px-3 py-2 text-sm whitespace-pre-wrap min-h-[6rem]">{side.rendered ?? '—'}</pre>
        {side.source === 1 && side.values && (
          <details className="mt-1 text-xs text-slate-500">
            <summary className="cursor-pointer hover:underline">{t('admin.msgPreviewTranslatedValuesUsed')}</summary>
            <table className="w-full mt-1">
              <tbody>
                {Object.entries(side.values).map(([k, v]) => (
                  <tr key={k}><td className="pr-3 text-slate-400">{k}</td><td className="font-mono">{v}</td></tr>
                ))}
              </tbody>
            </table>
          </details>
        )}
      </div>
    )
  }

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/40 flex items-start justify-center p-4 overflow-y-auto">
      <div className="bg-white rounded-lg shadow-xl max-w-5xl w-full mt-10 p-6 space-y-4">
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

        {previewError && (
          <div className="text-sm text-rose-700 bg-rose-50 border border-rose-200 rounded-md p-3">{previewError}</div>
        )}
        {!preview && !previewError && (
          <div className="text-sm text-slate-500">{t('admin.msgPreviewLoading')}</div>
        )}
        {preview && (
          <div className="grid md:grid-cols-2 gap-4">
            {renderSide(preview.english)}
            {renderSide(preview.spanish)}
          </div>
        )}

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
