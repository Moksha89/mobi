import { useStatus, useLogs } from '../hooks/useApi';
import { AppLogLevel } from '../types';
import {
  Smartphone, Monitor, Server, Wifi, WifiOff, Activity,
  CheckCircle, XCircle, AlertTriangle, RefreshCw
} from 'lucide-react';

const levelName = (l: AppLogLevel) => ['DEBUG', 'INFO', 'WARN', 'ERROR', 'CRIT'][l] || 'INFO';
const levelClass = (l: AppLogLevel) => ['debug', 'info', 'warning', 'error', 'critical'][l] || 'info';

function Dashboard() {
  const { status, loading, error, refresh } = useStatus();
  const { logs } = useLogs();

  if (loading && !status) {
    return <div className="loading-overlay"><div className="spinner" /> Loading dashboard...</div>;
  }

  return (
    <div>
      <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h2>Dashboard</h2>
          <p>System health overview</p>
        </div>
        <button className="btn btn-ghost" onClick={refresh}>
          <RefreshCw size={14} /> Refresh
        </button>
      </div>

      {error && <div className="alert alert-error"><AlertTriangle size={16} /> {error}</div>}

      <div className="stats-grid">
        <div className="stat-card">
          <div className={`stat-icon ${status?.adbAvailable ? 'green' : 'red'}`}>
            {status?.adbAvailable ? <CheckCircle size={24} /> : <XCircle size={24} />}
          </div>
          <div className="stat-info">
            <h4>{status?.adbAvailable ? 'Available' : 'Unavailable'}</h4>
            <p>ADB {status?.adbVersion ? `v${status.adbVersion.substring(0, 20)}` : 'Status'}</p>
          </div>
        </div>

        <div className="stat-card">
          <div className={`stat-icon ${status?.scrcpyAvailable ? 'green' : 'red'}`}>
            <Monitor size={24} />
          </div>
          <div className="stat-info">
            <h4>{status?.scrcpyAvailable ? 'Available' : 'Unavailable'}</h4>
            <p>scrcpy {status?.scrcpyVersion ? `v${status.scrcpyVersion.substring(0, 20)}` : 'Status'}</p>
          </div>
        </div>

        <div className="stat-card">
          <div className={`stat-icon blue`}>
            <Smartphone size={24} />
          </div>
          <div className="stat-info">
            <h4>{status?.connectedDevices ?? 0}</h4>
            <p>Connected Devices</p>
          </div>
        </div>

        <div className="stat-card">
          <div className={`stat-icon purple`}>
            <Monitor size={24} />
          </div>
          <div className="stat-info">
            <h4>{status?.activeSessions ?? 0}</h4>
            <p>Active Sessions</p>
          </div>
        </div>

        <div className="stat-card">
          <div className={`stat-icon ${status?.vpsReachable ? 'green' : status?.vpsConfigured ? 'yellow' : 'red'}`}>
            <Server size={24} />
          </div>
          <div className="stat-info">
            <h4>{status?.vpsReachable ? 'Connected' : status?.vpsConfigured ? 'Configured' : 'Not Set'}</h4>
            <p>VPS Status</p>
          </div>
        </div>

        <div className="stat-card">
          <div className={`stat-icon ${status?.monitorRunning ? 'green' : 'yellow'}`}>
            <Activity size={24} />
          </div>
          <div className="stat-info">
            <h4>{status?.monitorRunning ? 'Running' : 'Stopped'}</h4>
            <p>Device Monitor</p>
          </div>
        </div>
      </div>

      <div className="grid-2">
        <div className="card">
          <div className="card-header">
            <h3>Service Health</h3>
          </div>
          <table>
            <tbody>
              <tr>
                <td>ADB</td>
                <td>
                  <span className={`badge ${status?.adbAvailable ? 'online' : 'offline'}`}>
                    <span className="badge-dot" />
                    {status?.adbAvailable ? 'Online' : 'Offline'}
                  </span>
                </td>
              </tr>
              <tr>
                <td>scrcpy</td>
                <td>
                  <span className={`badge ${status?.scrcpyAvailable ? 'online' : 'offline'}`}>
                    <span className="badge-dot" />
                    {status?.scrcpyAvailable ? 'Online' : 'Offline'}
                  </span>
                </td>
              </tr>
              <tr>
                <td>RustDesk</td>
                <td>
                  <span className={`badge ${status?.rustDeskRunning ? 'online' : status?.rustDeskInstalled ? 'warning' : 'offline'}`}>
                    <span className="badge-dot" />
                    {status?.rustDeskRunning ? 'Running' : status?.rustDeskInstalled ? 'Installed' : 'Not Found'}
                  </span>
                </td>
              </tr>
              <tr>
                <td>VPS</td>
                <td>
                  <span className={`badge ${status?.vpsReachable ? 'online' : 'offline'}`}>
                    {status?.vpsReachable ? <Wifi size={12} /> : <WifiOff size={12} />}
                    {status?.vpsReachable ? 'Reachable' : 'Unreachable'}
                  </span>
                </td>
              </tr>
              <tr>
                <td>Monitor</td>
                <td>
                  <span className={`badge ${status?.monitorRunning ? 'running' : 'stopped'}`}>
                    <span className="badge-dot" />
                    {status?.monitorRunning ? 'Active' : 'Inactive'}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div className="card">
          <div className="card-header">
            <h3>Recent Logs</h3>
          </div>
          <div style={{ maxHeight: 300, overflowY: 'auto' }}>
            {logs.length === 0 ? (
              <div className="empty-state">
                <p>No recent logs</p>
              </div>
            ) : (
              logs.slice(0, 20).map((log) => (
                <div key={log.id} className="log-entry">
                  <span className="log-time">
                    {new Date(log.timestamp).toLocaleTimeString()}
                  </span>
                  <span className={`log-level ${levelClass(log.level)}`}>
                    {levelName(log.level)}
                  </span>
                  <span className="log-category">[{log.category}]</span>
                  {log.message}
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export default Dashboard;
