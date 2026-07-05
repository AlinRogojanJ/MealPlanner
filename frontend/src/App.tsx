import { WeekView } from './features/calendar/WeekView'
import { RecipesPage } from './features/recipes/RecipesPage'
import { GroceryPage } from './features/grocery/GroceryPage'
import { LoggingPage } from './features/logging/LoggingPage'
import { LoginPage } from './features/auth/LoginPage'
import { useAuthStore } from './stores/authStore'
import { useUiStore, type Tab } from './stores/uiStore'

const TABS: { id: Tab; label: string }[] = [
  { id: 'calendar', label: 'Calendar' },
  { id: 'recipes', label: 'Recipes' },
  { id: 'grocery', label: 'Grocery list' },
  { id: 'logging', label: 'Log food' },
]

function App() {
  const activeTab = useUiStore((s) => s.activeTab)
  const setActiveTab = useUiStore((s) => s.setActiveTab)
  const session = useAuthStore((s) => s.session)
  const signOut = useAuthStore((s) => s.signOut)

  if (!session) return <LoginPage />

  return (
    <div className="min-h-screen bg-slate-100">
      <header className="border-b border-slate-200 bg-white print:hidden">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-3">
          <div className="flex items-center gap-2">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-indigo-600 text-sm font-black text-white">
              M
            </span>
            <div className="leading-tight">
              <h1 className="text-base font-bold text-slate-800">MacroSync</h1>
              <p className="text-[11px] text-slate-400">Shared meals, personal macros</p>
            </div>
          </div>
          <nav className="flex gap-1 text-sm font-medium">
            {TABS.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`rounded-lg px-3 py-1.5 transition-colors ${
                  activeTab === tab.id
                    ? 'bg-indigo-50 text-indigo-700'
                    : 'text-slate-500 hover:bg-slate-50 hover:text-slate-700'
                }`}
              >
                {tab.label}
              </button>
            ))}
          </nav>
          <div className="flex items-center gap-2">
            <span className="text-sm text-slate-500">
              Hi, <span className="font-semibold text-slate-700">{session.displayName}</span>
            </span>
            <button
              onClick={signOut}
              className="rounded-lg border border-slate-200 px-2.5 py-1 text-xs font-medium text-slate-500 hover:bg-slate-50"
            >
              Sign out
            </button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-7xl px-4 py-6">
        {activeTab === 'calendar' && <WeekView />}
        {activeTab === 'recipes' && <RecipesPage />}
        {activeTab === 'grocery' && <GroceryPage />}
        {activeTab === 'logging' && <LoggingPage />}
      </main>
    </div>
  )
}

export default App
