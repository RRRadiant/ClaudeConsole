import { createWebViewBridge } from './webViewBridge'

describe('createWebViewBridge', () => {
  it('returns preview bootstrap data when WebView2 is unavailable', async () => {
    const bridge = createWebViewBridge()

    const response = await bridge.request('app.ready')

    expect(response.ok).toBe(true)
    expect(response.data.dashboard.claudeVersion).toBeTruthy()
    expect(response.data.theme.mode).toBe('dark')
  })

  it('posts a request and resolves the matching host response', async () => {
    const posted: unknown[] = []
    const listeners: Array<(event: MessageEvent) => void> = []
    const webview = {
      postMessage: (message: unknown) => posted.push(message),
      addEventListener: (_type: 'message', listener: (event: MessageEvent) => void) => listeners.push(listener),
      removeEventListener: vi.fn(),
    }
    const bridge = createWebViewBridge(webview)

    const pending = bridge.request('theme.get')
    const request = posted[0] as { id: string; type: string }
    listeners[0](new MessageEvent('message', {
      data: { id: request.id, ok: true, data: { mode: 'light', isDark: false, accentColor: '#2563EB' } },
    }))

    await expect(pending).resolves.toMatchObject({
      ok: true,
      data: { mode: 'light' },
    })
    expect(request.type).toBe('theme.get')
  })
})
