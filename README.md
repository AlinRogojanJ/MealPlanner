# MacroSync

Shared meal planning for people with different goals — one dish, everyone's own portion.

A couple with a 1,000 kcal gap can plan a week, cook one dish, eat correct portions, log a dessert, and shop — all in one place. See `MacroSync_Product_Plan_v1.1.docx` and `MacroSync_Technical_Design_v1.1.docx` for the full spec.

## Stack

- **Frontend** — React 19 + TypeScript + Vite, Tailwind CSS, TanStack Query (server cache) + Zustand (UI state)
- **Backend** — .NET 10 ASP.NET Core Web API, Clean-Architecture-lite (Api → Application → Domain; Infrastructure implements Application interfaces)
- **Database** — Azure SQL + EF Core 10, code-first migrations checked into Git
- **Auth** — app-issued JWT (15 min) + rotating refresh tokens; Google Sign-In primary, email/password fallback

## Running locally

Two terminals:

```bash
# 1. API on http://localhost:5119
cd backend/src/MacroSync.Api
dotnet run --launch-profile http

# 2. SPA on http://localhost:5173 (proxies /api to :5119)
cd frontend
npm install
npm run dev
```

Open http://localhost:5173 — the weekly calendar (main page) shows the demo household: Alin (2,500 kcal cut) and Maria (1,700 kcal cut), a fully planned week with per-person portions computed by the real `PortionSolver`, and running daily totals per person.

If the API isn't running, the frontend falls back to snapshots in `frontend/src/mocks/`.

```bash
# Tests
cd backend && dotnet test
```

## AI recalc & recommendations (Phase 2)

Set an Anthropic API key in `appsettings.json` → `Ai:AnthropicApiKey` (or the `ANTHROPIC_API_KEY` env var) to enable:

- **AI auto-recalc** — Claude proposes a preference-aware mix of meal adjustments after an off-plan log (protects protein, shrinks snacks first); the rules engine clamps and re-prices every proposal, so the arithmetic stays deterministic.
- **AI meal recommendations** — "✨ Suggest for us" in the add-dish modal ranks dishes that fit everyone's remaining targets, with reasons.

Without a key, both fall back to the rules-based engine (marked accordingly in the UI). Endpoint: `GET /api/v1/plans/{planId}/recommendations?date=&slot=`.

## Data source: Mock vs Sql

`backend/src/MacroSync.Api/appsettings.json` → `"DataSource"`:

- **`Mock`** (default) — in-memory demo data, no database needed. Writes work but reset on restart.
- **`Sql`** — EF Core against `ConnectionStrings:MacroSync` (placeholder included; point it at Azure SQL / LocalDB / Docker SQL). In Development the API applies migrations and seeds the demo household automatically on startup.

Migrations live in `backend/src/MacroSync.Infrastructure/Migrations` (`InitialCreate` checked in). Add more with:

```bash
cd backend
dotnet ef migrations add <Name> --project src/MacroSync.Infrastructure --startup-project src/MacroSync.Api
```

## Structure

```
backend/
├── MacroSync.sln
├── src/
│   ├── MacroSync.Api             # controllers, JWT auth, validation, OpenAPI
│   ├── MacroSync.Application     # DTOs, service interfaces, FluentValidation
│   ├── MacroSync.Domain          # entities, enums, PortionSolver, RecalcEngine (pure C#)
│   └── MacroSync.Infrastructure  # EF Core DbContext + migrations, SQL services,
│                                 # mock services (Mocks/MockDb.cs), JWT/Google auth
└── tests/
    ├── MacroSync.UnitTests       # PortionSolver tests
    └── MacroSync.IntegrationTests# stub — API + SQL via Testcontainers later

frontend/
└── src/
    ├── api/          # fetch client + DTO types (OpenAPI codegen later)
    ├── features/
    │   ├── calendar/ # week view — the main page
    │   ├── recipes/  # curated library
    │   ├── grocery/  # aggregated list + copy/print export
    │   └── auth|household|logging/  # later milestones
    ├── components/   # shared UI (MacroBar, …)
    ├── stores/       # Zustand slices (UI state only)
    ├── mocks/        # mock data snapshots of the API responses
    └── lib/          # date/macros/color utils
```

## API (v1)

| Verb | Route | Purpose |
|---|---|---|
| POST | `/api/v1/auth/google` | Google ID-token exchange → app JWT + refresh |
| POST | `/api/v1/auth/register` · `/login` · `/refresh` | Email/password fallback + token rotation |
| GET | `/api/v1/households/{id}` | Household with members + targets |
| POST | `/api/v1/households/{id}/members` | Join via invite code |
| PUT | `/api/v1/users/me/profile` | New active nutrition profile (versioned) |
| GET | `/api/v1/households/{id}/plans?week=YYYY-MM-DD` | Weekly calendar with portions + totals |
| POST | `/api/v1/plans/{planId}/meals` | Add dish to a slot → portion solve |
| POST | `/api/v1/meals/{id}/solve` | Re-run split (targets changed, eater skipped) |
| POST | `/api/v1/logs` | Log off-plan food → RecalcSuggestion |
| GET | `/api/v1/suggestions?userId=` | Pending recalc suggestions |
| POST | `/api/v1/suggestions/{id}/accept` · `/dismiss` | One-tap apply / dismiss |
| GET | `/api/v1/plans/{planId}/grocery-list` | Aggregated list, grouped by aisle |
| POST | `/api/v1/plans/{planId}/grocery-list/share` | Create anonymous share link |
| GET | `/api/v1/grocery-lists/{shareToken}` | Read-only shared grocery list (no auth) |
| GET | `/api/v1/recipes` | Curated recipe library |

Demo IDs: household `11111111-…`, plan `22222222-…`, users Alin `aaaaaaaa-…` / Maria `bbbbbbbb-…`.

In Mock mode, auth issues real signed JWTs for the demo user regardless of credentials (dev only); Google sign-in requires Sql mode + a `Google:ClientId`.

## Next steps (per roadmap Phase 1)

- Frontend: Log food tab + suggestion accept/dismiss UI
- Enforce `[Authorize]` + household-membership policies once the SPA has a login flow
- Frontend type generation from OpenAPI in CI
- Drag meals between days, copy day/week, week templates
