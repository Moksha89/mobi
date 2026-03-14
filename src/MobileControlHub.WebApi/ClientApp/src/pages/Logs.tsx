import { useState } from 'react';
import { useLogs } from '../hooks/useApi';
import { AppLogLevel } from '../types';
import { ScrollText, RefreshCw, Trash2, AlertTriangle, Filter } from 'lucide-react';

const levelName = (l: AppLogLevel) => ['DEBUG', 'INFO', 'WARN', 'ERROR', 'CRIT'][l] || 'INFO';
const levelClass = (l: AppLogLevel) => ['debug', 'info', 'warning', 'error', 'critical'][l] || 'info';

function Logs() {
  const { logs, loading, error, fetchLogs, fetchRecent } = useLogs();
  const [levelFilter, setLevelFilter] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [deviceFilter, setDeviceFilter] = useState('');
  const [limit, setLimit] = useState(200);

  const applyFilters = () => {
    fetchLogs({
      level: levelFilter || undefined,
      category: categoryFilter || undefined,
      deviceSerial: deviceFilter || undefined,
      limit,
    });
  };

  const clearFilters = () => {
    setLevelFilter('');
    setCategoryFilter('');
    setDeviceFilter('');
    setLimit(200);
    fetchRecent(200);
  };

  return (
    <div>
      <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h2>Logs</h2>
          <p>{logs.length} entries loaded</p>
        </div>
        <div className="actions-row">
          <button className="btn btn-ghost" onClick={() => fetchRecent(200)}>
            <RefreshCw size={14} /> Refresh
          </button>
        </div>
      </div>

      {error && <div className="alert alert-error"><AlertTriangle size={16} /> {error}</div>}

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="filters-bar">
          <select value={levelFilter} onChange={e => setLevelFilter(e.target.value)}>
            <option value="">All Levels</option>
            <option value="Debug">Debug</option>
            <option value="Info">Info</option>
            <option value="Warning">Warning</option>
            <option value="Error">Error</option>
            <option value="Critical">Critical</option>
          </select>
          <input
            placeholder="Category..."
            value={categoryFilter}
            onChange={e => setCategoryFilter(e.target.value)}
            style={{ minWidth: 120 }}
          />
          <input
            placeholder="Device serial..."
            value={deviceFilter}
            onChange={e => setDeviceFilter(e.target.value)}
            style={{ minWidth: 150 }}
          />
          <select value={limit} onChange={e => setLimit(parseInt(e.target.value))}>
            <option value={50}>50 entries</option>
            <option value={100}>100 entries</option>
            <option value={200}>200 entries</option>
            <option value={500}>500 entries</option>
          </select>
          <button className="btn btn-primary btn-sm" onClick={applyFilters}>
            <Filter size={12} /> Apply
          </button>
          <button className="btn btn-ghost btn-sm" onClick={clearFilters}>
            Clear
          </button>
        </div>
      </div>

      <div className="card">
        {loading ? (
          <div className="loading-overlay"><div className="spinner" /> Loading logs...</div>
        ) : logs.length === 0 ? (
          <div className="empty-state">
            <ScrollText size={48} />
            <h3>No log entries</h3>
            <p>Log entries will appear here as the app runs</p>
          </div>
        ) : (
          <div style={{ maxHeight: 'calc(100vh - 300px)', overflowY: 'auto' }}>
            {logs.map(log => (
              <div key={log.id} className="log-entry">
                <span className="log-time">
                  {new Date(log.timestamp).toLocaleString()}
                </span>
                <span className={`log-level ${levelClass(log.level)}`}>
                  {levelName(log.level)}
                </span>
                <span className="log-category">[{log.category}]</span>
                {log.deviceSerial && (
                  <span style={{ color: 'var(--orange)', marginRight: 8 }}>
                    {log.deviceSerial}
                  </span>
                )}
                <span>{log.message}</span>
                {log.details && (
                  <div style={{ marginTop: 4, paddingLeft: 20, color: 'var(--text-muted)', fontSize: 12 }}>
                    {log.details}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

export default Logs;
