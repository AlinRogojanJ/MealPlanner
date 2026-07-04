import { useQuery } from '@tanstack/react-query'
import { api } from '../../api/client'
import { useWeekPlan } from '../calendar/useWeekPlan'

export function useGroceryList() {
  const { data: plan } = useWeekPlan()
  return useQuery({
    queryKey: ['plans', plan?.planId, 'grocery-list'],
    queryFn: () => api.getGroceryList(plan!.planId),
    enabled: !!plan?.planId,
  })
}
