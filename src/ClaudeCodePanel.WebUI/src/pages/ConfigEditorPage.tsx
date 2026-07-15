import { useState } from 'react'
import { AlertTriangle, Braces, FileJson, Save } from 'lucide-react'

const initialContent = '{\n  "env": {\n    "ANTHROPIC_MODEL": "claude-sonnet-4-6"\n  },\n  "permissions": {\n    "defaultMode": "acceptEdits"\n  }\n}'

export function ConfigEditorPage() {
  const [selectedFile, setSelectedFile] = useState('settings.json')
  const [content, setContent] = useState(initialContent)
  const [savedContent, setSavedContent] = useState(initialContent)
  const modified = content !== savedContent

  return (
    <main className="workspace-page" aria-labelledby="config-title">
      <div className="page-heading"><div><span className="eyebrow">CONFIGURATION</span><h1 id="config-title">配置文件</h1><p>浏览并安全编辑本机 Claude Code 配置。</p></div><div className="heading-actions">{modified && <span className="unsaved-badge">未保存</span>}<button className="primary-action" type="button" onClick={() => setSavedContent(content)} disabled={!modified}><Save size={16} />保存更改</button></div></div>
      <div className="editor-layout">
        <nav className="file-list" aria-label="配置文件列表">
          <span className="eyebrow">FILES</span>
          {['settings.json', 'claude.json', 'mcp.json', 'CLAUDE.md'].map((file) => <button key={file} type="button" className={selectedFile === file ? 'is-active' : ''} onClick={() => setSelectedFile(file)}><FileJson size={16} /><span>{file}</span><small>{file.endsWith('.json') ? 'JSON' : 'Markdown'}</small></button>)}
        </nav>
        <section className="code-surface">
          <header><span><Braces size={16} />{selectedFile}</span><small>UTF-8 · JSON</small></header>
          <label><span className="sr-only">配置内容</span><textarea aria-label="配置内容" spellCheck={false} value={content} onChange={(event) => setContent(event.target.value)} /></label>
        </section>
        <aside className="context-surface file-context">
          <span className="eyebrow">FILE INFO</span><h2>文件状态</h2>
          <dl><div><dt>路径</dt><dd>~/.claude/{selectedFile}</dd></div><div><dt>格式</dt><dd>{selectedFile.endsWith('.json') ? 'JSON' : 'Markdown'}</dd></div><div><dt>状态</dt><dd>{modified ? '有未保存更改' : '已同步'}</dd></div></dl>
          <div className="info-callout"><AlertTriangle size={16} /><span>保存前会验证 JSON，并检查外部文件冲突。</span></div>
        </aside>
      </div>
    </main>
  )
}
