import { CodeXml, RefreshCw, ShieldCheck } from 'lucide-react'
import type { DashboardSnapshot } from '../types'

interface TaskShelfProps {
  dashboard: DashboardSnapshot
  isRefreshing?: boolean
  onRefresh?: () => void
  onOpenConfig?: () => void
}

export function TaskShelf({ dashboard, isRefreshing = false, onRefresh, onOpenConfig }: TaskShelfProps) {
  const environmentReady = dashboard.isClaudeInstalled && dashboard.apiConnected
  return (
    <section className={`task-shelf ${isRefreshing ? 'is-active' : ''}`} aria-label="任务与快捷操作">
      <div className="task-progress-block">
        <span className={`task-orb ${isRefreshing ? 'is-spinning' : ''}`}><RefreshCw size={17} /></span>
        <div>
          <small>任务与快捷操作</small>
          <strong>{isRefreshing ? '正在同步工作区状态' : '最近任务已完成'}</strong>
          <span>{isRefreshing ? '读取本机配置与服务状态…' : '所有状态均为最新'}</span>
        </div>
        <div className="task-meter" role={isRefreshing ? 'progressbar' : undefined} aria-label={isRefreshing ? '同步进度' : undefined}>
          <i style={{ width: isRefreshing ? '68%' : '100%' }} />
        </div>
      </div>
      <div className="quick-actions">
        <small>快速操作</small>
        <div>
          <button type="button" onClick={onOpenConfig}><CodeXml size={16} />打开配置编辑器</button>
          <button type="button" onClick={onRefresh}><RefreshCw size={16} />刷新状态</button>
        </div>
      </div>
      <div className="environment-summary">
        <span className={environmentReady ? 'is-ok' : 'is-warning'}><ShieldCheck size={24} /></span>
        <div><small>环境健康摘要</small><strong>{environmentReady ? '运行良好' : '需要检查'}</strong><span>{dashboard.activeMcpServersCount} / {dashboard.totalMcpServersCount} 服务 · {dashboard.enabledModelsCount} 模型</span></div>
      </div>
    </section>
  )
}
