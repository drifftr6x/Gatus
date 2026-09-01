const API_BASE = import.meta.env.VITE_API_URL || '/api'

class ApiClient {
  private accessToken: string | null = null

  setTokens(accessToken: string, _refreshToken: string) {
    // Access token in memory only; refresh token is httpOnly cookie set by server
    this.accessToken = accessToken
    // Keep a session marker so we know the user was logged in (for page refresh)
    localStorage.setItem('gatus-session', 'active')
  }

  loadTokens() {
    // Access token is in-memory only; on page refresh it will be null
    // The refresh token cookie is sent automatically with credentials: 'include'
    this.accessToken = null
  }

  clearTokens() {
    this.accessToken = null
    localStorage.removeItem('gatus-session')
  }

  get hasSession(): boolean {
    return localStorage.getItem('gatus-session') === 'active'
  }

  private async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> {
    const url = `${API_BASE}${endpoint}`
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string>),
    }

    if (this.accessToken) {
      headers['Authorization'] = `Bearer ${this.accessToken}`
    }

    const response = await fetch(url, { ...options, headers, credentials: 'include' })

    if (response.status === 401 && this.hasSession) {
      // Try to refresh token
      const refreshed = await this.refreshAccessToken()
      if (refreshed) {
        headers['Authorization'] = `Bearer ${this.accessToken}`
        const retryResponse = await fetch(url, { ...options, headers })
        return this.handleResponse<T>(retryResponse)
      }
    }

    return this.handleResponse<T>(response)
  }

  private async handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Unknown error' }))
      throw new Error(error.error || `HTTP ${response.status}`)
    }
    return response.json()
  }

  private async refreshAccessToken(): Promise<boolean> {
    try {
      // Refresh token is in httpOnly cookie — sent automatically with credentials: 'include'
      const response = await fetch(`${API_BASE}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: '{}',
      })

      if (!response.ok) {
        this.clearTokens()
        return false
      }

      const data = await response.json()
      this.setTokens(data.accessToken, data.refreshToken)
      return true
    } catch {
      this.clearTokens()
      return false
    }
  }

  async get<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: 'GET' })
  }

  async post<T>(endpoint: string, data?: unknown): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'POST',
      body: data ? JSON.stringify(data) : undefined,
    })
  }

  async put<T>(endpoint: string, data?: unknown): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'PUT',
      body: data ? JSON.stringify(data) : undefined,
    })
  }

  async delete<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: 'DELETE' })
  }
}

export const api = new ApiClient()
api.loadTokens()

// API Types
export interface LoginRequest {
  email: string
  password: string
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  user: UserDto
}

export interface UserDto {
  id: string
  email: string
  firstName: string
  lastName: string
  displayName: string
  role: string
  isActive: boolean
  lastLoginAt?: string
}

export interface DeviceDto {
  id: string
  name: string
  serialNumber?: string
  description?: string
  location?: string
  status: string
  lastSeenAt?: string
  hostname?: string
  ipAddress?: string
  macAddress?: string
  firmwareVersion?: string
  groupId?: string
  groupName?: string
  tags?: string
  createdAt: string
  updatedAt?: string
  isActive: boolean
  cpuPercent?: number
  memoryPercent?: number
  diskFreePercent?: number
  diskFreeGb?: number
  uptimeSeconds?: number
  osVersion?: string
  latitude?: number
  longitude?: number
  domainName?: string
  domainJoinStatus?: string
  domainSecureChannelHealthy?: boolean
  }

export interface DeviceListResponse {
  devices: DeviceDto[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ContentDto {
  id: string
  name: string
  description?: string
  type: string
  url: string
  thumbnailUrl?: string
  fileSizeBytes: number
  durationSeconds?: number
  mimeType?: string
  createdAt: string
  updatedAt?: string
  isActive: boolean
  createdByName?: string
}

export interface ContentListResponse {
  contents: ContentDto[]
  totalCount: number
  page: number
  pageSize: number
}

// API Functions
export interface ProductFeatureFlags {
  groups: boolean
  schedules: boolean
  content: boolean
  alerts: boolean
  analytics: boolean
  notifications: boolean
  logs: boolean
  advancedReports: boolean
}

export interface ProductConfigurationDto {
  productName: string
  edition: string
  version: string
  features: ProductFeatureFlags
}

export const productApi = {
  get: () => api.get<ProductConfigurationDto>('/product'),
}

export const authApi = {
  login: (data: LoginRequest) => api.post<AuthResponse>('/auth/login', data),
  logout: () => api.post('/auth/logout'),
  getCurrentUser: () => api.get<UserDto>('/auth/me'),
}

export interface DevicePolicyDto {
  version: string
  homeUrl?: string
  sessionTimeoutSeconds: number
  inactivityResetSeconds: number
  clearSessionOnReset: boolean
  allowedUrls: string[]
  blockedUrls: string[]
  restartOnExit: boolean
  maxRestartAttempts: number
  restartDelaySeconds: number
  kioskEnabled: boolean
  lockdown: { profile: string; hideDesktop: boolean; hideTaskbar: boolean; maintenanceModeAllowed: boolean }
}

export const devicesApi = {
  getPolicy: (id: string) => api.get<DevicePolicyDto>(`/devices/${id}/policy`),
  updatePolicy: (id: string, data: {
    homeUrl?: string; sessionTimeoutSeconds?: number; inactivityResetSeconds?: number;
    clearSessionOnReset?: boolean; allowedUrls?: string[]; blockedUrls?: string[];
    restartOnExit?: boolean; maxRestartAttempts?: number; restartDelaySeconds?: number;
    kioskEnabled?: boolean; lockdownProfile?: string
  }) => api.put<DevicePolicyDto>(`/devices/${id}/policy`, data),
  list: (params?: { page?: number; pageSize?: number; status?: string; search?: string }) => {
    const searchParams = new URLSearchParams()
    if (params?.page) searchParams.set('page', params.page.toString())
    if (params?.pageSize) searchParams.set('pageSize', params.pageSize.toString())
    if (params?.status) searchParams.set('status', params.status)
    if (params?.search) searchParams.set('search', params.search)
    const query = searchParams.toString()
    return api.get<DeviceListResponse>(`/devices${query ? `?${query}` : ''}`)
  },
  get: (id: string) => api.get<DeviceDto>(`/devices/${id}`),
  create: (data: Partial<DeviceDto>) => api.post<DeviceDto>('/devices', data),
  update: (id: string, data: Partial<DeviceDto>) => api.put<DeviceDto>(`/devices/${id}`, data),
  delete: (id: string) => api.delete(`/devices/${id}`),
  bulkCommand: (data: { deviceIds: string[]; commandType: string; payload?: string }) =>
    api.post<BulkOperationResponse>('/devices/bulk-command', data),
  bulkAssignGroup: (data: { deviceIds: string[]; groupId: string | null }) =>
    api.post<BulkOperationResponse>('/devices/bulk-assign-group', data),
  bulkTag: (data: { deviceIds: string[]; tags: string }) =>
    api.post<BulkOperationResponse>('/devices/bulk-tag', data),
  import: (data: ImportDevicesRequest) =>
    api.post<ImportDevicesResponse>('/devices/import', data),
  listAll: async () => {
    // Single request; the device list page already uses pageSize 500
    const result = await devicesApi.list({ page: 1, pageSize: 1000 })
    return result.devices
  },
    }

  export interface ImportDeviceRow {
  name: string
  serialNumber?: string
  description?: string
  location?: string
  hostname?: string
  ipAddress?: string
  macAddress?: string
  firmwareVersion?: string
  group?: string
  }

  export interface ImportDevicesRequest {
  devices: ImportDeviceRow[]
  }

  export interface ImportRowResult {
  row: number
  name: string
  status: 'created' | 'skipped' | 'error'
  message?: string
  }

  export interface ImportDevicesResponse {
  totalRows: number
  imported: number
  skipped: number
  failed: number
  results: ImportRowResult[]
  }

export interface BulkOperationResult {
  deviceId: string
  deviceName: string
  success: boolean
  error?: string
}

export interface BulkOperationResponse {
  totalRequested: number
  succeeded: number
  failed: number
  results: BulkOperationResult[]
}

export interface DeviceGroupDto {
  id: string
  name: string
  description?: string
  deviceCount: number
  createdAt: string
  updatedAt?: string
}

export interface DeviceConfigTemplateDto {
  id: string
  name: string
  description?: string
  configJson: string
  createdAt: string
  updatedAt?: string
}

export const groupsApi = {
  list: () => api.get<DeviceGroupDto[]>('/deviceGroups'),
  get: (id: string) => api.get<DeviceGroupDto>(`/deviceGroups/${id}`),
  create: (data: { name: string; description?: string }) =>
    api.post<DeviceGroupDto>('/deviceGroups', data),
  update: (id: string, data: { name: string; description?: string }) =>
    api.put<DeviceGroupDto>(`/deviceGroups/${id}`, data),
  delete: (id: string) => api.delete(`/deviceGroups/${id}`),
}

export const templatesApi = {
  list: () => api.get<DeviceConfigTemplateDto[]>('/device-config-templates'),
  get: (id: string) => api.get<DeviceConfigTemplateDto>(`/device-config-templates/${id}`),
  create: (data: { name: string; description?: string; configJson: string }) =>
    api.post<DeviceConfigTemplateDto>('/device-config-templates', data),
  update: (id: string, data: { name: string; description?: string; configJson: string }) =>
    api.put<DeviceConfigTemplateDto>(`/device-config-templates/${id}`, data),
  delete: (id: string) => api.delete(`/device-config-templates/${id}`),
}

export const contentApi = {
  list: (params?: { page?: number; pageSize?: number; type?: string; search?: string }) => {
    const searchParams = new URLSearchParams()
    if (params?.page) searchParams.set('page', params.page.toString())
    if (params?.pageSize) searchParams.set('pageSize', params.pageSize.toString())
    if (params?.type) searchParams.set('type', params.type)
    if (params?.search) searchParams.set('search', params.search)
    const query = searchParams.toString()
    return api.get<ContentListResponse>(`/content${query ? `?${query}` : ''}`)
  },
  get: (id: string) => api.get<ContentDto>(`/content/${id}`),
  create: (data: Partial<ContentDto>) => api.post<ContentDto>('/content', data),
  update: (id: string, data: Partial<ContentDto>) => api.put<ContentDto>(`/content/${id}`, data),
  delete: (id: string) => api.delete(`/content/${id}`),
  upload: (file: File, name: string, description?: string) => {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('name', name)
    if (description) formData.append('description', description)
    return api.post<ContentDto>('/content/upload', formData)
  },
  versions: (contentId: string) => api.get<ContentVersionDto[]>(`/content/${contentId}/versions`),
  }

  export interface ContentVersionDto {
  id: string
  version: number
  sha256Checksum: string
  fileSizeBytes: number
  mimeType?: string
  createdAt: string
  isActive: boolean
  releaseNotes?: string
  deploymentCount: number
  }

  export const deploymentsApi = {
  list: (params?: { status?: string; limit?: number }) => {
    const searchParams = new URLSearchParams()
    if (params?.status) searchParams.set('status', params.status)
    if (params?.limit) searchParams.set('limit', params.limit.toString())
    const query = searchParams.toString()
    return api.get<DeploymentDto[]>(`/deployments${query ? `?${query}` : ''}`)
  },
  create: (data: CreateDeploymentRequest) => api.post<{ id: string; name: string; deviceCount: number }>('/deployments', data),
  cancel: (id: string) => api.post(`/deployments/${id}/cancel`),
  }

  export interface DeploymentDto {
  id: string
  name: string
  description?: string
  contentName: string
  contentVersion: number
  contentVersionId: string
  status: string
  scheduledAt?: string
  startedAt?: string
  completedAt?: string
  createdAt: string
  results: DeploymentResultDto[]
  }

  export interface DeploymentResultDto {
  id: string
  deviceId: string
  deviceName: string
  status: string
  startedAt?: string
  completedAt?: string
  errorMessage?: string
  rollbackPerformed: boolean
  }

  export interface CreateDeploymentRequest {
  contentVersionId: string
  deviceIds?: string[]
  groupId?: string
  name?: string
  description?: string
  scheduledAt?: string
  rolloutPercent?: number
  }

export interface EnrollmentTokenDto {
  id: string
  label?: string
  expiresAt: string
  isUsed: boolean
  usedAt?: string
  isRevoked: boolean
  createdAt: string
}

export interface CreatedEnrollmentTokenDto {
  id: string
  token: string
  expiresAt: string
}

export interface CommandDto {
  id: string
  deviceId: string
  deviceName: string
  type: string
  status: string
  createdByName: string
  createdAt: string
  expiresAt?: string
  acknowledgedAt?: string
  completedAt?: string
  resultMessage?: string
}

export interface CommandListResponse {
  commands: CommandDto[]
  totalCount: number
}

export interface AlertDto {
  id: string
  deviceId: string
  deviceName: string
  severity: string
  title: string
  message?: string
  status: string
  raisedAt: string
  acknowledgedAt?: string
  acknowledgedByName?: string
  resolvedAt?: string
  autoResolved: boolean
}

export interface AlertListResponse {
  alerts: AlertDto[]
  totalCount: number
  activeCount: number
}

export interface AlertRuleDto {
  id: string
  name: string
  metric: string
  operator: string
  threshold: number
  severity: string
  isEnabled: boolean
  createdAt: string
}

export const alertsApi = {
  list: (params?: { severity?: string; status?: string; deviceId?: string; limit?: number }) => {
    const sp = new URLSearchParams()
    if (params?.severity) sp.set('severity', params.severity)
    if (params?.status) sp.set('status', params.status)
    if (params?.deviceId) sp.set('deviceId', params.deviceId)
    if (params?.limit) sp.set('limit', params.limit.toString())
    const q = sp.toString()
    return api.get<AlertListResponse>(`/alerts${q ? `?${q}` : ''}`)
  },
  count: () => api.get<{ active: number; critical: number }>('/alerts/count'),
  acknowledge: (id: string) => api.post(`/alerts/${id}/acknowledge`),
  resolve: (id: string) => api.post(`/alerts/${id}/resolve`),
  rules: () => api.get<AlertRuleDto[]>('/alerts/rules'),
  }

  export interface DomainHealthSettings {
  expectedDomain?: string | null
  alertOnMismatch: boolean
  alertOnTrustBroken: boolean
  }

  export const settingsApi = {
  getDomainHealth: () => api.get<DomainHealthSettings>('/settings/domain-health'),
  updateDomainHealth: (data: DomainHealthSettings) =>
  api.put<DomainHealthSettings>('/settings/domain-health', data),
  }

export const commandsApi = {
  history: (params?: { deviceId?: string; status?: string; limit?: number }) => {
    const searchParams = new URLSearchParams()
    if (params?.deviceId) searchParams.set('deviceId', params.deviceId)
    if (params?.status) searchParams.set('status', params.status)
    if (params?.limit) searchParams.set('limit', params.limit.toString())
    const query = searchParams.toString()
    return api.get<CommandListResponse>(`/commands/history${query ? `?${query}` : ''}`)
  },
  issue: (deviceId: string, data: { type: string; payload?: string; timeoutSeconds?: number; expiresInMinutes?: number }) =>
    api.post<CommandDto>(`/devices/${deviceId}/commands`, data),
  cancel: (id: string) => api.post(`/commands/${id}/cancel`),
}

export const enrollmentApi = {
  list: () => api.get<EnrollmentTokenDto[]>('/enrollmenttokens'),
  create: (data: { label?: string; expiresInHours?: number; deviceId?: string }) =>
    api.post<CreatedEnrollmentTokenDto>('/enrollmenttokens', data),
  revoke: (id: string) => api.post(`/enrollmenttokens/${id}/revoke`),
  delete: (id: string) => api.delete(`/enrollmenttokens/${id}`),
}

export const usersApi = {
  list: () => api.get<UserDto[]>('/users'),
  get: (id: string) => api.get<UserDto>(`/users/${id}`),
  create: (data: Partial<UserDto> & { password: string }) => api.post<UserDto>('/users', data),
  update: (id: string, data: Partial<UserDto>) => api.put<UserDto>(`/users/${id}`, data),
  updateRole: (id: string, role: string) => api.put(`/users/${id}/role`, { role }),
  delete: (id: string) => api.delete(`/users/${id}`),
}

export interface ScheduleDto {
  id: string
  deviceId: string
  deviceName: string
  contentId: string
  contentName: string
  name: string
  description?: string
  startTime: string
  endTime: string
  priority: number
  recurrence: string
  recurrencePattern?: string
  isActive: boolean
  createdByName?: string
  createdAt: string
  updatedAt?: string
}

export interface ScheduleListResponse {
  schedules: ScheduleDto[]
  totalCount: number
}

export interface TelemetrySummaryDto {
  totalDevices: number
  onlineDevices: number
  offlineDevices: number
  devicesInError: number
  activeSchedules: number
  activeContent: number
  telemetryPointsLast24h: number
}

export interface TelemetryValueDto {
  timestamp: string
  value: string
}

export interface TelemetrySeriesDto {
  metricName: string
  unit?: string
  points: TelemetryValueDto[]
}

export const schedulesApi = {
  list: (params?: { deviceId?: string; isActive?: boolean }) => {
    const searchParams = new URLSearchParams()
    if (params?.deviceId) searchParams.set('deviceId', params.deviceId)
    if (params?.isActive !== undefined) searchParams.set('isActive', params.isActive.toString())
    const query = searchParams.toString()
    return api.get<ScheduleListResponse>(`/schedules${query ? `?${query}` : ''}`)
  },
  get: (id: string) => api.get<ScheduleDto>(`/schedules/${id}`),
  create: (data: {
    deviceId: string
    contentId: string
    name: string
    description?: string
    startTime: string
    endTime: string
    priority?: number
    recurrence?: string
  }) => api.post<ScheduleDto>('/schedules', data),
  update: (id: string, data: {
    name: string
    description?: string
    startTime: string
    endTime: string
    priority?: number
    recurrence?: string
    isActive?: boolean
  }) => api.put<ScheduleDto>(`/schedules/${id}`, data),
  delete: (id: string) => api.delete(`/schedules/${id}`),
}

export const telemetryApi = {
  summary: () => api.get<TelemetrySummaryDto>('/telemetry/summary'),
  deviceSeries: (deviceId: string, metric?: string) => {
    const searchParams = new URLSearchParams()
    if (metric) searchParams.set('metric', metric)
    const query = searchParams.toString()
    return api.get<TelemetrySeriesDto[]>(`/telemetry/device/${deviceId}${query ? `?${query}` : ''}`)
  },
}

// Analytics types
export interface DeviceUptimeSummary {
  deviceId: string
  deviceName: string
  groupName?: string
  status: string
  uptimePercent: number
  totalMinutesOnline: number
  totalMinutesOffline: number
  lastSeenAt?: string
  hasSamples: boolean
}

export interface UptimeReportResponse {
  devices: DeviceUptimeSummary[]
  totalDevices: number
  overallUptimePercent: number
  generatedAt: string
}

export interface AlertTrendPoint {
  date: string
  raised: number
  resolved: number
  critical: number
  warning: number
  info: number
}

export interface AlertTrendResponse {
  points: AlertTrendPoint[]
  totalAlerts: number
  activeAlerts: number
  resolvedAlerts: number
}

export interface TelemetryMetricAggregate {
  metricName: string
  unit: string
  min: number
  max: number
  avg: number
  latest: number
  sampleCount: number
}

export interface TelemetryAggregationResponse {
  metrics: TelemetryMetricAggregate[]
  deviceCount: number
  from: string
  to: string
}

export interface DeviceHealthSummary {
  deviceId: string
  deviceName: string
  status: string
  cpuAvg?: number
  memoryAvg?: number
  diskFreeAvg?: number
  uptimeSeconds?: number
  lastHeartbeat?: string
}

export interface LogEntryDto {
  timestamp: string
  level: string
  message: string
  exception?: string
  correlationId?: string
  requestPath?: string
  statusCode?: number
  elapsed?: number
  source?: string
}

export interface LogResponseDto {
  entries: LogEntryDto[]
  totalMatched: number
}

export const logsApi = {
  list: (params?: {
    level?: string
    search?: string
    limit?: number
    lastMinutes?: number
    source?: 'audit'
  }) => {
    const sp = new URLSearchParams()
    if (params?.level) sp.set('level', params.level)
    if (params?.search) sp.set('search', params.search)
    if (params?.limit) sp.set('limit', params.limit.toString())
    if (params?.lastMinutes) sp.set('lastMinutes', params.lastMinutes.toString())
    if (params?.source) sp.set('source', params.source)
    const q = sp.toString()
    return api.get<LogResponseDto>(`/logs${q ? `?${q}` : ''}`)
  },
  levels: () => api.get<string[]>('/logs/levels'),
}

export const analyticsApi = {
  uptime: (days?: number) => {
    const sp = new URLSearchParams()
    if (days) sp.set('days', days.toString())
    const q = sp.toString()
    return api.get<UptimeReportResponse>(`/analytics/uptime${q ? `?${q}` : ''}`)
  },
  alertTrends: (days?: number) => {
    const sp = new URLSearchParams()
    if (days) sp.set('days', days.toString())
    const q = sp.toString()
    return api.get<AlertTrendResponse>(`/analytics/alert-trends${q ? `?${q}` : ''}`)
  },
  telemetry: (hours?: number, deviceId?: string) => {
    const sp = new URLSearchParams()
    if (hours) sp.set('hours', hours.toString())
    if (deviceId) sp.set('deviceId', deviceId)
    const q = sp.toString()
    return api.get<TelemetryAggregationResponse>(`/analytics/telemetry${q ? `?${q}` : ''}`)
  },
  deviceHealth: () => api.get<DeviceHealthSummary[]>('/analytics/device-health'),
  connectivity: (hours?: number, bucketMinutes?: number, deviceId?: string) => {
    const sp = new URLSearchParams()
    if (hours) sp.set('hours', hours.toString())
    if (bucketMinutes) sp.set('bucketMinutes', bucketMinutes.toString())
    if (deviceId) sp.set('deviceId', deviceId)
    const q = sp.toString()
    return api.get<ConnectivityResponse>(`/analytics/connectivity${q ? `?${q}` : ''}`)
  },
  }

  export interface ConnectivitySlot {
  timestamp: string
  status: 'online' | 'offline' | 'unknown'
  avgResponseMs?: number
  }

  export interface DeviceConnectivityDto {
  deviceId: string
  deviceName: string
  groupName?: string
  currentStatus: string
  uptimePercent: number
  slots: ConnectivitySlot[]
  }

  export interface ConnectivityResponse {
  devices: DeviceConnectivityDto[]
  hours: number
  bucketMinutes: number
  }

  export interface NotificationChannelDto {
  id: string
  name: string
  type: string
  configJson: string
  isEnabled: boolean
  createdAt: string
  updatedAt?: string
  }

  export interface NotificationTestResult {
  success: boolean
  message?: string
  }

  export const notificationChannelsApi = {
  list: () => api.get<NotificationChannelDto[]>('/notification-channels'),
  create: (data: { name: string; type: string; configJson: string }) =>
    api.post<NotificationChannelDto>('/notification-channels', data),
  update: (id: string, data: { name: string; configJson: string; isEnabled: boolean }) =>
    api.put<NotificationChannelDto>(`/notification-channels/${id}`, data),
  delete: (id: string) => api.delete(`/notification-channels/${id}`),
  test: (id: string) => api.post<NotificationTestResult>(`/notification-channels/${id}/test`),
  }
