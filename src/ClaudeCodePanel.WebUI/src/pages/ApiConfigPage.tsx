import { useState } from 'react'
import { Check, CloudCog, Eye, EyeOff, KeyRound, Save, WandSparkles } from 'lucide-react'

export function ApiConfigPage() {
  const [provider, setProvider] = useState('Anthropic')
  const [apiKey, setApiKey] = useState('')
  const [showKey, setShowKey] = useState(false)
  const [saved, setSaved] = useState(false)
  const providers = ['Anthropic', 'OpenAI', 'DeepSeek', 'Custom']

  return (
    <main className="workspace-page" aria-labelledby="api-title">
      <div className="page-heading"><div><span className="eyebrow">API WORKSPACE</span><h1 id="api-title">提供商与凭据</h1><p>配置模型提供商、端点和安全凭据。</p></div><button className="primary-action" type="button" onClick={() => setSaved(true)}><Save size={16} />保存配置</button></div>
      <div className="workspace-columns">
        <section className="work-surface clear-surface">
          <div className="section-heading"><div><span className="eyebrow">PROVIDER</span><h2>选择提供商</h2></div><CloudCog size={20} /></div>
          <div className="segmented" aria-label="API 提供商">
            {providers.map((item) => <button key={item} type="button" aria-pressed={provider === item} onClick={() => setProvider(item)}>{item}</button>)}
          </div>
          <div className="form-grid">
            <label className="field"><span>API 密钥</span><div className="secret-input"><KeyRound size={16} /><input aria-label="API 密钥" type={showKey ? 'text' : 'password'} value={apiKey} placeholder="安全存储在 Windows 凭据管理器" onChange={(event) => { setApiKey(event.target.value); setSaved(false) }} /><button type="button" aria-label={showKey ? '隐藏密钥' : '显示密钥'} onClick={() => setShowKey((value) => !value)}>{showKey ? <EyeOff size={16} /> : <Eye size={16} />}</button></div></label>
            <label className="field"><span>API 端点</span><input defaultValue={provider === 'Anthropic' ? 'https://api.anthropic.com' : 'https://api.openai.com/v1'} /></label>
            <label className="field"><span>最大令牌</span><input type="number" defaultValue="8192" /></label>
            <label className="field"><span>超时（秒）</span><input type="number" defaultValue="60" /></label>
          </div>
          {saved && <div className="inline-success"><Check size={15} />配置已保存</div>}
        </section>
        <aside className="context-surface">
          <div className="section-heading"><div><span className="eyebrow">CONNECTION</span><h2>连接状态</h2></div><span className="status-dot is-ok" /></div>
          <div className="connection-summary"><strong>{provider}</strong><span>凭据由主机安全管理</span><em><Check size={13} />连接就绪</em></div>
          <button className="secondary-action" type="button"><CloudCog size={16} />测试连接</button>
          <button className="secondary-action" type="button"><WandSparkles size={16} />检测可用模型</button>
          <div className="model-list"><small>已启用模型</small><button type="button">claude-sonnet-4-6 <Check size={13} /></button><button type="button">claude-haiku-4-5 <Check size={13} /></button></div>
        </aside>
      </div>
    </main>
  )
}
