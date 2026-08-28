import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'
import type { Plugin } from 'vite'

/**
 * Rewrites color utilities in our source to reference theme CSS variables,
 * so light/dark mode is a single <html> attribute swap without touching pages.
 */
function themeColorVars(): Plugin {
  const pairs: [RegExp, string][] = [
    [/bg-surface-950/g, 'bg-(--bg-page)'],
    [/bg-surface-900/g, 'bg-(--bg-card)'],
    [/bg-surface-850/g, 'bg-(--bg-elev)'],
    [/bg-surface-800(?![\d/])/g, 'bg-(--bg-input)'],
    [/border-surface-800/g, 'border-(--border)'],
    [/border-surface-700/g, 'border-(--border-strong)'],
    [/bg-surface-700(?![\d/])/g, 'bg-(--border-strong)'],
    [/text-slate-100/g, 'text-(--text-1)'],
    [/text-slate-200/g, 'text-(--text-2)'],
    [/text-slate-300/g, 'text-(--text-3)'],
    [/text-slate-400/g, 'text-(--text-4)'],
    [/text-slate-500/g, 'text-(--text-5)'],
    [/text-slate-600/g, 'text-(--text-6)'],
    [/text-white/g, 'text-(--text-1)'],
    [/accent-500\/(\d+)/g, '(--accent-a$1)'],
    [/accent-500/g, '(--accent)'],
    [/accent-400/g, '(--accent-strong)'],
    [/accent-300/g, '(--accent-text)'],
    [/placeholder-slate-500/g, 'placeholder:text-(--text-5)'],
  ]
  return {
    name: 'theme-color-vars',
    enforce: 'pre',
    transform(code, id) {
      if (!id.includes('apps\\admin-web\\src\\') && !id.includes('apps/admin-web/src/')) return null
      if (!/\.(tsx?|jsx?|css)$/.test(id)) return null
      let out = code
      for (const [pattern, replacement] of pairs) {
        out = out.replace(pattern, replacement)
      }
      return out === code ? null : out
    },
  }
}

export default defineConfig({
  plugins: [themeColorVars(), react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src')
    }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        secure: false
      },
      '/hubs': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        secure: false,
        ws: true
      }
    }
  }
})
