import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import {
  startDeviceHub,
  stopDeviceHub,
  getDeviceHubConnection,
} from '@/lib/signalr'
import { useAuth } from '@/hooks/useAuth'

/**
 * Connects to the SignalR device hub while authenticated and
 * invalidates React Query caches when real-time events arrive.
 * Mount once in the app layout — all pages get live updates.
 */
export function useSignalR() {
  const { isAuthenticated } = useAuth()
  const queryClient = useQueryClient()
  const [isConnected, setIsConnected] = useState(false)

  useEffect(() => {
    if (!isAuthenticated) {
      return
    }

    let cancelled = false

    const connect = async () => {
      try {
        const conn = await startDeviceHub()
        if (cancelled) return

        conn.on('DeviceStatusChanged', () => {
          queryClient.invalidateQueries({ queryKey: ['devices'] })
          queryClient.invalidateQueries({ queryKey: ['device'] })
          queryClient.invalidateQueries({ queryKey: ['telemetry-summary'] })
          queryClient.invalidateQueries({ queryKey: ['analytics'] })
        })

        conn.on('TelemetryReceived', () => {
          queryClient.invalidateQueries({ queryKey: ['devices'] })
          queryClient.invalidateQueries({ queryKey: ['device'] })
          queryClient.invalidateQueries({ queryKey: ['device-telemetry'] })
          queryClient.invalidateQueries({ queryKey: ['telemetry-summary'] })
          queryClient.invalidateQueries({ queryKey: ['telemetry'] })
          queryClient.invalidateQueries({ queryKey: ['analytics'] })
        })

        conn.on('AlertTriggered', () => {
          queryClient.invalidateQueries({ queryKey: ['alerts'] })
          queryClient.invalidateQueries({ queryKey: ['telemetry-summary'] })
        })

        conn.on('ContentUpdated', () => {
          queryClient.invalidateQueries({ queryKey: ['content'] })
        })

        conn.on('ScheduleChanged', () => {
          queryClient.invalidateQueries({ queryKey: ['schedules'] })
        })

        conn.onreconnecting(() => setIsConnected(false))
        conn.onreconnected(() => setIsConnected(true))
        conn.onclose(() => setIsConnected(false))

        setIsConnected(true)
      } catch (err) {
        console.error('SignalR connection failed:', err)
      }
    }

    connect()

    return () => {
      cancelled = true
      const conn = getDeviceHubConnection()
      conn.off('DeviceStatusChanged')
      conn.off('TelemetryReceived')
      conn.off('AlertTriggered')
      conn.off('ContentUpdated')
      conn.off('ScheduleChanged')
      stopDeviceHub()
      setIsConnected(false)
    }
  }, [isAuthenticated, queryClient])

  return { isConnected }
}
