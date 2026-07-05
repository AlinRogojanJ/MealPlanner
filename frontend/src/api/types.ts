// Mirrors the backend Application DTOs 1:1.
// Once CI is wired, these are generated from the API's OpenAPI spec instead.

export interface MemberDto {
  userId: string
  displayName: string
  role: 'Owner' | 'Member'
  dietType: 'Cut' | 'Maintain' | 'Bulk'
  calorieTarget: number
  proteinG: number
  carbsG: number
  fatG: number
}

export interface HouseholdDto {
  id: string
  name: string
  inviteCode: string
  members: MemberDto[]
}

export interface PortionDto {
  userId: string
  summary: string
  kcal: number
  proteinG: number
  carbsG: number
  fatG: number
  kcalDelta: number
}

export interface PlannedMealDto {
  id: string
  date: string
  slotType: 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack'
  recipeId: string
  recipeName: string
  portions: PortionDto[]
}

export interface DailyTotalDto {
  userId: string
  consumedKcal: number
  consumedProteinG: number
  consumedCarbsG: number
  consumedFatG: number
  targetKcal: number
  deltaKcal: number
}

export interface DayPlanDto {
  date: string
  meals: PlannedMealDto[]
  totals: DailyTotalDto[]
}

export interface WeekPlanDto {
  planId: string
  householdId: string
  weekStartDate: string
  members: MemberDto[]
  days: DayPlanDto[]
}

export interface RecipeIngredientDto {
  ingredientId: string
  name: string
  quantityG: number
  isDivisible: boolean
  kcalPer100G: number
}

export interface RecipeDto {
  id: string
  name: string
  servings: number
  instructions: string
  isCurated: boolean
  ingredients: RecipeIngredientDto[]
}

export interface GroceryItemDto {
  name: string
  aisle: string
  totalQuantityG: number
}

export interface GroceryListDto {
  planId: string
  weekStartDate: string
  items: GroceryItemDto[]
}

export interface ShareLinkDto {
  shareToken: string
  url: string
}

// ---- Off-plan logging & recalc ----

export interface LogFoodRequest {
  userId: string
  date: string
  description: string
  kcal: number
  proteinG: number
  carbsG: number
  fatG: number
}

export interface FoodLogDto {
  id: string
  userId: string
  date: string
  source: 'Planned' | 'OffPlan'
  description: string
  kcal: number
  proteinG: number
  carbsG: number
  fatG: number
}

export interface MealAdjustmentDto {
  plannedMealId: string
  recipeName: string
  slotType: string
  oldKcal: number
  newKcal: number
  scale: number
}

export interface SuggestionDto {
  id: string
  userId: string
  date: string
  status: 'Pending' | 'Accepted' | 'Dismissed'
  overageKcal: number
  absorbedKcal: number
  unabsorbedKcal: number
  adjustments: MealAdjustmentDto[]
}

export interface LogFoodResponse {
  log: FoodLogDto
  suggestion: SuggestionDto | null
}
