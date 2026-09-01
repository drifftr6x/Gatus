import { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { api, authApi } from '@/lib/api'
import type { UserDto } from '@/lib/api'

interface AuthContextType {
  user: UserDto | null
  isLoading: boolean
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    const initAuth = async () => {
      // Check session marker; refresh token cookie is sent automatically
      if (api.hasSession) {
        try {
          // First attempt will 401 (no access token in memory) then auto-refresh via cookie
          const userData = await authApi.getCurrentUser()
          setUser(userData)
        } catch {
          api.clearTokens()
        }
      }
      setIsLoading(false)
    }
    initAuth()
  }, [])

  const login = async (email: string, password: string) => {
    const response = await authApi.login({ email, password })
    api.setTokens(response.accessToken, response.refreshToken)
    setUser(response.user)
  }

  const logout = async () => {
    try {
      await authApi.logout()
    } catch {
      // Ignore errors on logout
    }
    api.clearTokens()
    setUser(null)
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        isAuthenticated: !!user,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
