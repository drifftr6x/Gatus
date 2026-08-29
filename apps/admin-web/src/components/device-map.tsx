import { useEffect, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { DeviceDto } from '@/lib/api'

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

function createStatusIcon(status: string) {
  const color = STATUS_COLORS[status] || STATUS_COLORS.Offline
  return L.divIcon({
    className: '',
    html: `<div style="
      width: 14px; height: 14px; border-radius: 50%;
      background: ${color}; border: 2px solid rgba(255,255,255,0.8);
      box-shadow: 0 0 6px ${color};
    "></div>`,
    iconSize: [14, 14],
    iconAnchor: [7, 7],
    popupAnchor: [0, -10],
  })
}

export function DeviceMap({ devices }: { devices: DeviceDto[] }) {
  const mapRef = useRef<HTMLDivElement>(null)
  const mapInstanceRef = useRef<L.Map | null>(null)
  const markersRef = useRef<L.LayerGroup | null>(null)

  const mappedDevices = devices.filter(d => d.latitude != null && d.longitude != null)

  useEffect(() => {
    if (!mapRef.current || mapInstanceRef.current) return

    const map = L.map(mapRef.current, {
      center: [39.8, -98.5], // Center of US
      zoom: 4,
      zoomControl: true,
      scrollWheelZoom: false,
      attributionControl: false,
    })

    // Dark tile layer matching the app theme
    L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
      maxZoom: 19,
    }).addTo(map)

    markersRef.current = L.layerGroup().addTo(map)
    mapInstanceRef.current = map

    return () => {
      map.remove()
      mapInstanceRef.current = null
    }
  }, [])

  useEffect(() => {
    if (!markersRef.current) return
    markersRef.current.clearLayers()

    mappedDevices.forEach((device) => {
      const marker = L.marker([device.latitude!, device.longitude!], {
        icon: createStatusIcon(device.status),
      })

      const popupContent = document.createElement('div')
      popupContent.style.cssText = 'font-family: inherit; min-width: 160px;'
      popupContent.innerHTML = `
        <div style="font-weight: 600; font-size: 13px; color: #e2e8f0;">${device.name}</div>
        ${device.location ? `<div style="font-size: 11px; color: #94a3b8; margin-top: 2px;">${device.location}</div>` : ''}
        <div style="display: flex; align-items: center; gap: 4px; margin-top: 4px;">
          <span style="width: 6px; height: 6px; border-radius: 50%; background: ${STATUS_COLORS[device.status] || STATUS_COLORS.Offline};"></span>
          <span style="font-size: 11px; color: #94a3b8;">${device.status}</span>
        </div>
      `

      marker.bindPopup(popupContent, {
        className: 'dark-popup',
        closeButton: false,
      })

      marker.on('click', () => {
        window.location.hash = `#/devices/${device.id}`
      })

      markersRef.current!.addLayer(marker)
    })

    // Auto-fit bounds if we have devices
    if (mappedDevices.length > 0 && mapInstanceRef.current) {
      const bounds = L.latLngBounds(mappedDevices.map(d => [d.latitude!, d.longitude!]))
      mapInstanceRef.current.fitBounds(bounds, { padding: [40, 40], maxZoom: 6 })
    }
  }, [mappedDevices])

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
      <div ref={mapRef} className="h-72 w-full" style={{ background: '#0f172a' }} />
      <div className="flex items-center justify-between border-t border-surface-800 px-4 py-2">
        <div className="flex items-center gap-4 text-xs text-slate-500">
          {Object.entries(STATUS_COLORS).map(([status, color]) => {
            const count = mappedDevices.filter(d => d.status === status).length
            if (count === 0) return null
            return (
              <span key={status} className="flex items-center gap-1">
                <span className="inline-block h-2 w-2 rounded-full" style={{ background: color }} />
                {count} {status}
              </span>
            )
          })}
        </div>
        <span className="text-xs text-slate-600">{mappedDevices.length} location{mappedDevices.length !== 1 ? 's' : ''}</span>
      </div>
    </div>
  )
}
