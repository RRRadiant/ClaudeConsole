import { Activity, RefreshCw, TrendingUp } from 'lucide-react'
import { HealthStrip } from '../components/HealthStrip'
import { TaskShelf } from '../components/TaskShelf'
import type { DashboardSnapshot, PanelKey } from '../types'

interface DashboardPageProps {
  dashboard: DashboardSnapshot
  isRefreshing: boolean
  onRefresh: () => void
  onNavigate?: (panel: PanelKey) => void
}

const pulseSamples = [24, 36, 29, 47, 40, 58, 46, 63, 55, 72, 59, 82]
const pulsePoints = pulseSamples
  .map((value, index) => `${(index / (pulseSamples.length - 1)) * 100},${100 - value}`)
  .join(' ')
const pulseAreaPoints = `0,100 ${pulsePoints} 100,100`

export function DashboardPage({ dashboard, isRefreshing, onRefresh, onNavigate }: DashboardPageProps) {
  return (
    <main className="workspace-page dashboard-page" aria-labelledby="dashboard-title">
      <div className="page-heading dashboard-heading">
        <div><span className="eyebrow">CONTROL CENTER</span><h1 id="dashboard-title">工作区概览</h1><p>集中查看 Claude Code 的运行环境、连接状态和可用能力。</p></div>
        <button className="primary-action" type="button" onClick={onRefresh} disabled={isRefreshing}><RefreshCw size={16} className={isRefreshing ? 'is-spinning' : ''} />{isRefreshing ? '正在刷新' : '刷新状态'}</button>
      </div>

      <HealthStrip dashboard={dashboard} />

      <div className="dashboard-main-grid">
        <section className="pulse-panel">
          <div className="section-heading"><div><span className="eyebrow">SYSTEM PULSE</span><h2>系统脉冲</h2><small>当前会话健康趋势</small></div><TrendingUp size={21} /></div>
          <div className="pulse-chart" aria-label="会话健康趋势">
            <div className="chart-axis"><span>100%</span><span>50%</span><span>0%</span></div>
            <svg className="pulse-wave" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
              <defs>
                <linearGradient id="pulse-area" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#8ce7ff" stopOpacity=".58" />
                  <stop offset="100%" stopColor="#72e4c6" stopOpacity=".05" />
                </linearGradient>
                <filter id="pulse-glow" x="-20%" y="-20%" width="140%" height="140%">
                  <feGaussianBlur stdDeviation="1.8" result="blur" />
                  <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
                </filter>
              </defs>
              <polygon className="pulse-wave__area" points={pulseAreaPoints} fill="url(#pulse-area)" />
              <polyline className="pulse-wave__line" points={pulsePoints} filter="url(#pulse-glow)" />
              <circle className="pulse-wave__point" cx="100" cy={100 - pulseSamples.at(-1)!} r="1.25" />
            </svg>
            <div className="chart-times"><span>-60m</span><span>-45m</span><span>-30m</span><span>-15m</span><span>现在</span></div>
          </div>
          <div className="pulse-summary"><div><small>MCP 服务</small><strong>{dashboard.activeMcpServersCount} / {dashboard.totalMcpServersCount}</strong></div><div><small>模型就绪</small><strong>{dashboard.enabledModelsCount}</strong></div><div><small>Skills 加载</small><strong>{dashboard.installedSkillsCount}</strong></div><div><small>整体状态</small><strong className="is-ok">运行良好</strong></div></div>
        </section>

        <aside className="activity-panel-new">
          <div className="section-heading"><div><span className="eyebrow">RECENT ACTIVITY</span><h2>最近活动</h2></div><span className="live-indicator"><i />实时</span></div>
          <div className="activity-timeline">
            {dashboard.recentEvents.length === 0 ? <div className="empty-state">还没有活动记录，刷新后将在这里显示。</div> : dashboard.recentEvents.slice(0, 6).map((event, index) => <article key={`${event.timestamp}-${event.message}`} style={{ animationDelay: `${index * 35}ms` }}><span className={`timeline-marker is-${event.type}`}><Activity size={12} /></span><div><strong>{event.message}</strong><small>{event.type === 'success' ? '工作区状态' : '系统通知'}</small></div><time dateTime={event.timestamp}>{formatRelativeTime(event.timestamp)}</time></article>)}
          </div>
          <button type="button" className="text-action">查看全部活动 <span>→</span></button>
        </aside>
      </div>

      <TaskShelf dashboard={dashboard} isRefreshing={isRefreshing} onRefresh={onRefresh} onOpenConfig={() => onNavigate?.('config-editor')} />
    </main>
  )
}

function formatRelativeTime(timestamp: string) {
  const difference = Date.now() - new Date(timestamp).getTime()
  const minutes = Math.max(0, Math.round(difference / 60_000))
  if (minutes < 1) return '刚刚'
  if (minutes < 60) return `${minutes} 分钟前`
  return new Intl.DateTimeFormat('zh-CN', { hour: '2-digit', minute: '2-digit' }).format(new Date(timestamp))
}
