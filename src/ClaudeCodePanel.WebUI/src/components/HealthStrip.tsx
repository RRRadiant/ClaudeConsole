import { Bot, Check, CloudCog, ServerCog, Sparkles, TerminalSquare } from 'lucide-react'
import type { DashboardSnapshot } from '../types'

export function HealthStrip({ dashboard }: { dashboard: DashboardSnapshot }) {
  const items = [
    { label: 'Claude CLI', value: dashboard.isClaudeInstalled ? dashboard.claudeVersion || '已安装' : '未安装', icon: TerminalSquare, ok: dashboard.isClaudeInstalled },
    { label: 'API 连接', value: dashboard.apiConnected ? dashboard.apiProvider || '已连接' : '未连接', icon: CloudCog, ok: dashboard.apiConnected },
    { label: '已启用模型', value: String(dashboard.enabledModelsCount), icon: Bot, ok: dashboard.enabledModelsCount > 0 },
    { label: 'Skills', value: String(dashboard.installedSkillsCount), icon: Sparkles, ok: dashboard.installedSkillsCount > 0 },
    { label: 'MCP 服务', value: `${dashboard.activeMcpServersCount} / ${dashboard.totalMcpServersCount}`, icon: ServerCog, ok: dashboard.activeMcpServersCount === dashboard.totalMcpServersCount },
  ]

  return (
    <section className="health-strip" aria-label="系统健康">
      <div className="health-strip__title"><span className="pulse-mark" /><strong>系统健康</strong></div>
      {items.map(({ label, value, icon: Icon, ok }) => (
        <div className="health-item" key={label}>
          <span className="health-icon"><Icon size={17} /></span>
          <span><small>{label}</small><strong>{value}</strong></span>
          <em className={ok ? 'is-ok' : 'is-warning'}><Check size={11} />{ok ? '正常' : '需处理'}</em>
        </div>
      ))}
    </section>
  )
}
