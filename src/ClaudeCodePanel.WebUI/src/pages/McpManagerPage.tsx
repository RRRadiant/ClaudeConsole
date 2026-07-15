import { useMemo, useState } from 'react'
import { Check, Plus, Search, ServerCog, X } from 'lucide-react'

const initialServers = [
  { name: 'filesystem', detail: 'npx -y @modelcontextprotocol/server-filesystem', status: '已连接' },
  { name: 'github', detail: 'npx -y @modelcontextprotocol/server-github', status: '已连接' },
  { name: 'weather-mcp', detail: 'http://localhost:3100/sse', status: '需检查' },
]

export function McpManagerPage() {
  const [query, setQuery] = useState('')
  const [editorOpen, setEditorOpen] = useState(false)
  const [selected, setSelected] = useState(initialServers[0])
  const servers = useMemo(() => initialServers.filter((server) => server.name.includes(query.toLowerCase())), [query])
  return (
    <main className="workspace-page" aria-labelledby="mcp-title">
      <div className="page-heading"><div><span className="eyebrow">MCP WORKSPACE</span><h1 id="mcp-title">服务与连接</h1><p>管理工具服务、启动参数与连接健康。</p></div><button className="primary-action" type="button" onClick={() => setEditorOpen(true)}><Plus size={16} />添加服务</button></div>
      <div className="workspace-columns">
        <section className="work-surface list-surface">
          <label className="list-search"><Search size={16} /><input aria-label="搜索 MCP 服务" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="搜索服务" /></label>
          <div className="data-list">
            {servers.map((server) => <button key={server.name} type="button" className={selected.name === server.name ? 'is-selected' : ''} onClick={() => setSelected(server)}><span className="row-icon"><ServerCog size={18} /></span><span><strong>{server.name}</strong><small>{server.detail}</small></span><em className={server.status === '已连接' ? 'is-ok' : 'is-warning'}>{server.status === '已连接' && <Check size={12} />}{server.status}</em></button>)}
          </div>
        </section>
        <aside className="context-surface"><span className="eyebrow">DETAILS</span><h2>{selected.name}</h2><p className="context-copy">{selected.detail}</p><dl><div><dt>类型</dt><dd>{selected.detail.startsWith('http') ? 'SSE' : 'stdio'}</dd></div><div><dt>状态</dt><dd>{selected.status}</dd></div></dl><button className="secondary-action" type="button">测试连接</button><button className="secondary-action" type="button" onClick={() => setEditorOpen(true)}>编辑配置</button></aside>
      </div>
      {editorOpen && <div className="modal-backdrop"><section className="editor-dialog" role="dialog" aria-modal="true" aria-label="添加 MCP 服务"><header><div><span className="eyebrow">NEW SERVER</span><h2>添加 MCP 服务</h2></div><button type="button" aria-label="关闭" onClick={() => setEditorOpen(false)}><X size={18} /></button></header><label className="field"><span>名称</span><input placeholder="例如 filesystem" /></label><label className="field"><span>命令或 URL</span><input placeholder="npx ... 或 https://..." /></label><footer><button type="button" className="secondary-action" onClick={() => setEditorOpen(false)}>取消</button><button type="button" className="primary-action" onClick={() => setEditorOpen(false)}>保存服务</button></footer></section></div>}
    </main>
  )
}
