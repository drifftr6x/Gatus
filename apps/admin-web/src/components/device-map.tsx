import { useEffect, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { DeviceDto, DeviceGroupDto } from '@/lib/api'

// Fix Leaflet default icon paths (broken by bundlers)
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
  Error: 0,
  Offline: 1,
  Maintenance: 2,
  Online: 3,
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
  const size = count > 1 ? 20 : 14
  return L.divIcon({
    className: '',
    html: `<div style="
      width: ${size}px; height: ${size}px; border-radius: 50%;
      background: ${color}; border: 2px solid rgba(255,255,255,0.9);
      box-shadow: 0 0 8px ${color}, 0 0 20px ${color}40;
      display: flex; align-items: center; justify-content: center;
      font-size: 9px; font-weight: 700; color: white;
    ">${count > 1 ? count : ''}</div>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
    popupAnchor: [0, -(size / 2 + 4)],
  })
}

export function DeviceMap({ devices, groups }: { devices: DeviceDto[]; groups?: DeviceGroupDto[] }) {
  const mapRef = useRef<HTMLDivElement>(null)
  const mapInstanceRef = useRef<L.Map | null>(null)
  const markersRef = useRef<L.LayerGroup | null>(null)

  // Build store markers from devices that have coordinates
  const mappedDevices = devices.filter(d => d.latitude != null && d.longitude != null)

  // Group devices by store location (same lat/lng rounded to 3 decimal places ≈ ~100m)
  const storeMap = new Map<string, StoreMarker>()
  mappedDevices.forEach(d => {
    const key = `${d.latitude!.toFixed(3)},${d.longitude!.toFixed(3)}`
    const existing = storeMap.get(key)
    if (existing) {
      existing.devices.push(d)
      const newPriority = STATUS_PRIORITY[d.status] ?? 99
      const curPriority = STATUS_PRIORITY[existing.worstStatus] ?? 99
      if (newPriority < curPriority) existing.worstStatus = d.status
    } else {
      storeMap.set(key, {
        name: d.location || d.groupName || d.name,
        lat: d.latitude!,
        lng: d.longitude!,
        devices: [d],
        worstStatus: d.status,
      })
    }
  })
  const stores = [...storeMap.values()]

  // Store locations without devices (from groups)
  const storeGroups = groups ?? []

  useEffect(() => {
    if (!mapRef.current) return

    // Clean up any existing map on this container (StrictMode double-mount)
    if (mapInstanceRef.current) {
      mapInstanceRef.current.remove()
      mapInstanceRef.current = null
    }

    const map = L.map(mapRef.current, {
      center: [39.8, -98.5],
      zoom: 4,
      zoomControl: true,
      scrollWheelZoom: false,
      attributionControl: false,
    })

    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
    }).addTo(map)

    markersRef.current = L.layerGroup().addTo(map)
    mapInstanceRef.current = map

    return () => {
      map.remove()
      mapInstanceRef.current = null
      markersRef.current = null
    }
  }, [])

  useEffect(() => {
    if (!markersRef.current) return
    markersRef.current.clearLayers()

    // Draw store markers (with devices)
    stores.forEach((store) => {
      const marker = L.marker([store.lat, store.lng], {
        icon: createStoreIcon(store),
      })

      const deviceList = store.devices.map(d =>
        `<div style="display: flex; align-items: center; gap: 4px; margin-top: 3px;">
          <span style="width: 5px; height: 5px; border-radius: 50%; background: ${STATUS_COLORS[d.status] || STATUS_COLORS.Offline};"></span>
          <span style="font-size: 11px; color: #cbd5e1;">${d.name}</span>
          <span style="font-size: 10px; color: #64748b;">${d.status}</span>
        </div>`
      ).join('')

      const popupContent = document.createElement('div')
      popupContent.style.cssText = 'font-family: inherit; min-width: 180px;'
      popupContent.innerHTML = `
        <div style="font-weight: 600; font-size: 13px; color: #e2e8f0;">${store.name}</div>
        <div style="font-size: 10px; color: #64748b; margin-top: 1px;">${store.devices.length} device${store.devices.length !== 1 ? 's' : ''}</div>
        ${deviceList}
      `

      marker.bindPopup(popupContent, { className: 'dark-popup', closeButton: false })
      markersRef.current!.addLayer(marker)
    })

    // Draw empty store markers (groups without devices)
    storeGroups.forEach((group) => {
      if (group.deviceCount > 0) return
      // We don't have coordinates for groups — skip for now
      // Groups without devices won't show on map until they get devices
    })

    // Fit bounds
    if (stores.length > 0 && mapInstanceRef.current) {
      const bounds = L.latLngBounds(stores.map(s => [s.lat, s.lng]))
      mapInstanceRef.current.fitBounds(bounds, { padding: [40, 40], maxZoom: 6 })
    }
  }, [stores, storeGroups])

  const totalOnline = mappedDevices.filter(d => d.status === 'Online').length
  const totalOffline = mappedDevices.filter(d => d.status === 'Offline').length
  const totalError = mappedDevices.filter(d => d.status === 'Error').length
  const totalMaint = mappedDevices.filter(d => d.status === 'Maintenance').length

  if (mappedDevices.length === 0) {
    return (
      <div className="flex h-64 items-center justify-center rounded-xl border border-surface-800 bg-surface-900">
        <p className="text-sm text-slate-500">
          No devices with coordinates. Add latitude/longitude to devices to see them on the map.
        </p>
      </div>
    )
  }

  return (
    <div className="rounded-xl border border-surface-800 bg-surface-900 shadow-lg overflow-hidden">
      <div ref={mapRef} className="h-80 w-full" style={{ background: '#0f172a' }} />
      <div className="flex items-center justify-between border-t border-surface-800 px-4 py-2">
        <div className="flex items-center gap-4 text-xs text-slate-500">
          {totalOnline > 0 && <span className="flex items-center gap-1"><span className="inline-block h-2 w-2 rounded-full" style={{ background: STATUS_COLORS.Online }} />{totalOnline} Online</span>}
          {totalOffline > 0 && <span className="flex items-center gap-1"><span className="inline-block h-2 w-2 rounded-full" style={{ background: STATUS_COLORS.Offline }} />{totalOffline} Offline</span>}
          {totalError > 0 && <span className="flex items-center gap-1"><span className="inline-block h-2 w-2 rounded-full" style={{ background: STATUS_COLORS.Error }} />{totalError} Error</span>}
          {totalMaint > 0 && <span className="flex items-center gap-1"><span className="inline-block h-2 w-2 rounded-full" style={{ background: STATUS_COLORS.Maintenance }} />{totalMaint} Maintenance</span>}
        </div>
        <span className="text-xs text-slate-600">{stores.length} location{stores.length !== 1 ? 's' : ''} · {mappedDevices.length} device{mappedDevices.length !== 1 ? 's' : ''}</span>
      </div>
    </div>
  )
}
