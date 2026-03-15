import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDevices, deviceActions, sessionActions, virtualDeviceActions } from '../hooks/useApi';
import { DeviceConnectionState } from '../types';
import {
  Smartphone, RefreshCw, Monitor, RotateCcw, Camera, Package,
  Upload, Download, Power, Terminal, Copy, Search, AlertTriangle, Edit2, Eye, Cloud, CloudOff
} from 'lucide-react';

const stateLabel = (s: DeviceConnectionState) =>
  ['Unknown', 'Online', 'Offline', 'Unauthorized', 'Disconnected', 'Recovery', 'Sideload', 'No Perms'][s] || 'Unknown';

const stateBadge = (s: DeviceConnectionState) => {
  if (s === DeviceConnectionState.Online) return 'online';
  if (s === DeviceConnectionState.Offline || s === DeviceConnectionState.Disconnected) return 'offline';
  if (s === DeviceConnectionState.Unauthorized) return 'warning';
  return 'info';
};

function BatteryIndicator({ level }: { level: number }) {
  if (level < 0) return <span style={{ color: 'var(--text-muted)' }}>N/A</span>;
  const cls = level > 50 ? 'high' : level > 20 ? 'medium' : 'low';
  return (
    <span className="battery">
      <span className="battery-bar">
        <span className={`battery-fill ${cls}`} style={{ width: `${level}%` }} />
      </span>
      {level}%
    </span>
  );
}

function Devices() {
  const navigate = useNavigate();
  const { devices, loading, error, refreshDevices } = useDevices();
  const [search, setSearch] = useState('');
  const [actionMsg, setActionMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [editingName, setEditingName] = useState<string | null>(null);
  const [newName, setNewName] = useState('');
  const [connectingCloud, setConnectingCloud] = useState(false);

  const filtered = devices.filter(d => {
    const q = search.toLowerCase();
    return !q || d.serialNumber.toLowerCase().includes(q)
      || d.model.toLowerCase().includes(q)
      || d.manufacturer.toLowerCase().includes(q)
      || d.friendlyName.toLowerCase().includes(q);
  });

  const doAction = async (label: string, fn: () => Promise<unknown>) => {
    try {
      setActionMsg(null);
      await fn();
      setActionMsg({ type: 'success', text: `${label} successful` });
      setTimeout(() => setActionMsg(null), 3000);
    } catch (e) {
      setActionMsg({ type: 'error', text: `${label} failed: ${(e as Error).message}` });
    }
  };

  const saveName = async (serial: string) => {
    await doAction('Rename', () => deviceActions.setName(serial, newName));
    setEditingName(null);
  };

  if (loading && devices.length === 0) {
    return <div className="loading-overlay"><div className="spinner" /> Loading devices...</div>;
  }

  return (
    <div>
      <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h2>Devices</h2>
          <p>{devices.length} device(s) detected</p>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-primary" onClick={async () => {
            setConnectingCloud(true);
            try {
              await virtualDeviceActions.connectAll();
              await refreshDevices();
              setActionMsg({ type: 'success', text: 'Cloud devices connected' });
              setTimeout(() => setActionMsg(null), 3000);
            } catch (e) {
              setActionMsg({ type: 'error', text: `Cloud connect failed: ${(e as Error).message}` });
            } finally {
              setConnectingCloud(false);
            }
          }} disabled={connectingCloud}>
            <Cloud size={14} /> {connectingCloud ? 'Connecting...' : 'Connect Cloud Devices'}
          </button>
          <button className="btn btn-primary" onClick={refreshDevices}>
            <RefreshCw size={14} /> Rescan
          </button>
        </div>
      </div>

      {error && <div className="alert alert-error"><AlertTriangle size={16} /> {error}</div>}
      {actionMsg && <div className={`alert alert-${actionMsg.type}`}>{actionMsg.text}</div>}

      <div className="filters-bar">
        <div style={{ position: 'relative', flex: 1, maxWidth: 400 }}>
          <Search size={16} style={{ position: 'absolute', left: 12, top: 10, color: 'var(--text-muted)' }} />
          <input
            placeholder="Search devices..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            style={{ paddingLeft: 36, width: '100%' }}
          />
        </div>
      </div>

      {filtered.length === 0 ? (
        <div className="empty-state">
          <Smartphone size={48} />
          <h3>No devices found</h3>
          <p>Connect Android phones via USB and enable USB Debugging</p>
        </div>
      ) : (
        <div className="device-grid">
          {filtered.map(device => (
            <div key={device.serialNumber} className="device-card">
              <div className="device-card-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  {device.isVirtual
                    ? <Cloud size={18} style={{ color: '#a6e3a1' }} />
                    : <Smartphone size={18} style={{ color: 'var(--accent)' }} />
                  }
                  {editingName === device.serialNumber ? (
                    <div style={{ display: 'flex', gap: 4 }}>
                      <input
                        value={newName}
                        onChange={e => setNewName(e.target.value)}
                        onKeyDown={e => e.key === 'Enter' && saveName(device.serialNumber)}
                        style={{ width: 120, padding: '4px 8px', fontSize: 13 }}
                        autoFocus
                      />
                      <button className="btn btn-sm btn-primary" onClick={() => saveName(device.serialNumber)}>Save</button>
                      <button className="btn btn-sm btn-ghost" onClick={() => setEditingName(null)}>Cancel</button>
                    </div>
                  ) : (
                    <h4>
                      {device.displayName}
                      <button
                        className="btn-icon"
                        style={{ marginLeft: 6, width: 24, height: 24, border: 'none' }}
                        onClick={() => { setEditingName(device.serialNumber); setNewName(device.friendlyName || ''); }}
                        title="Edit name"
                      >
                        <Edit2 size={12} />
                      </button>
                    </h4>
                  )}
                </div>
                <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                  {device.isVirtual && (
                    <span className="badge" style={{ background: '#1e4620', color: '#a6e3a1', fontSize: 10 }}>
                      Cloud
                    </span>
                  )}
                  <span className={`badge ${stateBadge(device.connectionState)}`}>
                    <span className="badge-dot" />
                    {stateLabel(device.connectionState)}
                  </span>
                </div>
              </div>

              <dl className="device-card-info">
                <dt>Serial</dt>
                <dd style={{ fontFamily: 'monospace', fontSize: 12 }}>{device.serialNumber}</dd>
                <dt>Model</dt>
                <dd>{device.model || 'Unknown'}</dd>
                <dt>Manufacturer</dt>
                <dd>{device.manufacturer || 'Unknown'}</dd>
                <dt>Android</dt>
                <dd>{device.androidVersion || 'N/A'}</dd>
                <dt>Connection</dt>
                <dd>{device.connectionType}</dd>
                <dt>Battery</dt>
                <dd><BatteryIndicator level={device.batteryLevel} /></dd>
                <dt>Screen</dt>
                <dd>{device.isScreenOn === null ? 'N/A' : device.isScreenOn ? 'On' : 'Off'}</dd>
                <dt>Session</dt>
                <dd>
                  <span className={`badge ${device.hasActiveSession ? 'running' : 'stopped'}`}>
                    {device.hasActiveSession ? 'Active' : 'None'}
                  </span>
                </dd>
              </dl>

              <div className="device-card-actions">
                <button className="btn btn-sm btn-primary"
                  onClick={() => navigate(`/devices/${device.serialNumber}/screen`)}
                  title="View and control phone screen in browser">
                  <Eye size={12} /> View Screen
                </button>
                {!device.hasActiveSession ? (
                  <button className="btn btn-sm btn-success"
                    onClick={() => doAction('Start scrcpy', () => sessionActions.start(device.serialNumber))}>
                    <Monitor size={12} /> scrcpy
                  </button>
                ) : (
                  <button className="btn btn-sm btn-danger"
                    onClick={() => doAction('Stop scrcpy', () => sessionActions.stop(device.serialNumber))}>
                    <Monitor size={12} /> Stop
                  </button>
                )}
                <button className="btn btn-sm btn-ghost"
                  onClick={() => doAction('Reconnect', () => deviceActions.reconnect(device.serialNumber))}>
                  <RotateCcw size={12} /> Reconnect
                </button>
                <button className="btn btn-sm btn-ghost"
                  onClick={() => doAction('Screenshot', () => deviceActions.screenshot(device.serialNumber))}>
                  <Camera size={12} /> Screenshot
                </button>
                <button className="btn btn-sm btn-ghost"
                  onClick={() => doAction('Reboot', () => deviceActions.reboot(device.serialNumber))}>
                  <Power size={12} /> Reboot
                </button>
                <button className="btn btn-sm btn-ghost"
                  onClick={() => { navigator.clipboard.writeText(`${device.serialNumber} | ${device.model} | ${device.manufacturer}`); setActionMsg({ type: 'success', text: 'Copied to clipboard' }); setTimeout(() => setActionMsg(null), 2000); }}>
                  <Copy size={12} /> Copy
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default Devices;
