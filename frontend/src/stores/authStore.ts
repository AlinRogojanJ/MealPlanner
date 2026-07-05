import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { AuthResponse } from '../api/types'

// Session persisted to localStorage so a refresh keeps you signed in.
// The access token is read by the fetch wrapper via getAccessToken().

interface AuthState {
  session: AuthResponse | null
  signIn: (session: AuthResponse) => void
  signOut: () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      session: null,
      signIn: (session) => set({ session }),
      signOut: () => set({ session: null }),
    }),
    { name: 'macrosync-auth' },
  ),
)

export function getAccessToken(): string | null {
  return useAuthStore.getState().session?.accessToken ?? null
}
