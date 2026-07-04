import type { GroceryListDto, HouseholdDto, RecipeDto, WeekPlanDto } from './types'
import mockWeekPlan from '../mocks/week-plan.json'
import mockHousehold from '../mocks/household.json'
import mockRecipes from '../mocks/recipes.json'

// Demo household seeded by the backend mock store.
export const DEMO_HOUSEHOLD_ID = '11111111-1111-1111-1111-111111111111'

async function get<T>(path: string, fallback: T): Promise<T> {
  try {
    const res = await fetch(path)
    if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
    return (await res.json()) as T
  } catch {
    // API not running — serve the snapshot from src/mocks so the UI still works.
    console.warn(`[api] falling back to mock data for ${path}`)
    return fallback
  }
}

export const api = {
  getWeekPlan: (householdId: string, week: string) =>
    get<WeekPlanDto>(
      `/api/v1/households/${householdId}/plans?week=${week}`,
      mockWeekPlan as WeekPlanDto,
    ),

  getHousehold: (householdId: string) =>
    get<HouseholdDto>(`/api/v1/households/${householdId}`, mockHousehold as HouseholdDto),

  getRecipes: () => get<RecipeDto[]>('/api/v1/recipes', mockRecipes as RecipeDto[]),

  getGroceryList: (planId: string) =>
    get<GroceryListDto | null>(`/api/v1/plans/${planId}/grocery-list`, null),
}
