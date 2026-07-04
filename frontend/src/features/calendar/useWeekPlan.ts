import { useQuery } from '@tanstack/react-query'
import { api, DEMO_HOUSEHOLD_ID } from '../../api/client'
import { useUiStore } from '../../stores/uiStore'

export function useWeekPlan() {
  const selectedWeek = useUiStore((s) => s.selectedWeek)
  return useQuery({
    // Per-household cache keys (Tech Design §7.2)
    queryKey: ['households', DEMO_HOUSEHOLD_ID, 'plans', selectedWeek],
    queryFn: () => api.getWeekPlan(DEMO_HOUSEHOLD_ID, selectedWeek),
    refetchInterval: 30_000, // lightweight polling covers the two-editors case in v1
  })
}
