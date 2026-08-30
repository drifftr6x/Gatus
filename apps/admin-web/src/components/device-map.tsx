import { useEffect, useMemo, useRef, useState } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { DeviceDto } from '@/lib/api'

// Fix Leaflet default icon paths
delete (L.Icon.Default.prototype as any)._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
})

const STATUS_COLORS: Record<string, string> = {
  Online: '#10b981',
  Offline: '#64748b',
  Error: '#ef4444',
  Maintenance: '#f59e0b',
}

const STATUS_PRIORITY: Record<string, number> = {
  Error: 0, Offline: 1, Maintenance: 2, Online: 3,
}

interface StoreMarker {
  name: string
  lat: number
  lng: number
  devices: DeviceDto[]
  worstStatus: string
}

function createStoreIcon(store: StoreMarker) {
  const color = STATUS_COLORS[store.worstStatus] || STATUS_COLORS.Offline
  const count = store.devices.length
  const size = count > 1 ? 22 : 16
  return L.divIcon({
    className: '',
    html: `<div style="
      width:${size}px;height:${size}px;border-radius:50%;
      background:${color};border:2px solid rgba(255,255,255,0.9);
      box-shadow:0 0 8px ${color},0 0 20px ${color}40;
      display:flex;align-items:center;justify-content:center;
      font-size:10px;font-weight:700;color:white;
    ">${count > 1 ? count : ''}</div>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
    popupAnchor: [0, -(size / 2 + 4)],
  })
}

export function DeviceMap({ devices }: { devices: DeviceDto[] }) {
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<L.Map | null>(null)
  const layerRef = useRef<L.LayerGroup | null>(null)
  const [error, setError] = useState('')

  const mapped = devices.filter(d => d.latitude != null && d.longitude != null)

  // Group by store location (memoized so marker effect doesn't re-run every render)
  const stores = useMemo(() => {
    const map = new Map<string, StoreMarker>()
    mapped.forEach(d => {
      const key = `${d.latitude!.toFixed(3)},${d.longitude!.toFixed(3)}`
      const ex = map.get(key)
      if (ex) {
        ex.devices.push(d)
        if ((STATUS_PRIORITY[d.status] ?? 99) < (STATUS_PRIORITY[ex.worstStatus] ?? 99)) ex.worstStatus = d.status
      } else {
        map.set(key, { name: d.location || d.groupName || d.name, lat: d.latitude!, lng: d.longitude!, devices: [d], worstStatus: d.status })
      }
    })
    return [...map.values()]
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [JSON.stringify(mapped.map(d => [d.id, d.status, d.latitude, d.longitude]))])

  // Initialize map
  useEffect(() => {
    const el = containerRef.current
    if (!el) return

    try {
      // Clean any previous Leaflet instance on this element
      if ((el as any)._leaflet_id) {
        delete (el as any)._leaflet_id
        el.innerHTML = ''
        el.className = ''
      }
      if (mapRef.current) {
        mapRef.current.remove()
        mapRef.current = null
      }

      const map = L.map(el, {
        center: [39.8, -98.5],
        zoom: 4,
        zoomControl: true,
        scrollWheelZoom: true,
        attributionControl: true,
      })

      L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap &copy; CARTO',
      }).addTo(map)

      layerRef.current = L.layerGroup().addTo(map)
      mapRef.current = map
      setError('')
    } catch (e) {
      setError(`Map init failed: ${e instanceof Error ? e.message : e}`)
    }

    return () => {
      if (mapRef.current) {
        mapRef.current.remove()
        mapRef.current = null
        layerRef.current = null
      }
    }
  }, [])

  // Update markers
  useEffect(() => {
    if (!layerRef.current) return
    layerRef.current.clearLayers()

    stores.forEach(store => {
      const marker = L.marker([store.lat, store.lng], { icon: createStoreIcon(store) })

      const deviceList = store.devices.map(d =>
        `<div style="display:flex;align-items:center;gap:4px;margin-top:3px;">
          <span style="width:5px;height:5px;border-radius:50%;background:${STATUS_COLORS[d.status] || STATUS_COLORS.Offline};"></span>
          <a href="#/devices/${d.id}" style="font-size:11px;color:#cbd5e1;text-decoration:none;">${d.name}</a>
          <span style="font-size:10px;color:#64748b;">${d.status}</span>
        </div>`
      ).join('')

      const div = document.createElement('div')
      div.style.cssText = 'font-family:inherit;min-width:180px;'
      div.innerHTML = `
        <div style="font-weight:600;font-size:13px;color:#e2e8f0;">${store.name}</div>
        <div style="font-size:10px;color:#64748b;margin-top:1px;">${store.devices.length} device${store.devices.length !== 1 ? 's' : ''}</div>
        ${deviceList}`

      marker.bindPopup(div, { className: 'dark-popup', closeButton: false })
      layerRef.current!.addLayer(marker)
    })

    if (stores.length > 0 && mapRef.current) {
      const bounds = L.latLngBounds(stores.map(s => [s.lat, s.lng]))
      mapRef.current.fitBounds(bounds, { padding: [40, 40], maxZoom: 6 })
    }
  }, [stores])

  const online = mapped.filter(d => d.status === 'Online').length
  const offline = mapped.filter(d => d.status === 'Offline').length
  const err = mapped.filter(d => d.status === 'Error').length
  const maint = mapped.filter(d => d.status === 'Maintenance').length

  if (error) {
    return (
      <div className="flex h-64 flex-col items-center justify-center rounded-xl border border-red-500/30 bg-surface-900">
        <p className="text-sm text-red-400">{error}</p>
      </div>
    )
  }

  if (mapped.length === 0) {
    return (
      <div className="flex h-64 items-center justify-center rounded-xl border border-surface-800 bg-surface-900">
        <p className="text-sm text-slate-500">No devices with coordinates.</p>
      </div>
    )
  }

  return (
    <div className="rounded-xl border border-surface-800 bg-surface-900 shadow-lg overflow-hidden">
      <div ref={containerRef} style={{ height: '320px', width: '100%', background: '#0a0f1e' }} />
      <div className="flex items-center justify-between border-t border-surface-800 px-4 py-2">
        <div className="flex items-center gap-4 text-xs text-slate-500">
          {online > 0 && <span className="flex items-center gap-1"><span className="inline-block h-2 w-2 rounded-full" style={{ background: STATUS_COLORS.Online }} />{online} Online</span>}
          {offline > 0 && <span className="flex items-center gap-1"><span className="inline-block h-2 w-2 rounded-full" style={{ background: STATUS_COLORS.Offline }} />{offline} Offline</span>}
          {err > 0 && <span className="flex items-center gap-1"><span className="inline-block h-2 w-2 rounded-full" style={{ background: STATUS_COLORS.Error }} />{err} Error</span>}
          {maint > 0 && <span className="flex items-center gap-1"><span className="inline-block h-2 w-2 rounded-full" style={{ background: STATUS_COLORS.Maintenance }} />{maint} Maintenance</span>}
        </div>
        <span className="text-xs text-slate-600">{stores.length} location{stores.length !== 1 ? 's' : ''} · {mapped.length} device{mapped.length !== 1 ? 's' : ''}</span>
      </div>
    </div>
  )
}
