import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, RefreshCw, Loader2, Phone, MessageSquare, Cpu, HardDrive, Server } from 'lucide-react';
import { cloudPlatformActions } from '../hooks/useApi';
import type { CloudDevice } from '../types';

function CloudDeviceScreen() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const navigate = useNavigate();
  const iframeRef = useRef<HTMLIFrameElement>(null);

  const [device, setDevice] = useState<CloudDevice | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [streamUrl, setStreamUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!deviceId) return;
    setLoading(true);
    cloudPlatformActions.getDevice(deviceId)
      .then(dev => {
        setDevice(dev);
        if (dev.state !== 'Running') {
          setError(`Device is ${dev.state}. It must be Running to view the screen.`);
        } else if (dev.streamUrl) {
          setStreamUrl(dev.streamUrl);
        } else {
          cloudPlatformActions.getStreamUrl(deviceId)
            .then(r => setStreamUrl(r.streamUrl))
            .catch(() => setError('Could not get stream URL'));
        }
      })
      .catch(e => setError((e as Error).message))
      .finally(() => setLoading(false));
  }, [deviceId]);

  if (!deviceId) return <div className="loading-overlay">No device selected</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100vh', background: '#1a1a1a' }}>
      {/* Top bar */}
      <div style={{
        display: 'flex', justifyContent: 'space-between', alignItems: 'center',
        padding: '8px 16px', background: 'var(--surface)', borderBottom: '1px solid var(--border)',
        flexShrink: 0,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <button className="btn btn-ghost btn-sm" onClick={() => navigate('/devices')}>
            <ArrowLeft size={14} /> Back
          </button>
          <h3 style={{ fontSize: 16, fontWeight: 600, margin: 0 }}>
            {device?.name || 'K8s Cloud Device'}
          </h3>
          {device && (
            <>
              <span className="badge info" style={{ fontSize: 11 }}>
                {device.brand} {device.model}
              </span>
              <span className="badge info" style={{ fontSize: 11 }}>
                Android {device.androidVersion}
              </span>
              <span className={`badge ${device.state === 'Running' ? 'online' : 'warning'}`} style={{ fontSize: 11 }}>
                <span className="badge-dot" />
                {device.state}
              </span>
              {device.gpuAccelerated && (
                <span className="badge info" style={{ fontSize: 11, background: 'rgba(166,227,161,0.15)', color: '#a6e3a1' }}>
                  GPU
                </span>
              )}
            </>
          )}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {device?.phoneNumber && (
            <span style={{ fontSize: 12, color: '#a6e3a1', display: 'flex', alignItems: 'center', gap: 4 }}>
              <Phone size={12} /> {device.phoneNumber}
            </span>
          )}
          <button
            className="btn btn-sm btn-ghost"
            onClick={() => {
              if (iframeRef.current && streamUrl) {
                iframeRef.current.src = streamUrl;
              }
            }}
            title="Reconnect"
          >
            <RefreshCw size={14} />
          </button>
        </div>
      </div>

      {/* Device info bar */}
      {device && (
        <div style={{
          display: 'flex', gap: 16, padding: '6px 16px', background: 'rgba(0,0,0,0.3)',
          borderBottom: '1px solid var(--border)', fontSize: 11, color: 'var(--text-muted)', flexShrink: 0, flexWrap: 'wrap',
        }}>
          <span><Cpu size={11} style={{ verticalAlign: 'middle' }} /> {device.cpuCores} cores / {device.ramMb >= 1024 ? `${(device.ramMb / 1024).toFixed(0)} GB` : `${device.ramMb} MB`} RAM</span>
          <span><HardDrive size={11} style={{ verticalAlign: 'middle' }} /> {device.screenWidth}x{device.screenHeight} @ {device.screenDpi}dpi</span>
          <span><Server size={11} style={{ verticalAlign: 'middle' }} /> {device.storageGb} GB {device.persistentStorage ? '(persistent)' : ''}</span>
          {device.hasGapps && <span style={{ color: '#a6e3a1' }}>GApps installed</span>}
          {device.phoneNumber && <span><MessageSquare size={11} style={{ verticalAlign: 'middle' }} /> SMS/Voice enabled</span>}
        </div>
      )}

      {/* Error banner */}
      {error && (
        <div className="alert alert-warning" style={{ margin: '8px 16px', flexShrink: 0 }}>
          {error}
        </div>
      )}

      {/* Loading state */}
      {loading && (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', flex: 1, color: 'var(--text-muted)' }}>
          <Loader2 size={32} style={{ animation: 'spin 1s linear infinite' }} />
          <span style={{ marginLeft: 12 }}>Loading device...</span>
        </div>
      )}

      {/* Screen viewer via scrcpy-web iframe */}
      {!loading && streamUrl && (
        <iframe
          ref={iframeRef}
          src={streamUrl}
          style={{
            flex: 1,
            border: 'none',
            width: '100%',
            minHeight: 0,
            background: '#000',
          }}
          allow="clipboard-read; clipboard-write; autoplay"
          title="K8s Device Screen"
        />
      )}

      {/* No stream URL */}
      {!loading && !streamUrl && !error && (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', flex: 1, color: 'var(--text-muted)', flexDirection: 'column', gap: 8 }}>
          <Server size={48} style={{ opacity: 0.3 }} />
          <p>Stream not available. Device may still be starting up.</p>
          <button className="btn btn-sm btn-primary" onClick={() => window.location.reload()}>
            <RefreshCw size={12} /> Retry
          </button>
        </div>
      )}

      <style>{`
        @keyframes spin {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
}

export default CloudDeviceScreen;
