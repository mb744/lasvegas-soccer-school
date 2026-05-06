export type Language = 0 | 1 // 0 = English, 1 = Spanish
export type OutreachStatus = 0 | 1 | 2 | 3 | 4 // Pending Sent AccountCreated Registered Failed

export interface Me {
  userId: string
  email: string
  firstName: string
  lastName: string
  phone: string | null
  language: Language
  isAdmin: boolean
}

export interface SignupRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  phone?: string
  language: Language
}

export interface LoginRequest {
  email: string
  password: string
  rememberMe?: boolean
}

// --- Players (parent's durable roster) ---

export interface PlayerSummary {
  id: number
  firstName: string
  lastName: string
  dateOfBirth: string
}

export interface SavePlayerRequest {
  firstName: string
  lastName: string
  dateOfBirth: string
}

// --- Registration ---

export interface RegistrationPlayerInput {
  playerId?: number | null
  firstName?: string
  lastName?: string
  dateOfBirth?: string
  schoolGrade: string
  uniformSize: string
  shoeSize: string
  heardFrom?: string
  waiverParticipantName?: string
  waiverTeamName?: string
  waiverParentGuardianName?: string
  waiverPhone?: string
  waiverEmail?: string
  signatureDataUrl: string
}

export interface SubmitRegistrationRequest {
  parentFirstName: string
  parentLastName: string
  addressLine1?: string
  addressLine2?: string
  city?: string
  state?: string
  postalCode?: string
  cellPhone: string
  email: string
  language: Language
  waiverConsent: boolean
  players: RegistrationPlayerInput[]
}

export interface RegistrationSummary {
  id: number
  season: string
  parentFirstName: string
  parentLastName: string
  email: string
  cellPhone: string
  language: Language
  playerCount: number
  createdAt: string
}

export interface RegistrationPlayerDetail {
  id: number          // RegistrationPlayer.Id (used in the waiver PDF route)
  playerId: number    // underlying durable Player.Id
  firstName: string
  lastName: string
  dateOfBirth: string
  schoolGrade: string
  uniformSize: string
  shoeSize: string
  heardFrom: string | null
  waiverParticipantName: string | null
  waiverTeamName: string | null
  waiverParentGuardianName: string | null
  waiverPhone: string | null
  waiverEmail: string | null
  hasSignature: boolean
  signedAt: string | null
}

export interface RegistrationDetail {
  id: number
  season: string
  parentFirstName: string
  parentLastName: string
  addressLine1: string
  addressLine2: string | null
  city: string
  state: string
  postalCode: string
  cellPhone: string
  email: string
  language: Language
  waiverConsent: boolean
  waiverSignedAt: string | null
  createdAt: string
  players: RegistrationPlayerDetail[]
}

// --- Outreach (admin tracking) ---

export interface CreateOutreachRequest {
  email?: string
  phone?: string
  language: Language
}

export interface OutreachResponse {
  id: number
  email: string | null
  phone: string | null
  language: Language
  status: OutreachStatus
  statusMessage: string | null
  link: string
  createdAt: string
  sentAt: string | null
  accountCreatedAt: string | null
  registeredAt: string | null
  parentAccountId: number | null
}

// --- Admin: user management ---

export interface UserSummary {
  id: string
  email: string
  firstName: string
  lastName: string
  phone: string | null
  isAdmin: boolean
  isBanned: boolean
  createdAt: string | null
  lastLoginAt: string | null
  registrationCount: number
}

export const OUTREACH_STATUS_LABELS: Record<OutreachStatus, string> = {
  0: 'Pending',
  1: 'Sent',
  2: 'Account created',
  3: 'Registered',
  4: 'Failed',
}
