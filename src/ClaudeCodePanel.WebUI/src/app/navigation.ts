import {
  Blocks,
  Bot,
  CodeXml,
  Download,
  KeyRound,
  LayoutDashboard,
  ServerCog,
  ShieldCheck,
} from 'lucide-react'
import type { PanelKey } from '../types'

export const navigationItems = [
  { key: 'dashboard', label: '仪表盘', description: '状态与概览', icon: LayoutDashboard },
  { key: 'api-config', label: 'API 配置', description: '提供商与凭据', icon: KeyRound },
  { key: 'config-editor', label: '配置编辑器', description: '配置文件', icon: CodeXml },
  { key: 'mcp-manager', label: 'MCP 服务', description: '服务与连接', icon: ServerCog },
  { key: 'skill-manager', label: 'Skills', description: '扩展能力', icon: Blocks },
  { key: 'installer', label: '安装管理', description: 'CLI 安装管理', icon: Download },
  { key: 'env-check', label: '环境检查', description: '开发环境', icon: ShieldCheck },
] satisfies Array<{ key: PanelKey; label: string; description: string; icon: typeof Bot }>

export const getNavigationItem = (key: PanelKey) =>
  navigationItems.find((item) => item.key === key) ?? navigationItems[0]
