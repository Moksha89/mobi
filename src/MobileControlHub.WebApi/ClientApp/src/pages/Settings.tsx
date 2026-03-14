import { useState, useEffect } from 'react';
import { useSettings } from '../hooks/useApi';
import { Settings as SettingsIcon, Save, AlertTriangle, CheckCircle, FolderOpen } from 'lucide-react';

function SettingsPage() {
  const { config, loading, error, save } = useSettings();
  const [form, setForm] = useState({
    adbPath: '',
    scrcpyPath: '',
    rustDeskPath: '',
    devicePollIntervalSeconds: 5,
    adbTimeoutSeconds: 10,
    autoReconnectDevices: true,
    autoRestartScrcpy: false,
    startMonitoringOnLaunch: true,
    defaultScrcpyArgs: '',
  });
  const [initialized, setInitialized] = useState(false);
  const [saveMsg, setSaveMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    if (config && !initialized) {
      setForm({
        adbPath: config.adbPath || '',
        scrcpyPath: config.scrcpyPath || '',
        rustDeskPath: config.rustDeskPath || '',
        devicePollIntervalSeconds: config.devicePollIntervalSeconds || 5,
        adbTimeoutSeconds: config.adbTimeoutSeconds || 10,
        autoReconnectDevices: config.autoReconnectDevices ?? true,
        autoRestartScrcpy: config.autoRestartScrcpy ?? false,
        startMonitoringOnLaunch: config.startMonitoringOnLaunch ?? true,
        defaultScrcpyArgs: config.defaultScrcpyArgs || '',
      });
      setInitialized(true);
    }
  }, [config, initialized]);

  const handleSave = async () => {
    const ok = await save(form);
    setSaveMsg(ok
      ? { type: 'success', text: 'Settings saved successfully' }
      : { type: 'error', text: 'Failed to save settings' }
    );
    setTimeout(() => setSaveMsg(null), 3000);
  };

  if (loading && !config) {
    return <div className="loading-overlay"><div className="spinner" /> Loading settings...</div>;
  }

  return (
    <div>
      <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h2>Settings</h2>
          <p>Application configuration</p>
        </div>
        <button className="btn btn-primary" onClick={handleSave}>
          <Save size={14} /> Save Settings
        </button>
      </div>

      {error && <div className="alert alert-error"><AlertTriangle size={16} /> {error}</div>}
      {saveMsg && <div className={`alert alert-${saveMsg.type}`}>
        {saveMsg.type === 'success' ? <CheckCircle size={16} /> : <AlertTriangle size={16} />}
        {saveMsg.text}
      </div>}

      <div className="grid-2">
        <div className="card">
          <div className="card-header">
            <h3>Tool Paths</h3>
          </div>

          <div className="form-group">
            <label><FolderOpen size={12} style={{ marginRight: 4 }} /> ADB Executable Path</label>
            <input
              value={form.adbPath}
              onChange={e => setForm({ ...form, adbPath: e.target.value })}
              placeholder="tools\adb.exe"
            />
          </div>

          <div className="form-group">
            <label><FolderOpen size={12} style={{ marginRight: 4 }} /> scrcpy Executable Path</label>
            <input
              value={form.scrcpyPath}
              onChange={e => setForm({ ...form, scrcpyPath: e.target.value })}
              placeholder="tools\scrcpy.exe"
            />
          </div>

          <div className="form-group">
            <label><FolderOpen size={12} style={{ marginRight: 4 }} /> RustDesk Executable Path</label>
            <input
              value={form.rustDeskPath}
              onChange={e => setForm({ ...form, rustDeskPath: e.target.value })}
              placeholder="C:\Program Files\RustDesk\rustdesk.exe"
            />
          </div>
        </div>

        <div className="card">
          <div className="card-header">
            <h3>Monitoring</h3>
          </div>

          <div className="form-group">
            <label>Device Poll Interval (seconds)</label>
            <input
              type="number"
              min={1}
              max={60}
              value={form.devicePollIntervalSeconds}
              onChange={e => setForm({ ...form, devicePollIntervalSeconds: parseInt(e.target.value) || 5 })}
            />
          </div>

          <div className="form-group">
            <label>ADB Timeout (seconds)</label>
            <input
              type="number"
              min={1}
              max={120}
              value={form.adbTimeoutSeconds}
              onChange={e => setForm({ ...form, adbTimeoutSeconds: parseInt(e.target.value) || 10 })}
            />
          </div>

          <div className="form-group">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={form.autoReconnectDevices}
                onChange={e => setForm({ ...form, autoReconnectDevices: e.target.checked })}
                style={{ width: 'auto' }}
              />
              Auto-reconnect disconnected devices
            </label>
          </div>

          <div className="form-group">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={form.autoRestartScrcpy}
                onChange={e => setForm({ ...form, autoRestartScrcpy: e.target.checked })}
                style={{ width: 'auto' }}
              />
              Auto-restart crashed scrcpy sessions
            </label>
          </div>

          <div className="form-group">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={form.startMonitoringOnLaunch}
                onChange={e => setForm({ ...form, startMonitoringOnLaunch: e.target.checked })}
                style={{ width: 'auto' }}
              />
              Start monitoring on app launch
            </label>
          </div>
        </div>

        <div className="card" style={{ gridColumn: '1 / -1' }}>
          <div className="card-header">
            <h3>scrcpy Settings</h3>
          </div>

          <div className="form-group">
            <label>Default scrcpy Arguments</label>
            <input
              value={form.defaultScrcpyArgs}
              onChange={e => setForm({ ...form, defaultScrcpyArgs: e.target.value })}
              placeholder="--max-size=1024 --max-fps=30"
            />
            <p style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 4 }}>
              These arguments are passed to scrcpy when starting a new session. Common options:
              --max-size=1024, --max-fps=30, --bit-rate=4M, --no-audio, --turn-screen-off
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

export default SettingsPage;
