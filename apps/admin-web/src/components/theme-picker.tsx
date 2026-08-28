import { useEffect, useRef, useState } from 'react'
import { Moon, Sun, Check } from 'lucide-react'
import { useTheme, ACCENTS } from '@/hooks/useTheme'

export function ThemePicker() {
  const { mode, accent, setMode, setAccent } = useTheme()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const onClickOutside = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', onClickOutside)
    return () => document.removeEventListener('mousedown', onClickOutside)
  }, [])

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setOpen((o) => !o)}
        className="flex h-8 w-8 items-center justify-center rounded-md text-(--text-4) transition-colors hover:bg-(--bg-input) hover:text-(--text-1)"
        title="Theme settings"
      >
        {mode === 'dark' ? <Moon className="h-4 w-4" /> : <Sun className="h-4 w-4" />}
      </button>

      {open && (
        <div className="absolute right-0 top-10 z-50 w-56 rounded-xl border border-(--border) bg-(--bg-card) p-4 shadow-lg">
          <p className="text-xs font-semibold uppercase tracking-wider text-(--text-5)">
            Appearance
          </p>

          {/* Light / dark */}
          <div className="mt-3 grid grid-cols-2 gap-2">
            <button
              onClick={() => setMode('light')}
              className={`flex items-center justify-center gap-1.5 rounded-lg border px-3 py-2 text-sm font-medium transition-colors ${
                mode === 'light'
                  ? 'border-(--accent) bg-(--accent-a15) text-(--text-1)'
                  : 'border-(--border) text-(--text-4) hover:bg-(--bg-elev)'
              }`}
            >
              <Sun className="h-4 w-4" />
              Light
            </button>
            <button
              onClick={() => setMode('dark')}
              className={`flex items-center justify-center gap-1.5 rounded-lg border px-3 py-2 text-sm font-medium transition-colors ${
                mode === 'dark'
                  ? 'border-(--accent) bg-(--accent-a15) text-(--text-1)'
                  : 'border-(--border) text-(--text-4) hover:bg-(--bg-elev)'
              }`}
            >
              <Moon className="h-4 w-4" />
              Dark
            </button>
          </div>

          {/* Accent color */}
          <p className="mt-4 text-xs font-semibold uppercase tracking-wider text-(--text-5)">
            Accent
          </p>
          <div className="mt-3 flex gap-2">
            {ACCENTS.map((a) => (
              <button
                key={a.name}
                onClick={() => setAccent(a.name)}
                title={a.label}
                className="flex h-7 w-7 items-center justify-center rounded-full transition-transform hover:scale-110"
                style={{ backgroundColor: a.swatch }}
              >
                {accent === a.name && <Check className="h-4 w-4 text-white" />}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
