import axios from 'axios'
import type {
  CreateInvitationRequest,
  InvitationResponse,
  InvitationLookupResponse,
  SubmitRegistrationRequest,
  RegistrationSummary,
  RegistrationDetail,
} from './types'

const ADMIN_KEY_STORAGE = 'lvss.adminKey'

export const adminKey = {
  get: () => localStorage.getItem(ADMIN_KEY_STORAGE) ?? '',
  set: (key: string) => localStorage.setItem(ADMIN_KEY_STORAGE, key),
  clear: () => localStorage.removeItem(ADMIN_KEY_STORAGE),
}

const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

function adminHeaders() {
  return { 'X-Admin-Key': adminKey.get() }
}

export const Api = {
  async lookupInvite(token: string) {
    const r = await api.get<InvitationLookupResponse>(`/invitations/by-token/${encodeURIComponent(token)}`)
    return r.data
  },

  async submitRegistration(payload: SubmitRegistrationRequest) {
    const r = await api.post('/registrations', payload)
    return r.data
  },

  async createInvitation(payload: CreateInvitationRequest) {
    const r = await api.post<InvitationResponse>('/invitations', payload, { headers: adminHeaders() })
    return r.data
  },

  async listInvitations() {
    const r = await api.get<InvitationResponse[]>('/invitations', { headers: adminHeaders() })
    return r.data
  },

  async resendInvitation(id: number) {
    const r = await api.post<InvitationResponse>(`/invitations/${id}/resend`, {}, { headers: adminHeaders() })
    return r.data
  },

  async listRegistrations() {
    const r = await api.get<RegistrationSummary[]>('/registrations', { headers: adminHeaders() })
    return r.data
  },

  async getRegistration(id: number) {
    const r = await api.get<RegistrationDetail>(`/registrations/${id}`, { headers: adminHeaders() })
    return r.data
  },

  async viewWaivers(id: number) {
    const r = await api.get(`/registrations/${id}/waivers.pdf`, {
      headers: adminHeaders(),
      responseType: 'blob',
    })
    const url = URL.createObjectURL(r.data as Blob)
    window.open(url, '_blank')
  },

  async downloadWaivers(id: number) {
    const r = await api.get(`/registrations/${id}/waivers.pdf`, {
      headers: adminHeaders(),
      responseType: 'blob',
    })
    const url = URL.createObjectURL(r.data as Blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `waivers-${id}.pdf`
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  },

  async viewPlayerWaiver(regId: number, playerId: number) {
    const r = await api.get(`/registrations/${regId}/players/${playerId}/waiver.pdf`, {
      headers: adminHeaders(),
      responseType: 'blob',
    })
    const url = URL.createObjectURL(r.data as Blob)
    window.open(url, '_blank')
  },

  async downloadPlayerWaiver(regId: number, playerId: number, filenameStem: string) {
    const r = await api.get(`/registrations/${regId}/players/${playerId}/waiver.pdf`, {
      headers: adminHeaders(),
      responseType: 'blob',
    })
    const url = URL.createObjectURL(r.data as Blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `waiver-${filenameStem}.pdf`
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  },
}
