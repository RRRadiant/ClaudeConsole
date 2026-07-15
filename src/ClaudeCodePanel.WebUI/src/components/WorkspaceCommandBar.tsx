import { Command, Search, Settings2 } from 'lucide-react'

interface WorkspaceCommandBarProps {
  title: string
  onNativeFallback: () => void
}

export function WorkspaceCommandBar({ title, onNativeFallback }: WorkspaceCommandBarProps) {
  return (
    <header className="workspace-commandbar">
      <button type="button" className="workspace-selector">
        <Command size={16} />
        <span>Claude Code 开发环境</span>
        <small>本地</small>
      </button>
      <label className="command-search">
        <Search size={18} />
        <span className="sr-only">快速命令</span>
        <input aria-label="快速命令" placeholder={`快速操作… 搜索${title}或执行命令`} />
        <kbd>Ctrl K</kbd>
      </label>
      <span className="host-connection"><i /> 主机已连接</span>
      <button type="button" className="command-icon" aria-label="原生故障恢复" onClick={onNativeFallback}><Settings2 size={18} /></button>
    </header>
  )
}
