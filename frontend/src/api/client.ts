import { getAccessToken } from '../stores/authStore'
import type {
  AuthResponse,
  FoodLogDto,
  GroceryListDto,
  HouseholdDto,
  LogFoodRequest,
  LogFoodResponse,
  MealRecommendationDto,
  MemberDto,
  PlannedMealDto,
  RecipeDto,
  ShareLinkDto,
  SuggestionDto,
  WeekPlanDto,
} from './types'
import mockWeekPlan from '../mocks/week-plan.json'
import mockHousehold from '../mocks/household.json'
import mockRecipes from '../mocks/recipes.json'

// Demo household seeded by the backend mock store.
export const DEMO_HOUSEHOLD_ID = '11111111-1111-1111-1111-111111111111'

function authHeaders(): Record<string, string> {
  const token = getAccessToken()
  return token ? { Authorization: `Bearer ${token}` } : {}
}

async function post<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return (await res.json()) as T
}

async function get<T>(path: string, fallback: T): Promise<T> {
  try {
    const res = await fetch(path, { headers: authHeaders() })
    if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
    return (await res.json()) as T
  } catch {
    // API not running — serve the snapshot from src/mocks so the UI still works.
    console.warn(`[api] falling back to mock data for ${path}`)
    return fallback
  }
}

export const api = {
  getWeekPlan: async (householdId: string, week: string): Promise<WeekPlanDto | null> => {
    try {
      const res = await fetch(`/api/v1/households/${householdId}/plans?week=${week}`, {
        headers: authHeaders(),
      })
      if (res.status === 404) return null // no plan for that week — honest empty state
      if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
      return (await res.json()) as WeekPlanDto
    } catch {
      // API not running — the snapshot only covers its own demo week.
      console.warn('[api] falling back to mock week plan')
      const snapshot = mockWeekPlan as WeekPlanDto
      return snapshot.weekStartDate === week ? snapshot : null
    }
  },

  getHousehold: (householdId: string) =>
    get<HouseholdDto>(`/api/v1/households/${householdId}`, mockHousehold as HouseholdDto),

  getRecipes: () => get<RecipeDto[]>('/api/v1/recipes', mockRecipes as RecipeDto[]),

  getGroceryList: (planId: string) =>
    get<GroceryListDto | null>(`/api/v1/plans/${planId}/grocery-list`, null),

  // ---- Auth ----

  login: (email: string, password: string) =>
    post<AuthResponse>('/api/v1/auth/login', { email, password }),

  register: (email: string, password: string, displayName: string) =>
    post<AuthResponse>('/api/v1/auth/register', { email, password, displayName }),

  // ---- Planning writes (need the API running — no mock fallback) ----

  addMeal: (planId: string, request: { date: string; slotType: string; recipeId: string }) =>
    post<PlannedMealDto>(`/api/v1/plans/${planId}/meals`, request),

  solveMeal: (mealId: string, skippedUserIds: string[]) =>
    post<PlannedMealDto>(`/api/v1/meals/${mealId}/solve`, { skippedUserIds }),

  createWeekPlan: (householdId: string, weekStartDate: string, copyFromWeekStartDate?: string) =>
    post<WeekPlanDto>(`/api/v1/households/${householdId}/plans`, { weekStartDate, copyFromWeekStartDate }),

  moveMeal: (mealId: string, date: string, slotType: string) =>
    post<PlannedMealDto>(`/api/v1/meals/${mealId}/move`, { date, slotType }),

  deleteMeal: async (mealId: string): Promise<void> => {
    const res = await fetch(`/api/v1/meals/${mealId}`, { method: 'DELETE', headers: authHeaders() })
    if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  },

  updateProfile: (
    userId: string,
    request: { calorieTarget: number; proteinG: number; carbsG: number; fatG: number; dietType: string },
  ) => {
    // userId query param is the dev fallback until [Authorize] is enforced.
    return fetch(`/api/v1/users/me/profile?userId=${userId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify(request),
    }).then(async (res) => {
      if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
      return (await res.json()) as MemberDto
    })
  },

  createShareLink: (planId: string) =>
    post<ShareLinkDto>(`/api/v1/plans/${planId}/grocery-list/share`, {}),

  getRecommendations: (planId: string, date: string, slot: string) =>
    get<MealRecommendationDto[]>(
      `/api/v1/plans/${planId}/recommendations?date=${date}&slot=${slot}`,
      [],
    ),

  // ---- Off-plan logging & recalc (writes need the API running — no mock fallback) ----

  logFood: (request: LogFoodRequest) => post<LogFoodResponse>('/api/v1/logs', request),

  getLogsForDay: (userId: string, date: string) =>
    get<FoodLogDto[]>(`/api/v1/logs?userId=${userId}&date=${date}`, []),

  getPendingSuggestions: (userId: string) =>
    get<SuggestionDto[]>(`/api/v1/suggestions?userId=${userId}`, []),

  acceptSuggestion: (id: string) => post<SuggestionDto>(`/api/v1/suggestions/${id}/accept`, {}),

  dismissSuggestion: (id: string) => post<SuggestionDto>(`/api/v1/suggestions/${id}/dismiss`, {}),
}
