import { Bot, CircleHelp, Settings2 } from 'lucide-react'
import { navigationItems } from '../app/navigation'
import type { PanelKey } from '../types'
import { GlassSurface } from './GlassSurface'

interface NavigationDockProps {
  activePage: PanelKey
  onNavigate: (page: PanelKey) => void
  onNativeFallback: () => void
}

export function NavigationDock({ activePage, onNavigate, onNativeFallback }: NavigationDockProps) {
  return (
    <GlassSurface preset="navigation" className="dock-frame" padding="1px">
      <aside className="navigation-dock" aria-label="主导航">
        <div className="dock-brand" title="Claude Console"><Bot size={23} /></div>
        <nav className="dock-items" aria-label="工作区页面">
          {navigationItems.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              type="button"
              aria-label={label}
              aria-current={activePage === key ? 'page' : undefined}
              className={`dock-item ${activePage === key ? 'is-active' : ''}`}
              onClick={() => onNavigate(key)}
              title={label}
            >
              <Icon size={21} />
              <span>{label}</span>
            </button>
          ))}
        </nav>
        <div className="dock-footer">
          <button type="button" className="dock-utility" aria-label="设置" onClick={onNativeFallback}><Settings2 size={18} /></button>
          <button type="button" className="dock-utility" aria-label="帮助"><CircleHelp size={18} /></button>
          <span className="dock-online" title="主机已连接" />
        </div>
      </aside>
    </GlassSurface>
  )
}
