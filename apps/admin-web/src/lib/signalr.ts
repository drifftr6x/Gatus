import * as signalR from '@microsoft/signalr'

const HUB_URL = import.meta.env.VITE_HUB_URL || '/hubs/devices'

let connection: signalR.HubConnection | null = null

export function getDeviceHubConnection(): signalR.HubConnection {
  if (connection) {
    return connection
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, {
      accessTokenFactory: () => localStorage.getItem('accessToken') ?? '',
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  return connection
}

export async function startDeviceHub(): Promise<signalR.HubConnection> {
  const conn = getDeviceHubConnection()
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    await conn.start()
  }
  return conn
}

export async function stopDeviceHub(): Promise<void> {
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    await connection.stop()
  }
  connection = null
}

// Event payload types
export interface DeviceStatusChangedEvent {
  deviceId: string
  status: string
  timestamp: string
}

export interface ContentUpdatedEvent {
  contentId: string
  name: string
}

export interface ScheduleChangedEvent {
  scheduleId: string
  deviceId: string
  changeType: string
}
