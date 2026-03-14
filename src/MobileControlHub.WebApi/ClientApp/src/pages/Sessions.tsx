import { useSessions, sessionActions } from '../hooks/useApi';
import { SessionState } from '../types';
import { Monitor, StopCircle, RotateCcw, Trash2, AlertTriangle, Clock } from 'lucide-react';

const stateLabel = (s: SessionState) => ['Stopped', 'Starting', 'Running', 'Error', 'Reconnecting'][s] || 'Unknown';
const stateBadge = (s: SessionState) => {
  if (s === SessionState.Running) return 'running';
  if (s === SessionState.Error) return 'error';
  if (s === SessionState.Starting || s === SessionState.Reconnecting) return 'warning';
  return 'stopped';
};

function formatDuration(start: string, end: string | null) {
  const s = new Date(start).getTime();
  const e = end ? new Date(end).getTime() : Date.now();
  const diff = Math.floor((e - s) / 1000);
  const h = Math.floor(diff / 3600);
  const m = Math.floor((diff % 3600) / 60);
  const sec = diff % 60;
  if (h > 0) return `${h}h ${m}m ${sec}s`;
  if (m > 0) return `${m}m ${sec}s`;
  return `${sec}s`;
}

function Sessions() {
  const { sessions, loading, error, refresh } = useSessions();

  const doAction = async (label: string, fn: () => Promise<unknown>) => {
    try {
      await fn();
      refresh();
    } catch (e) {
      console.error(`${label} failed:`, e);
    }
  };

  if (loading && sessions.length === 0) {
    return <div className="loading-overlay"><div className="spinner" /> Loading sessions...</div>;
  }

  return (
    <div>
      <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h2>Sessions</h2>
          <p>{sessions.filter(s => s.state === SessionState.Running).length} active scrcpy session(s)</p>
        </div>
        <div className="actions-row">
          <button className="btn btn-ghost" onClick={refresh}>
            <RotateCcw size={14} /> Refresh
          </button>
          {sessions.length > 0 && (
            <button className="btn btn-danger" onClick={() => doAction('Stop all', sessionActions.stopAll)}>
              <Trash2 size={14} /> Stop All
            </button>
          )}
        </div>
      </div>

      {error && <div className="alert alert-error"><AlertTriangle size={16} /> {error}</div>}

      {sessions.length === 0 ? (
        <div className="empty-state">
          <Monitor size={48} />
          <h3>No active sessions</h3>
          <p>Start a scrcpy session from the Devices page</p>
        </div>
      ) : (
        <div className="card">
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Device Serial</th>
                  <th>State</th>
                  <th>PID</th>
                  <th>Started</th>
                  <th>Duration</th>
                  <th>Auto-Restart</th>
                  <th>Restarts</th>
                  <th>Error</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {sessions.map(session => (
                  <tr key={session.sessionId}>
                    <td style={{ fontFamily: 'monospace', fontSize: 13 }}>{session.deviceSerial}</td>
                    <td>
                      <span className={`badge ${stateBadge(session.state)}`}>
                        <span className="badge-dot" />
                        {stateLabel(session.state)}
                      </span>
                    </td>
                    <td>{session.processId || '-'}</td>
                    <td>{new Date(session.startedAt).toLocaleTimeString()}</td>
                    <td>
                      <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                        <Clock size={12} />
                        {formatDuration(session.startedAt, session.endedAt)}
                      </span>
                    </td>
                    <td>{session.autoRestart ? 'Yes' : 'No'}</td>
                    <td>{session.restartCount}</td>
                    <td style={{ color: 'var(--red)', maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {session.lastError || '-'}
                    </td>
                    <td>
                      <div className="actions-row">
                        {session.state === SessionState.Running && (
                          <button className="btn btn-sm btn-danger"
                            onClick={() => doAction('Stop', () => sessionActions.stop(session.sessionId))}>
                            <StopCircle size={12} /> Stop
                          </button>
                        )}
                        {(session.state === SessionState.Stopped || session.state === SessionState.Error) && (
                          <button className="btn btn-sm btn-success"
                            onClick={() => doAction('Restart', () => sessionActions.restart(session.sessionId))}>
                            <RotateCcw size={12} /> Restart
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

export default Sessions;
