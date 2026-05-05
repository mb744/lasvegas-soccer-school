export type Language = 0 | 1 // 0 = English, 1 = Spanish
export type InvitationStatus = 0 | 1 | 2 | 3 | 4 // Pending Sent Opened Registered Failed

export interface CreateInvitationRequest {
  email?: string
  phone?: string
  language: Language
}

export interface InvitationResponse {
  id: number
  token: string
  email: string | null
  phone: string | null
  language: Language
  status: InvitationStatus
  statusMessage: string | null
  link: string
  createdAt: string
  sentAt: string | null
  registeredAt: string | null
}

export interface InvitationLookupResponse {
  token: string
  language: Language
  status: InvitationStatus
  email: string | null
  phone: string | null
  alreadyRegistered: boolean
}

export interface PlayerSubmission {
  firstName: string
  lastName: string
  dateOfBirth: string
  schoolGrade: string
  shirtSize: string
  shortSize: string
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
  token: string
  parentFirstName: string
  parentLastName: string
  addressLine1: string
  addressLine2?: string
  city: string
  state: string
  postalCode: string
  cellPhone: string
  email: string
  language: Language
  waiverConsent: boolean
  players: PlayerSubmission[]
}

export interface RegistrationSummary {
  id: number
  parentFirstName: string
  parentLastName: string
  email: string
  cellPhone: string
  language: Language
  playerCount: number
  createdAt: string
}

export interface PlayerDetail {
  id: number
  firstName: string
  lastName: string
  dateOfBirth: string
  schoolGrade: string
  shirtSize: string
  shortSize: string
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
  players: PlayerDetail[]
}

export const STATUS_LABELS: Record<InvitationStatus, string> = {
  0: 'Pending',
  1: 'Sent',
  2: 'Opened',
  3: 'Registered',
  4: 'Failed',
}
