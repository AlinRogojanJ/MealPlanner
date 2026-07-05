import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { api } from '../../api/client'
import { useAuthStore } from '../../stores/authStore'

/**
 * Sign-in screen (§5.1): Google front-and-center, email/password fallback.
 * The Google button is a placeholder until a Google:ClientId is configured —
 * in Mock mode any email/password signs in as the demo user.
 */
export function LoginPage() {
  const signIn = useAuthStore((s) => s.signIn)
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('alin@example.com')
  const [password, setPassword] = useState('demo1234')
  const [displayName, setDisplayName] = useState('')

  const submit = useMutation({
    mutationFn: () =>
      mode === 'login'
        ? api.login(email, password)
        : api.register(email, password, displayName || email.split('@')[0]),
    onSuccess: signIn,
  })

  const inputCls =
    'w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 focus:border-indigo-400 focus:outline-none'

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-100 p-4">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex items-center justify-center gap-2">
          <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-600 text-lg font-black text-white">
            M
          </span>
          <div className="leading-tight">
            <h1 className="text-xl font-bold text-slate-800">MacroSync</h1>
            <p className="text-xs text-slate-400">Shared meals, personal macros</p>
          </div>
        </div>

        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          {/* Google Sign-In — primary method, front and center (§5.1) */}
          <button
            disabled
            title="Configure Google:ClientId in the API to enable"
            className="mb-3 flex w-full cursor-not-allowed items-center justify-center gap-2 rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm font-medium text-slate-400"
          >
            <svg width="16" height="16" viewBox="0 0 48 48" aria-hidden>
              <path fill="#c6c6c6" d="M44.5 20H24v8.5h11.8C34.7 33.9 30.1 37 24 37c-7.2 0-13-5.8-13-13s5.8-13 13-13c3.1 0 5.9 1.1 8.1 2.9l6.4-6.4C34.6 4.1 29.6 2 24 2 11.8 2 2 11.8 2 24s9.8 22 22 22c11 0 21-8 21-22 0-1.3-.2-2.7-.5-4z" />
            </svg>
            Sign in with Google
            <span className="text-[10px]">(soon)</span>
          </button>

          <div className="mb-3 flex items-center gap-3 text-[11px] text-slate-400">
            <span className="h-px flex-1 bg-slate-200" /> or with email <span className="h-px flex-1 bg-slate-200" />
          </div>

          <div className="space-y-3">
            {mode === 'register' && (
              <input
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                placeholder="Display name"
                className={inputCls}
              />
            )}
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="Email"
              className={inputCls}
            />
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && submit.mutate()}
              placeholder="Password (min 8 chars)"
              className={inputCls}
            />
            <button
              disabled={submit.isPending || !email || password.length < 8}
              onClick={() => submit.mutate()}
              className="w-full rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {submit.isPending ? 'Signing in…' : mode === 'login' ? 'Sign in' : 'Create account'}
            </button>
            {submit.isError && (
              <p className="text-xs text-red-600">
                {String(submit.error).includes('401')
                  ? 'Wrong email or password.'
                  : `Sign-in failed — is the API running? (${String(submit.error)})`}
              </p>
            )}
          </div>

          <p className="mt-4 text-center text-xs text-slate-500">
            {mode === 'login' ? (
              <>
                No account?{' '}
                <button onClick={() => setMode('register')} className="font-semibold text-indigo-600 hover:underline">
                  Register
                </button>
              </>
            ) : (
              <>
                Have an account?{' '}
                <button onClick={() => setMode('login')} className="font-semibold text-indigo-600 hover:underline">
                  Sign in
                </button>
              </>
            )}
          </p>
        </div>

        <p className="mt-4 text-center text-[11px] text-slate-400">
          Demo mode: any credentials sign in as Alin (household owner).
        </p>
      </div>
    </div>
  )
}
