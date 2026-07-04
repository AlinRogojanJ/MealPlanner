export function formatDayHeader(isoDate: string): { weekday: string; date: string } {
  const d = new Date(isoDate + 'T00:00:00')
  return {
    weekday: d.toLocaleDateString('en-GB', { weekday: 'short' }),
    date: d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' }),
  }
}

export function isToday(isoDate: string): boolean {
  return isoDate === new Date().toISOString().slice(0, 10)
}

export function formatWeekRange(weekStartIso: string): string {
  const start = new Date(weekStartIso + 'T00:00:00')
  const end = new Date(start)
  end.setDate(end.getDate() + 6)
  const opts: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short' }
  return `${start.toLocaleDateString('en-GB', opts)} – ${end.toLocaleDateString('en-GB', opts)}`
}
