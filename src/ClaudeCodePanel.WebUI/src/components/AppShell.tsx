import { useEffect, useState } from 'react'
import { Bot, Settings2 } from 'lucide-react'
import { getNavigationItem } from '../app/navigation'
import { createWebViewBridge } from '../bridge/webViewBridge'
import type { AppBootstrapSnapshot, DashboardSnapshot, PanelKey, ThemeSnapshot } from '../types'
import { ApiConfigPage } from '../pages/ApiConfigPage'
import { ConfigEditorPage } from '../pages/ConfigEditorPage'
import { DashboardPage } from '../pages/DashboardPage'
import { EnvironmentPage } from '../pages/EnvironmentPage'
import { InstallerPage } from '../pages/InstallerPage'
import { McpManagerPage } from '../pages/McpManagerPage'
import { SkillsPage } from '../pages/SkillsPage'
import { GlassSurface } from './GlassSurface'
import { NavigationDock } from './NavigationDock'
import { WorkspaceCommandBar } from './WorkspaceCommandBar'

const bridge = createWebViewBridge()

export function AppShell() {
  const [bootstrap, setBootstrap] = useState<AppBootstrapSnapshot | null>(null)
  const [activePage, setActivePage] = useState<PanelKey>('dashboard')
  const [error, setError] = useState<string | null>(null)
  const [isRefreshing, setIsRefreshing] = useState(false)

  useEffect(() => {
    let active = true
    bridge.request('app.ready').then((response) => {
      if (!active) return
      if (!response.ok) {
        setError(response.error?.message ?? 'Web 界面初始化失败')
        return
      }
      applyTheme(response.data.theme)
      setBootstrap(response.data)
    }).catch((reason: unknown) => {
      if (active) setError(reason instanceof Error ? reason.message : 'Web 界面初始化失败')
    })
    return () => { active = false }
  }, [])

  useEffect(() => {
    const onThemeChanged = (event: Event) => {
      const theme = (event as CustomEvent<ThemeSnapshot>).detail
      if (!theme) return
      applyTheme(theme)
      setBootstrap((current) => current ? { ...current, theme } : current)
    }
    window.addEventListener('claudeconsole:theme', onThemeChanged)
    return () => window.removeEventListener('claudeconsole:theme', onThemeChanged)
  }, [])

  const refreshDashboard = async () => {
    if (isRefreshing) return
    setIsRefreshing(true)
    try {
      const response = await bridge.request('dashboard.get')
      if (!response.ok) throw new Error(response.error?.message ?? '刷新失败')
      setBootstrap((current) => current ? { ...current, dashboard: response.data } : current)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : '刷新失败')
    } finally {
      setIsRefreshing(false)
    }
  }

  const openNativeFallback = () => { void bridge.request('shell.native') }

  if (error) {
    return <div className="app-stage app-stage--centered"><GlassSurface preset="prominent" className="fatal-state" padding="1px"><div className="fatal-state__content"><Settings2 size={30} /><h1>界面加载遇到问题</h1><p>{error}</p><button type="button" onClick={() => window.location.reload()}>重新加载</button></div></GlassSurface></div>
  }

  if (!bootstrap) return <div className="app-stage app-stage--centered"><div className="boot-loader"><span /><Bot size={28} /><p>正在连接工作区</p></div></div>

  const pageMeta = getNavigationItem(activePage)
  return (
    <div className="app-stage immersive-stage">
      <div className="ambient ambient--one" /><div className="ambient ambient--two" /><div className="ambient ambient--three" />
      <NavigationDock activePage={activePage} onNavigate={setActivePage} onNativeFallback={openNativeFallback} />
      <section className="workspace-shell">
        <WorkspaceCommandBar title={pageMeta.description} onNativeFallback={openNativeFallback} />
        <div className="page-viewport" key={activePage}>{renderPage(activePage, bootstrap.dashboard, isRefreshing, refreshDashboard, setActivePage)}</div>
      </section>
    </div>
  )
}

function renderPage(page: PanelKey, dashboard: DashboardSnapshot, isRefreshing: boolean, onRefresh: () => void, onNavigate: (page: PanelKey) => void) {
  switch (page) {
    case 'dashboard': return <DashboardPage dashboard={dashboard} isRefreshing={isRefreshing} onRefresh={onRefresh} onNavigate={onNavigate} />
    case 'api-config': return <ApiConfigPage />
    case 'config-editor': return <ConfigEditorPage />
    case 'mcp-manager': return <McpManagerPage />
    case 'skill-manager': return <SkillsPage />
    case 'installer': return <InstallerPage />
    case 'env-check': return <EnvironmentPage />
  }
}

function applyTheme(theme: ThemeSnapshot) {
  document.documentElement.dataset.theme = theme.isDark ? 'dark' : 'light'
  document.documentElement.style.setProperty('--accent', theme.accentColor)
  document.documentElement.style.setProperty('--accent-rgb', hexToRgb(theme.accentColor))
}

function hexToRgb(hex: string) {
  const value = hex.replace('#', '')
  return `${Number.parseInt(value.slice(0, 2), 16)}, ${Number.parseInt(value.slice(2, 4), 16)}, ${Number.parseInt(value.slice(4, 6), 16)}`
}

export type { DashboardSnapshot }
