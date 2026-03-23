import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, RefreshCw, Loader2 } from 'lucide-react';
import { genymotionActions } from '../hooks/useApi';
import type { GenymotionInstance } from '../types';

declare global {
  interface Window {
    genyDeviceWebPlayer?: {
      DeviceRendererFactory: new () => {
        setupRenderer: (
          container: HTMLElement,
          webrtcAddress: string,
          options: Record<string, unknown>,
        ) => { VM_communication: { disconnect: () => void } };
      };
    };
  }
}

function GenymotionScreen() {
  const { uuid } = useParams<{ uuid: string }>();
  const navigate = useNavigate();
  const containerRef = useRef<HTMLDivElement>(null);
  const rendererRef = useRef<{ VM_communication: { disconnect: () => void } } | null>(null);

  const [instance, setInstance] = useState<GenymotionInstance | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [connected, setConnected] = useState(false);

  const connectToInstance = useCallback(async (inst: GenymotionInstance) => {
    if (!containerRef.current || !inst.webrtcUrl) return;

    try {
      setError(null);
      // Get access token from backend
      const { accessToken } = await genymotionActions.getAccessToken(inst.uuid);

      // Check if the Genymotion web player SDK is loaded
      if (!window.genyDeviceWebPlayer) {
        setError('Genymotion web player SDK not loaded. Please refresh the page.');
        return;
      }

      // Clear any previous renderer
      if (rendererRef.current) {
        try { rendererRef.current.VM_communication.disconnect(); } catch { /* ignore */ }
        rendererRef.current = null;
      }
      containerRef.current.innerHTML = '';

      // Create the device renderer
      const factory = new window.genyDeviceWebPlayer.DeviceRendererFactory();
      const renderer = factory.setupRenderer(
        containerRef.current,
        inst.webrtcUrl,
        {
          token: accessToken,
          keyboard: true,
          mouse: true,
          multitouch: true,
          clipboard: true,
          battery: true,
          gps: true,
          rotation: true,
          navbar: true,
          power: true,
          volume: true,
          phone: true,
          fileUpload: false,
          camera: true,
          microphone: false,
          diskIO: true,
          network: true,
          biometrics: true,
          storeAndForward: false,
        },
      );

      rendererRef.current = renderer;
      setConnected(true);
    } catch (e) {
      setError(`Connection failed: ${(e as Error).message}`);
    }
  }, []);

  // Load instance details
  useEffect(() => {
    if (!uuid) return;
    setLoading(true);
    genymotionActions.getInstance(uuid)
      .then(inst => {
        setInstance(inst);
        if (inst.state !== 'ONLINE') {
          setError(`Instance is ${inst.state}. It must be ONLINE to view the screen.`);
        }
      })
      .catch(e => setError((e as Error).message))
      .finally(() => setLoading(false));
  }, [uuid]);

  // Auto-connect when instance is loaded and ONLINE
  useEffect(() => {
    if (instance && instance.state === 'ONLINE' && instance.webrtcUrl && !connected) {
      connectToInstance(instance);
    }
  }, [instance, connected, connectToInstance]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (rendererRef.current) {
        try { rendererRef.current.VM_communication.disconnect(); } catch { /* ignore */ }
      }
    };
  }, []);

  if (!uuid) return <div className="loading-overlay">No instance selected</div>;

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
            {instance?.name || 'Genymotion Device'}
          </h3>
          {instance && (
            <>
              <span className="badge info" style={{ fontSize: 11 }}>
                {instance.recipeName}
              </span>
              <span className="badge info" style={{ fontSize: 11 }}>
                {instance.androidVersion}
              </span>
              <span className={`badge ${instance.state === 'ONLINE' ? 'online' : 'warning'}`} style={{ fontSize: 11 }}>
                <span className="badge-dot" />
                {instance.state}
              </span>
            </>
          )}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {connected && (
            <span style={{ fontSize: 12, color: '#a6e3a1' }}>Connected</span>
          )}
          <button
            className="btn btn-sm btn-ghost"
            onClick={() => {
              if (instance) {
                setConnected(false);
                connectToInstance(instance);
              }
            }}
            title="Reconnect"
          >
            <RefreshCw size={14} />
          </button>
        </div>
      </div>

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
          <span style={{ marginLeft: 12 }}>Loading instance...</span>
        </div>
      )}

      {/* Genymotion device renderer container */}
      {!loading && (
        <div
          ref={containerRef}
          id="genymotion-container"
          style={{
            flex: 1,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            overflow: 'hidden',
            minHeight: 0,
          }}
        />
      )}

      <style>{`
        @keyframes spin {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }
        #genymotion-container {
          --gm-background-color: #1A1A1A;
          --gm-on-background-color: #ffffff;
          --gm-surface-color: #292929;
          --gm-on-surface-color: #ffffff;
          --gm-primary-color: rgba(166, 227, 161, 1);
          --gm-secondary-color: #292929;
          --gm-on-secondary-color: #ffffff;
        }
      `}</style>
    </div>
  );
}

export default GenymotionScreen;
