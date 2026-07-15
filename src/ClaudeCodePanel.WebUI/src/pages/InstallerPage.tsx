import { useState } from 'react'
import { Check, Download, PackageCheck, TerminalSquare } from 'lucide-react'

export function InstallerPage() {
  const [installing, setInstalling] = useState(false)
  return (
    <main className="workspace-page" aria-labelledby="installer-title">
      <div className="page-heading"><div><span className="eyebrow">CLI LIFECYCLE</span><h1 id="installer-title">CLI 安装管理</h1><p>安装、升级或移除 Claude Code 命令行工具。</p></div><span className="page-status"><i />安装服务就绪</span></div>
      <div className="installer-grid">
        <section className="work-surface installer-hero"><span className="installer-icon"><TerminalSquare size={34} /></span><div><small>CLAUDE CODE CLI</small><h2>命令行环境</h2><p>当前版本 1.2.2 · 通过 npm 管理</p></div><span className="installed-pill"><Check size={14} />已安装</span><div className="installer-actions"><button type="button" className="primary-action" onClick={() => setInstalling(true)}><Download size={16} />通过 npm 安装</button><button type="button" className="secondary-action">通过 winget 安装</button><button type="button" className="danger-action">卸载</button></div></section>
        <aside className="context-surface"><PackageCheck size={26} /><h2>安装建议</h2><p className="context-copy">优先使用 npm 获取最新版本。安装任务会继续运行，即使切换到其他页面。</p><dl><div><dt>Node.js</dt><dd className="is-ok">已就绪</dd></div><div><dt>npm</dt><dd className="is-ok">已就绪</dd></div><div><dt>权限</dt><dd>当前用户</dd></div></dl></aside>
      </div>
      <section className={`install-console ${installing ? 'is-running' : ''}`}><header><span><TerminalSquare size={15} />安装输出</span><small>{installing ? 'RUNNING' : 'IDLE'}</small></header>{installing ? <><div className="progress-line"><span>正在下载 @anthropic-ai/claude-code</span><strong>68%</strong></div><div className="linear-progress" role="progressbar" aria-label="安装进度" aria-valuenow={68}><i /></div><pre>npm install -g @anthropic-ai/claude-code{`\n`}正在解析依赖…{`\n`}正在下载软件包…</pre></> : <div className="console-empty">开始安装后，实时输出将显示在这里。</div>}</section>
    </main>
  )
}
