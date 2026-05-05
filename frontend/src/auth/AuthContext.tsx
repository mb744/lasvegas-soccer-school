import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { Api } from '../api/client'
import type { LoginRequest, Me, SignupRequest } from '../api/types'

interface AuthContextValue {
  me: Me | null
  loading: boolean
  providers: string[]
  refresh: () => Promise<void>
  login: (req: LoginRequest) => Promise<Me>
  signup: (req: SignupRequest) => Promise<Me>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null)
  const [providers, setProviders] = useState<string[]>([])
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      const [m, p] = await Promise.all([Api.me(), Api.listProviders()])
      setMe(m)
      setProviders(p)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    refresh()
  }, [refresh])

  const login = useCallback(async (req: LoginRequest) => {
    const m = await Api.login(req)
    setMe(m)
    return m
  }, [])

  const signup = useCallback(async (req: SignupRequest) => {
    const m = await Api.signup(req)
    setMe(m)
    return m
  }, [])

  const logout = useCallback(async () => {
    await Api.logout()
    setMe(null)
  }, [])

  return (
    <AuthContext.Provider value={{ me, loading, providers, refresh, login, signup, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
