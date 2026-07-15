import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AppShell } from './AppShell'

describe('AppShell', () => {
  it('keeps all seven workflows inside the React workspace', async () => {
    render(<AppShell />)

    await screen.findByRole('heading', { name: '工作区概览' })

    const destinations = [
      ['API 配置', '提供商与凭据'],
      ['配置编辑器', '配置文件'],
      ['MCP 服务', '服务与连接'],
      ['Skills', '扩展能力'],
      ['安装管理', 'CLI 安装管理'],
      ['环境检查', '开发环境'],
    ] as const

    for (const [buttonName, heading] of destinations) {
      fireEvent.click(screen.getByRole('button', { name: buttonName }))
      await waitFor(() => expect(screen.getByRole('heading', { name: heading })).toBeInTheDocument())
      expect(screen.getByRole('button', { name: buttonName })).toHaveAttribute('aria-current', 'page')
    }
  })

  it('renders the approved health strip and task shelf', async () => {
    render(<AppShell />)

    expect(await screen.findByRole('region', { name: '系统健康' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: '任务与快捷操作' })).toBeInTheDocument()
  })

  it('supports the API configuration workflow', async () => {
    render(<AppShell />)
    await screen.findByRole('heading', { name: '工作区概览' })
    fireEvent.click(screen.getByRole('button', { name: /API 配置/ }))

    expect(await screen.findByRole('heading', { name: '提供商与凭据' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'OpenAI' }))
    expect(screen.getByRole('button', { name: 'OpenAI' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByLabelText('API 密钥')).toHaveAttribute('type', 'password')
    expect(screen.getByRole('button', { name: '保存配置' })).toBeInTheDocument()
  })

  it('supports editing configuration files', async () => {
    render(<AppShell />)
    await screen.findByRole('heading', { name: '工作区概览' })
    fireEvent.click(screen.getByRole('button', { name: '配置编辑器' }))

    expect(await screen.findByRole('heading', { name: '配置文件' })).toBeInTheDocument()
    const editor = screen.getByLabelText('配置内容')
    fireEvent.change(editor, { target: { value: '{\n  "model": "claude-sonnet-4-6"\n}' } })
    expect(screen.getByText('未保存')).toBeInTheDocument()
  })

  it('opens the MCP server editor', async () => {
    render(<AppShell />)
    await screen.findByRole('heading', { name: '工作区概览' })
    fireEvent.click(screen.getByRole('button', { name: /MCP 服务/ }))

    expect(await screen.findByRole('heading', { name: '服务与连接' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: '添加服务' }))
    expect(screen.getByRole('dialog', { name: '添加 MCP 服务' })).toBeInTheDocument()
  })

  it('switches between installed and marketplace skills', async () => {
    render(<AppShell />)
    await screen.findByRole('heading', { name: '工作区概览' })
    fireEvent.click(screen.getByRole('button', { name: /^Skills/ }))

    expect(await screen.findByRole('heading', { name: '扩展能力' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('tab', { name: '市场' }))
    expect(screen.getByRole('tab', { name: '市场' })).toHaveAttribute('aria-selected', 'true')
  })

  it('shows installer progress after starting an install', async () => {
    render(<AppShell />)
    await screen.findByRole('heading', { name: '工作区概览' })
    fireEvent.click(screen.getByRole('button', { name: /安装管理/ }))

    expect(await screen.findByRole('heading', { name: 'CLI 安装管理' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: '通过 npm 安装' }))
    expect(screen.getByRole('progressbar', { name: '安装进度' })).toBeInTheDocument()
  })

  it('runs an environment check', async () => {
    render(<AppShell />)
    await screen.findByRole('heading', { name: '工作区概览' })
    fireEvent.click(screen.getByRole('button', { name: /环境检查/ }))

    expect(await screen.findByRole('heading', { name: '开发环境' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: '重新检测' }))
    expect(screen.getByText('检测完成')).toBeInTheDocument()
  })
})
