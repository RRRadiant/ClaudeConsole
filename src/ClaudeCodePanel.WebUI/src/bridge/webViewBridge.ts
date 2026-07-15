import type {
  AppBootstrapSnapshot,
  BridgeResponse,
  DashboardSnapshot,
  NavigationSnapshot,
  PanelKey,
  ThemeSnapshot,
} from '../types'

export interface WebViewTransport {
  postMessage(message: unknown): void
  addEventListener(type: 'message', listener: (event: MessageEvent) => void): void
  removeEventListener(type: 'message', listener: (event: MessageEvent) => void): void
}

interface BridgeRequestMap {
  'app.ready': { payload?: never; response: AppBootstrapSnapshot }
  'dashboard.get': { payload?: never; response: DashboardSnapshot }
  'theme.get': { payload?: never; response: ThemeSnapshot }
  'navigation.select': { payload: { panel: PanelKey }; response: NavigationSnapshot }
  'shell.native': { payload?: never; response: { useNativeShell: true } }
}

const previewDashboard: DashboardSnapshot = {
  claudeVersion: '1.2.2',
  isClaudeInstalled: true,
  apiConnected: true,
  apiProvider: 'Anthropic',
  enabledModelsCount: 4,
  installedSkillsCount: 12,
  activeMcpServersCount: 3,
  totalMcpServersCount: 4,
  recentEvents: [
    { timestamp: new Date().toISOString(), message: '工作区配置已同步', type: 'success' },
    { timestamp: new Date(Date.now() - 180_000).toISOString(), message: 'Anthropic API 连接正常', type: 'success' },
    { timestamp: new Date(Date.now() - 420_000).toISOString(), message: '有 1 个 MCP 服务等待检查', type: 'info' },
  ],
  lastUpdated: new Date().toISOString(),
}

const previewTheme: ThemeSnapshot = {
  mode: 'dark',
  isDark: true,
  accentColor: '#6FAADD',
}

function previewResponse<T extends keyof BridgeRequestMap>(
  type: T,
  payload?: BridgeRequestMap[T]['payload'],
): BridgeResponse<BridgeRequestMap[T]['response']> {
  const data = (() => {
    switch (type) {
      case 'app.ready':
        return { dashboard: previewDashboard, theme: previewTheme }
      case 'dashboard.get':
        return { ...previewDashboard, lastUpdated: new Date().toISOString() }
      case 'theme.get':
        return previewTheme
      case 'navigation.select':
        return { panel: (payload as { panel: PanelKey }).panel, useNativeShell: true }
      case 'shell.native':
        return { useNativeShell: true }
    }
  })()

  return { ok: true, data } as BridgeResponse<BridgeRequestMap[T]['response']>
}

export function createWebViewBridge(transport?: WebViewTransport) {
  const pending = new Map<string, (response: BridgeResponse<unknown>) => void>()
  const target = transport ?? window.chrome?.webview

  const onMessage = (event: MessageEvent) => {
    const response = event.data as BridgeResponse<unknown>
    if (!response?.id) return
    pending.get(response.id)?.(response)
    pending.delete(response.id)
  }

  target?.addEventListener('message', onMessage)

  return {
    request<T extends keyof BridgeRequestMap>(
      type: T,
      payload?: BridgeRequestMap[T]['payload'],
    ): Promise<BridgeResponse<BridgeRequestMap[T]['response']>> {
      if (!target) return Promise.resolve(previewResponse(type, payload))

      const id = crypto.randomUUID()
      const response = new Promise<BridgeResponse<BridgeRequestMap[T]['response']>>((resolve) => {
        pending.set(id, resolve as (value: BridgeResponse<unknown>) => void)
      })
      target.postMessage({ id, type, payload })
      return response
    },
    dispose() {
      target?.removeEventListener('message', onMessage)
      pending.clear()
    },
  }
}

declare global {
  interface Window {
    chrome?: {
      webview?: WebViewTransport
    }
  }
}
