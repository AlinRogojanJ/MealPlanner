import { WeekView } from './features/calendar/WeekView'

function App() {
  return (
    <div className="min-h-screen bg-slate-100">
      <header className="border-b border-slate-200 bg-white">
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
            <span className="rounded-lg bg-indigo-50 px-3 py-1.5 text-indigo-700">Calendar</span>
            <span className="cursor-not-allowed rounded-lg px-3 py-1.5 text-slate-400" title="Coming soon">Recipes</span>
            <span className="cursor-not-allowed rounded-lg px-3 py-1.5 text-slate-400" title="Coming soon">Grocery list</span>
            <span className="cursor-not-allowed rounded-lg px-3 py-1.5 text-slate-400" title="Coming soon">Log food</span>
          </nav>
        </div>
      </header>
      <main className="mx-auto max-w-7xl px-4 py-6">
        <WeekView />
      </main>
    </div>
  )
}

export default App
