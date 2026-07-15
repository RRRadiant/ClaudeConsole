import { useState } from 'react'
import { Check, Download, GitBranch, Package, RefreshCw, ShieldCheck } from 'lucide-react'

const dependencies = [
  { name: 'Node.js', description: 'JavaScript 运行时', version: 'v22.14.0', icon: Package, ok: true },
  { name: 'npm', description: 'Node 包管理器', version: '10.9.2', icon: Download, ok: true },
  { name: 'Git', description: '版本控制系统', version: '2.48.1', icon: GitBranch, ok: true },
]

export function EnvironmentPage() {
  const [checked, setChecked] = useState(false)
  return (
    <main className="workspace-page" aria-labelledby="environment-title">
      <div className="page-heading"><div><span className="eyebrow">ENVIRONMENT</span><h1 id="environment-title">开发环境</h1><p>检测 Claude Code 运行所需的本机依赖。</p></div><button className="primary-action" type="button" onClick={() => setChecked(true)}><RefreshCw size={16} className={checked ? 'once-spin' : ''} />重新检测</button></div>
      {checked && <div className="inline-success"><Check size={15} />检测完成</div>}
      <div className="workspace-columns">
        <section className="work-surface dependency-list"><div className="section-heading"><div><span className="eyebrow">DEPENDENCIES</span><h2>运行依赖</h2></div><span className="page-status"><i />3 / 3 就绪</span></div>{dependencies.map(({ name, description, version, icon: Icon }) => <article key={name}><span className="row-icon"><Icon size={20} /></span><span><strong>{name}</strong><small>{description}</small></span><code>{version}</code><em className="is-ok"><Check size={13} />已安装</em></article>)}</section>
        <aside className="context-surface environment-card"><span className="environment-shield"><ShieldCheck size={34} /></span><span className="eyebrow">HEALTH SUMMARY</span><h2>运行良好</h2><p className="context-copy">所有核心依赖均已检测到，Claude Code 可以正常运行。</p><dl><div><dt>就绪</dt><dd>3</dd></div><div><dt>缺失</dt><dd>0</dd></div><div><dt>上次检测</dt><dd>刚刚</dd></div></dl></aside>
      </div>
    </main>
  )
}
