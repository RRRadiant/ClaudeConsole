import { render, screen, within } from '@testing-library/react'
import { DashboardPage } from './DashboardPage'

const dashboard = {
  claudeVersion: '1.2.3',
  isClaudeInstalled: true,
  apiConnected: true,
  apiProvider: 'Anthropic',
  enabledModelsCount: 3,
  installedSkillsCount: 7,
  activeMcpServersCount: 2,
  totalMcpServersCount: 4,
  recentEvents: [
    {
      timestamp: '2026-07-15T12:30:00Z',
      message: 'Workspace ready',
      type: 'success' as const,
    },
  ],
  lastUpdated: '2026-07-15T12:30:00Z',
}

describe('DashboardPage', () => {
  it('renders primary workspace status and recent events', () => {
    render(<DashboardPage dashboard={dashboard} isRefreshing={false} onRefresh={() => undefined} />)

    expect(screen.getByText('工作区概览')).toBeInTheDocument()
    expect(screen.getByText('1.2.3')).toBeInTheDocument()
    expect(screen.getByText('Anthropic')).toBeInTheDocument()
    const health = screen.getByRole('region', { name: '系统健康' })
    expect(within(health).getByText('7')).toBeInTheDocument()
    expect(within(health).getByText('2 / 4')).toBeInTheDocument()
    expect(screen.getByText('Workspace ready')).toBeInTheDocument()
  })

  it('exposes refresh progress with text and disabled state', () => {
    render(<DashboardPage dashboard={dashboard} isRefreshing onRefresh={() => undefined} />)

    const button = screen.getByRole('button', { name: '正在刷新' })
    expect(button).toBeDisabled()
  })
})
