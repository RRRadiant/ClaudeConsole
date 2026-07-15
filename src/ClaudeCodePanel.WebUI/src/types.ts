export type EventType = 'success' | 'error' | 'info'

export interface DashboardEventSnapshot {
  timestamp: string
  message: string
  type: EventType
}

export interface DashboardSnapshot {
  claudeVersion: string
  isClaudeInstalled: boolean
  apiConnected: boolean
  apiProvider: string
  enabledModelsCount: number
  installedSkillsCount: number
  activeMcpServersCount: number
  totalMcpServersCount: number
  recentEvents: DashboardEventSnapshot[]
  lastUpdated: string
}

export interface ThemeSnapshot {
  mode: 'system' | 'light' | 'dark' | 'custom'
  isDark: boolean
  accentColor: string
}

export interface AppBootstrapSnapshot {
  dashboard: DashboardSnapshot
  theme: ThemeSnapshot
}

export interface NavigationSnapshot {
  panel: PanelKey
  useNativeShell: boolean
}

export type PanelKey =
  | 'dashboard'
  | 'api-config'
  | 'config-editor'
  | 'mcp-manager'
  | 'skill-manager'
  | 'installer'
  | 'env-check'

export interface BridgeResponse<T> {
  id?: string
  ok: boolean
  data: T
  error?: {
    code: string
    message: string
  }
}
