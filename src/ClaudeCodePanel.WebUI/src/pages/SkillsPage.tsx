import { useMemo, useState } from 'react'
import { Check, Download, Search, Sparkles } from 'lucide-react'

const installed = [
  { name: 'systematic-debugging', description: '系统化诊断复杂问题', enabled: true },
  { name: 'test-driven-development', description: '红绿重构开发循环', enabled: true },
  { name: 'brainstorming', description: '在实现前澄清设计', enabled: true },
]
const marketplace = [
  { name: 'frontend-design', description: '构建精致的产品界面', enabled: false },
  { name: 'pdf-toolkit', description: '处理和生成 PDF 文档', enabled: false },
  { name: 'data-analysis', description: '探索并验证结构化数据', enabled: false },
]

export function SkillsPage() {
  const [tab, setTab] = useState<'installed' | 'marketplace'>('installed')
  const [query, setQuery] = useState('')
  const [items, setItems] = useState(installed)
  const source = tab === 'installed' ? items : marketplace
  const filtered = useMemo(() => source.filter((skill) => `${skill.name} ${skill.description}`.toLowerCase().includes(query.toLowerCase())), [query, source])
  return (
    <main className="workspace-page" aria-labelledby="skills-title">
      <div className="page-heading"><div><span className="eyebrow">SKILLS WORKSPACE</span><h1 id="skills-title">扩展能力</h1><p>管理已安装技能并浏览可信市场来源。</p></div><button className="primary-action" type="button"><Download size={16} />从来源安装</button></div>
      <div className="workspace-columns">
        <section className="work-surface list-surface">
          <div className="list-toolbar"><div className="tab-list" role="tablist"><button role="tab" aria-selected={tab === 'installed'} onClick={() => setTab('installed')}>已安装</button><button role="tab" aria-selected={tab === 'marketplace'} onClick={() => setTab('marketplace')}>市场</button></div><label className="list-search"><Search size={16} /><input aria-label="搜索 Skills" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="搜索技能" /></label></div>
          <div className="data-list skill-list">{filtered.map((skill) => <button key={skill.name} type="button"><span className="row-icon"><Sparkles size={18} /></span><span><strong>{skill.name}</strong><small>{skill.description}</small></span><em className={skill.enabled ? 'is-ok' : ''}>{skill.enabled ? <><Check size={12} />已启用</> : '可安装'}</em></button>)}</div>
        </section>
        <aside className="context-surface"><span className="eyebrow">SUMMARY</span><h2>{tab === 'installed' ? '已安装技能' : '技能市场'}</h2><div className="big-number">{filtered.length}</div><p className="context-copy">{tab === 'installed' ? '这些技能已同步到当前 Claude Code 工作区。' : '市场结果由现有仓库服务与离线回退提供。'}</p>{tab === 'installed' && <button className="secondary-action" type="button" onClick={() => setItems((current) => current.map((item, index) => index === 0 ? { ...item, enabled: !item.enabled } : item))}>切换首个技能</button>}<button className="secondary-action" type="button">刷新列表</button></aside>
      </div>
    </main>
  )
}
