import { useState } from 'react';
import { useVps } from '../hooks/useApi';
import { Server, Wifi, WifiOff, CheckCircle, XCircle, AlertTriangle, Save, TestTube } from 'lucide-react';

function VpsRemote() {
  const { vps, loading, testing, error, save, test } = useVps();
  const [form, setForm] = useState({
    host: '',
    sshPort: 22,
    rustDeskRelayServer: '',
    rustDeskIdServer: '',
    rustDeskRelayPort: 21117,
    rustDeskIdPort: 21116,
    rustDeskApiPort: 21118,
  });
  const [initialized, setInitialized] = useState(false);
  const [saveMsg, setSaveMsg] = useState<string | null>(null);

  if (loading && !vps) {
    return <div className="loading-overlay"><div className="spinner" /> Loading VPS settings...</div>;
  }

  if (vps && !initialized) {
    setForm({
      host: vps.host || '',
      sshPort: vps.sshPort || 22,
      rustDeskRelayServer: vps.rustDeskRelayServer || '',
      rustDeskIdServer: vps.rustDeskIdServer || '',
      rustDeskRelayPort: vps.rustDeskRelayPort || 21117,
      rustDeskIdPort: vps.rustDeskIdPort || 21116,
      rustDeskApiPort: vps.rustDeskApiPort || 21118,
    });
    setInitialized(true);
  }

  const handleSave = async () => {
    const ok = await save(form);
    setSaveMsg(ok ? 'Configuration saved' : 'Failed to save');
    setTimeout(() => setSaveMsg(null), 3000);
  };

  const handleTest = async () => {
    await test();
  };

  return (
    <div>
      <div className="page-header">
        <h2>VPS / Remote Access</h2>
        <p>Configure self-hosted RustDesk server and VPS connectivity</p>
      </div>

      {error && <div className="alert alert-error"><AlertTriangle size={16} /> {error}</div>}
      {saveMsg && <div className="alert alert-success"><CheckCircle size={16} /> {saveMsg}</div>}

      <div className="grid-2">
        <div className="card">
          <div className="card-header">
            <h3>VPS Configuration</h3>
          </div>

          <div className="form-group">
            <label>VPS Host / IP</label>
            <input
              value={form.host}
              onChange={e => setForm({ ...form, host: e.target.value })}
              placeholder="e.g., 69.197.142.77"
            />
          </div>

          <div className="form-group">
            <label>SSH Port</label>
            <input
              type="number"
              value={form.sshPort}
              onChange={e => setForm({ ...form, sshPort: parseInt(e.target.value) || 22 })}
            />
          </div>

          <div className="form-group">
            <label>RustDesk Relay Server (leave blank to use VPS host)</label>
            <input
              value={form.rustDeskRelayServer}
              onChange={e => setForm({ ...form, rustDeskRelayServer: e.target.value })}
              placeholder={form.host || 'Same as VPS host'}
            />
          </div>

          <div className="form-group">
            <label>RustDesk ID Server (leave blank to use VPS host)</label>
            <input
              value={form.rustDeskIdServer}
              onChange={e => setForm({ ...form, rustDeskIdServer: e.target.value })}
              placeholder={form.host || 'Same as VPS host'}
            />
          </div>

          <div className="grid-3">
            <div className="form-group">
              <label>Relay Port</label>
              <input
                type="number"
                value={form.rustDeskRelayPort}
                onChange={e => setForm({ ...form, rustDeskRelayPort: parseInt(e.target.value) || 21117 })}
              />
            </div>
            <div className="form-group">
              <label>ID Server Port</label>
              <input
                type="number"
                value={form.rustDeskIdPort}
                onChange={e => setForm({ ...form, rustDeskIdPort: parseInt(e.target.value) || 21116 })}
              />
            </div>
            <div className="form-group">
              <label>API Port</label>
              <input
                type="number"
                value={form.rustDeskApiPort}
                onChange={e => setForm({ ...form, rustDeskApiPort: parseInt(e.target.value) || 21118 })}
              />
            </div>
          </div>

          <div className="actions-row" style={{ marginTop: 8 }}>
            <button className="btn btn-primary" onClick={handleSave}>
              <Save size={14} /> Save Configuration
            </button>
            <button className="btn btn-ghost" onClick={handleTest} disabled={testing || !form.host}>
              {testing ? <div className="spinner" /> : <TestTube size={14} />}
              {testing ? 'Testing...' : 'Test Connection'}
            </button>
          </div>
        </div>

        <div className="card">
          <div className="card-header">
            <h3>Connectivity Status</h3>
          </div>

          {!vps?.isConfigured ? (
            <div className="empty-state">
              <Server size={48} />
              <h3>VPS Not Configured</h3>
              <p>Enter your VPS details and click Save</p>
            </div>
          ) : (
            <div>
              <table>
                <tbody>
                  <tr>
                    <td style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <Server size={16} /> VPS Host
                    </td>
                    <td>
                      <span className={`badge ${vps.isReachable ? 'online' : 'offline'}`}>
                        {vps.isReachable ? <Wifi size={12} /> : <WifiOff size={12} />}
                        {vps.isReachable ? 'Reachable' : 'Unreachable'}
                      </span>
                    </td>
                  </tr>
                  <tr>
                    <td style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <Server size={16} /> RustDesk Relay
                    </td>
                    <td>
                      <span className={`badge ${vps.isRelayReachable ? 'online' : 'offline'}`}>
                        {vps.isRelayReachable ? <CheckCircle size={12} /> : <XCircle size={12} />}
                        Port {form.rustDeskRelayPort} {vps.isRelayReachable ? 'Open' : 'Closed'}
                      </span>
                    </td>
                  </tr>
                  <tr>
                    <td style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <Server size={16} /> RustDesk ID Server
                    </td>
                    <td>
                      <span className={`badge ${vps.isIdServerReachable ? 'online' : 'offline'}`}>
                        {vps.isIdServerReachable ? <CheckCircle size={12} /> : <XCircle size={12} />}
                        Port {form.rustDeskIdPort} {vps.isIdServerReachable ? 'Open' : 'Closed'}
                      </span>
                    </td>
                  </tr>
                </tbody>
              </table>

              {vps.lastTestedAt && (
                <p style={{ color: 'var(--text-muted)', fontSize: 12, marginTop: 12 }}>
                  Last tested: {new Date(vps.lastTestedAt).toLocaleString()}
                </p>
              )}
            </div>
          )}

          <div style={{ marginTop: 24, padding: 16, background: 'var(--bg-surface)', borderRadius: 8 }}>
            <h4 style={{ fontSize: 14, marginBottom: 8 }}>RustDesk Client Setup</h4>
            <ol style={{ fontSize: 13, color: 'var(--text-secondary)', paddingLeft: 20, lineHeight: 1.8 }}>
              <li>Install RustDesk from <a href="https://github.com/rustdesk/rustdesk/releases" target="_blank" rel="noreferrer">github.com/rustdesk/rustdesk</a></li>
              <li>Open RustDesk &rarr; Settings &rarr; Network</li>
              <li>Set <strong>ID Server</strong>: <code>{form.host || '<your-vps-ip>'}</code></li>
              <li>Set <strong>Relay Server</strong>: <code>{form.host || '<your-vps-ip>'}</code></li>
              <li>Save &amp; restart RustDesk</li>
            </ol>
          </div>
        </div>
      </div>
    </div>
  );
}

export default VpsRemote;
