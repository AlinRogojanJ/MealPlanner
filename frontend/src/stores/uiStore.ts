import { create } from 'zustand'

// UI state only — server data lives in TanStack Query (Tech Design §7.2).

interface UiState {
  selectedWeek: string // ISO date of the week's Monday
  setSelectedWeek: (week: string) => void
}

export const useUiStore = create<UiState>((set) => ({
  selectedWeek: '2026-06-29', // demo week seeded in the backend mock store
  setSelectedWeek: (selectedWeek) => set({ selectedWeek }),
}))
