import axios from 'axios'
import type {
  CreateOutreachRequest,
  LoginRequest,
  Me,
  OutreachResponse,
  PlayerSummary,
  RegistrationDetail,
  RegistrationSummary,
  SavePlayerRequest,
  SignupRequest,
  SubmitRegistrationRequest,
  UserSummary,
} from './types'

const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
})

export const Api = {
  // --- Auth ---
  async signup(payload: SignupRequest) {
    const r = await api.post<Me>('/auth/signup', payload)
    return r.data
  },
  async login(payload: LoginRequest) {
    const r = await api.post<Me>('/auth/login', payload)
    return r.data
  },
  async logout() {
    await api.post('/auth/logout')
  },
  async me(): Promise<Me | null> {
    try {
      const r = await api.get<Me>('/auth/me')
      return r.data
    } catch (e: any) {
      if (e?.response?.status === 401) return null
      throw e
    }
  },
  externalLoginUrl(provider: 'Google' | 'Facebook', returnUrl = '/register') {
    return `/api/auth/external/${provider}?returnUrl=${encodeURIComponent(returnUrl)}`
  },
  async listProviders(): Promise<string[]> {
    try {
      const r = await api.get<string[]>('/auth/providers')
      return r.data
    } catch {
      return []
    }
  },

  // --- Players (parent roster) ---
  async listPlayers() {
    const r = await api.get<PlayerSummary[]>('/players')
    return r.data
  },
  async createPlayer(payload: SavePlayerRequest) {
    const r = await api.post<PlayerSummary>('/players', payload)
    return r.data
  },
  async updatePlayer(id: number, payload: SavePlayerRequest) {
    const r = await api.put<PlayerSummary>(`/players/${id}`, payload)
    return r.data
  },
  async deletePlayer(id: number) {
    await api.delete(`/players/${id}`)
  },

  // --- Registration ---
  async submitRegistration(payload: SubmitRegistrationRequest) {
    const r = await api.post<RegistrationDetail>('/registrations', payload)
    return r.data
  },
  async myRegistrations() {
    const r = await api.get<RegistrationSummary[]>('/registrations/mine')
    return r.data
  },
  async listRegistrations(season?: string) {
    const r = await api.get<RegistrationSummary[]>('/registrations', {
      params: season ? { season } : undefined,
    })
    return r.data
  },
  async getRegistration(id: number) {
    const r = await api.get<RegistrationDetail>(`/registrations/${id}`)
    return r.data
  },
  async deleteRegistration(id: number) {
    await api.delete(`/registrations/${id}`)
  },

  async viewWaivers(id: number) {
    const r = await api.get(`/registrations/${id}/waivers.pdf`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data as Blob)
    window.open(url, '_blank')
  },
  async downloadWaivers(id: number) {
    const r = await api.get(`/registrations/${id}/waivers.pdf`, { responseType: 'blob' })
    triggerDownload(r.data as Blob, `waivers-${id}.pdf`)
  },
  async viewPlayerWaiver(regId: number, rpId: number) {
    const r = await api.get(`/registrations/${regId}/players/${rpId}/waiver.pdf`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data as Blob)
    window.open(url, '_blank')
  },
  async downloadPlayerWaiver(regId: number, rpId: number, filenameStem: string) {
    const r = await api.get(`/registrations/${regId}/players/${rpId}/waiver.pdf`, { responseType: 'blob' })
    triggerDownload(r.data as Blob, `waiver-${filenameStem}.pdf`)
  },

  // --- Outreach (admin) ---
  async createOutreach(payload: CreateOutreachRequest) {
    const r = await api.post<OutreachResponse>('/outreach', payload)
    return r.data
  },
  async listOutreach() {
    const r = await api.get<OutreachResponse[]>('/outreach')
    return r.data
  },
  async resendOutreach(id: number) {
    const r = await api.post<OutreachResponse>(`/outreach/${id}/resend`, {})
    return r.data
  },
  async deleteOutreach(id: number) {
    await api.delete(`/outreach/${id}`)
  },

  // --- Admin: user management ---
  async listUsers() {
    const r = await api.get<UserSummary[]>('/admin/users')
    return r.data
  },
  async banUser(id: string) {
    await api.post(`/admin/users/${id}/ban`, {})
  },
  async unbanUser(id: string) {
    await api.post(`/admin/users/${id}/unban`, {})
  },
}

function triggerDownload(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}
