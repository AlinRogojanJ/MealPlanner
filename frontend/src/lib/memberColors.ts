// Stable per-member accent colors, assigned by member order in the household.
export interface MemberColor {
  dot: string
  text: string
  bar: string
  chipBg: string
}

const PALETTE: MemberColor[] = [
  { dot: 'bg-indigo-500', text: 'text-indigo-700', bar: 'bg-indigo-500', chipBg: 'bg-indigo-50' },
  { dot: 'bg-rose-500', text: 'text-rose-700', bar: 'bg-rose-500', chipBg: 'bg-rose-50' },
  { dot: 'bg-emerald-500', text: 'text-emerald-700', bar: 'bg-emerald-500', chipBg: 'bg-emerald-50' },
  { dot: 'bg-amber-500', text: 'text-amber-700', bar: 'bg-amber-500', chipBg: 'bg-amber-50' },
  { dot: 'bg-sky-500', text: 'text-sky-700', bar: 'bg-sky-500', chipBg: 'bg-sky-50' },
]

export function memberColor(index: number): MemberColor {
  return PALETTE[index % PALETTE.length]
}
