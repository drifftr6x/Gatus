import { useCallback, useEffect, useState } from 'react'

export type ThemeMode = 'dark' | 'light'
export type AccentName = 'indigo' | 'sky' | 'emerald' | 'amber' | 'rose'

export const ACCENTS: { name: AccentName; label: string; swatch: string }[] = [
  { name: 'indigo', label: 'Indigo', swatch: '#6366f1' },
  { name: 'sky', label: 'Sky', swatch: '#0ea5e9' },
  { name: 'emerald', label: 'Emerald', swatch: '#10b981' },
  { name: 'amber', label: 'Amber', swatch: '#f59e0b' },
  { name: 'rose', label: 'Rose', swatch: '#f43f5e' },
]

function readMode(): ThemeMode {
  const stored = localStorage.getItem('theme-mode')
  return stored === 'light' ? 'light' : 'dark'
}

function readAccent(): AccentName {
  const stored = localStorage.getItem('theme-accent')
  return ACCENTS.some((a) => a.name === stored) ? (stored as AccentName) : 'indigo'
}

export function useTheme() {
  const [mode, setModeState] = useState<ThemeMode>(readMode)
  const [accent, setAccentState] = useState<AccentName>(readAccent)

  useEffect(() => {
    document.documentElement.dataset.mode = mode
    document.documentElement.dataset.accent = accent
  }, [mode, accent])

  const setMode = useCallback((m: ThemeMode) => {
    localStorage.setItem('theme-mode', m)
    setModeState(m)
  }, [])

  const setAccent = useCallback((a: AccentName) => {
    localStorage.setItem('theme-accent', a)
    setAccentState(a)
  }, [])

  return { mode, accent, setMode, setAccent }
}
