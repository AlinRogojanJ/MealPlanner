import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, DEMO_HOUSEHOLD_ID } from '../../api/client'
import type { LogFoodRequest } from '../../api/types'

export function useLogsForDay(userId: string, date: string) {
  return useQuery({
    queryKey: ['logs', userId, date],
    queryFn: () => api.getLogsForDay(userId, date),
  })
}

export function usePendingSuggestions(userId: string) {
  return useQuery({
    queryKey: ['suggestions', userId],
    queryFn: () => api.getPendingSuggestions(userId),
  })
}

function useInvalidatePlanning() {
  const queryClient = useQueryClient()
  return (userId: string) => {
    // Accepting/logging changes portions and totals — refresh everything planning-related.
    queryClient.invalidateQueries({ queryKey: ['suggestions', userId] })
    queryClient.invalidateQueries({ queryKey: ['logs', userId] })
    queryClient.invalidateQueries({ queryKey: ['households', DEMO_HOUSEHOLD_ID, 'plans'] })
  }
}

export function useLogFood() {
  const invalidate = useInvalidatePlanning()
  return useMutation({
    mutationFn: (request: LogFoodRequest) => api.logFood(request),
    onSuccess: (_, request) => invalidate(request.userId),
  })
}

export function useAcceptSuggestion(userId: string) {
  const invalidate = useInvalidatePlanning()
  return useMutation({
    mutationFn: (id: string) => api.acceptSuggestion(id),
    onSuccess: () => invalidate(userId),
  })
}

export function useDismissSuggestion(userId: string) {
  const invalidate = useInvalidatePlanning()
  return useMutation({
    mutationFn: (id: string) => api.dismissSuggestion(id),
    onSuccess: () => invalidate(userId),
  })
}
