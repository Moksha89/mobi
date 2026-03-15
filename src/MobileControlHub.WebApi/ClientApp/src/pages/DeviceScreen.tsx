import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  ArrowLeft, RotateCcw, Home, Square, ChevronUp, ChevronDown,
  Power, Type, Volume2, VolumeX, Maximize2, Minimize2, Sun
} from 'lucide-react';

const API_BASE = '/api';

interface ScreenInfo {
  width: number;
  height: number;
}

function DeviceScreen() {
  const { serial } = useParams<{ serial: string }>();
  const navigate = useNavigate();
  const imgRef = useRef<HTMLImageElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const [screenInfo, setScreenInfo] = useState<ScreenInfo>({ width: 1080, height: 1920 });
  const [streaming, setStreaming] = useState(true);
  const [fps, setFps] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [textInput, setTextInput] = useState('');
  const [showTextInput, setShowTextInput] = useState(false);
  const [fullscreen, setFullscreen] = useState(false);
  const [actionMsg, setActionMsg] = useState<string | null>(null);
  const [swipeStart, setSwipeStart] = useState<{ x: number; y: number } | null>(null);

  const frameCountRef = useRef(0);
  const streamingRef = useRef(true);

  // Fetch screen info on mount
  useEffect(() => {
    if (!serial) return;
    fetch(`${API_BASE}/devices/${serial}/screen/info`)
      .then(r => r.json())
      .then(data => setScreenInfo(data))
      .catch(() => {});
  }, [serial]);

  // Screenshot streaming loop
  useEffect(() => {
    if (!serial || !streaming) return;
    streamingRef.current = true;
    let cancelled = false;

    const fetchFrame = async () => {
      while (!cancelled && streamingRef.current) {
        try {
          const res = await fetch(`${API_BASE}/devices/${serial}/screen?_t=${Date.now()}`);
          if (!res.ok) {
            setError(`Screenshot failed (${res.status})`);
            await new Promise(r => setTimeout(r, 2000));
            continue;
          }
          const blob = await res.blob();
          if (imgRef.current && !cancelled) {
            const url = URL.createObjectURL(blob);
            const oldUrl = imgRef.current.src;
            imgRef.current.src = url;
            if (oldUrl.startsWith('blob:')) URL.revokeObjectURL(oldUrl);
            frameCountRef.current++;
            setError(null);
          }
        } catch {
          if (!cancelled) {
            setError('Connection lost. Retrying...');
            await new Promise(r => setTimeout(r, 2000));
          }
        }
        // Small delay between frames to avoid hammering
        await new Promise(r => setTimeout(r, 150));
      }
    };

    fetchFrame();

    // FPS counter
    const fpsInterval = setInterval(() => {
      setFps(frameCountRef.current);
      frameCountRef.current = 0;
    }, 1000);

    return () => {
      cancelled = true;
      streamingRef.current = false;
      clearInterval(fpsInterval);
    };
  }, [serial, streaming]);

  // Map click coordinates on the displayed image to device coordinates
  const mapCoordinates = useCallback((clientX: number, clientY: number): { x: number; y: number } | null => {
    const img = imgRef.current;
    if (!img) return null;
    const rect = img.getBoundingClientRect();
    const relX = (clientX - rect.left) / rect.width;
    const relY = (clientY - rect.top) / rect.height;
    return {
      x: Math.round(relX * screenInfo.width),
      y: Math.round(relY * screenInfo.height),
    };
  }, [screenInfo]);

  const sendAction = async (url: string, body: unknown) => {
    try {
      const res = await fetch(`${API_BASE}/devices/${serial}/screen/${url}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        const data = await res.json().catch(() => ({ message: 'Action failed' }));
        showMsg(`Error: ${data.message}`);
      }
    } catch (e) {
      showMsg(`Error: ${(e as Error).message}`);
    }
  };

  const showMsg = (msg: string) => {
    setActionMsg(msg);
    setTimeout(() => setActionMsg(null), 2000);
  };

  // Handle tap on screen image
  const handleMouseDown = (e: React.MouseEvent) => {
    const coords = mapCoordinates(e.clientX, e.clientY);
    if (coords) setSwipeStart(coords);
  };

  const handleMouseUp = (e: React.MouseEvent) => {
    const endCoords = mapCoordinates(e.clientX, e.clientY);
    if (!swipeStart || !endCoords) { setSwipeStart(null); return; }

    const dx = endCoords.x - swipeStart.x;
    const dy = endCoords.y - swipeStart.y;
    const dist = Math.sqrt(dx * dx + dy * dy);

    if (dist < 20) {
      // It's a tap
      sendAction('tap', { x: swipeStart.x, y: swipeStart.y });
    } else {
      // It's a swipe
      sendAction('swipe', {
        x1: swipeStart.x, y1: swipeStart.y,
        x2: endCoords.x, y2: endCoords.y,
        durationMs: 300,
      });
    }
    setSwipeStart(null);
  };

  const sendKey = (keyCode: number) => sendAction('key', { keyCode });
  const sendText = () => {
    if (textInput.trim()) {
      sendAction('text', { text: textInput });
      setTextInput('');
    }
  };

  const toggleFullscreen = () => {
    if (!fullscreen && containerRef.current) {
      containerRef.current.requestFullscreen?.();
      setFullscreen(true);
    } else {
      document.exitFullscreen?.();
      setFullscreen(false);
    }
  };

  useEffect(() => {
    const handler = () => setFullscreen(!!document.fullscreenElement);
    document.addEventListener('fullscreenchange', handler);
    return () => document.removeEventListener('fullscreenchange', handler);
  }, []);

  if (!serial) return <div className="loading-overlay">No device selected</div>;

  return (
    <div className="screen-viewer" ref={containerRef}>
      {/* Top bar */}
      <div className="screen-topbar">
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <button className="btn btn-ghost btn-sm" onClick={() => navigate('/devices')}>
            <ArrowLeft size={14} /> Back
          </button>
          <h3 style={{ fontSize: 16, fontWeight: 600 }}>{serial}</h3>
          <span className="badge info" style={{ fontSize: 11 }}>
            {screenInfo.width}x{screenInfo.height}
          </span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>
            {streaming ? `${fps} FPS` : 'Paused'}
          </span>
          <button
            className={`btn btn-sm ${streaming ? 'btn-danger' : 'btn-success'}`}
            onClick={() => setStreaming(!streaming)}
          >
            {streaming ? 'Pause' : 'Resume'}
          </button>
          <button className="btn btn-sm btn-ghost" onClick={toggleFullscreen}>
            {fullscreen ? <Minimize2 size={14} /> : <Maximize2 size={14} />}
          </button>
        </div>
      </div>

      {error && <div className="alert alert-warning" style={{ margin: '0 16px' }}>{error}</div>}
      {actionMsg && <div className="screen-toast">{actionMsg}</div>}

      <div className="screen-body">
        {/* Phone screen */}
        <div className="screen-phone-container">
          <div className="screen-phone-frame">
            <img
              ref={imgRef}
              className="screen-image"
              alt="Device screen"
              draggable={false}
              onMouseDown={handleMouseDown}
              onMouseUp={handleMouseUp}
              onContextMenu={e => e.preventDefault()}
            />
            {!streaming && (
              <div className="screen-paused-overlay">
                <p>Stream Paused</p>
              </div>
            )}
          </div>
        </div>

        {/* Control panel */}
        <div className="screen-controls">
          <div className="control-section">
            <h4>Navigation</h4>
            <div className="control-buttons">
              <button className="ctrl-btn" onClick={() => sendKey(4)} title="Back">
                <RotateCcw size={18} />
                <span>Back</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendKey(3)} title="Home">
                <Home size={18} />
                <span>Home</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendKey(187)} title="Recent Apps">
                <Square size={18} />
                <span>Recent</span>
              </button>
            </div>
          </div>

          <div className="control-section">
            <h4>Power & Screen</h4>
            <div className="control-buttons">
              <button className="ctrl-btn" onClick={() => sendAction('wake', {})} title="Wake Screen">
                <Sun size={18} />
                <span>Wake</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendKey(26)} title="Power">
                <Power size={18} />
                <span>Power</span>
              </button>
            </div>
          </div>

          <div className="control-section">
            <h4>Volume</h4>
            <div className="control-buttons">
              <button className="ctrl-btn" onClick={() => sendKey(24)} title="Volume Up">
                <Volume2 size={18} />
                <span>Vol +</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendKey(25)} title="Volume Down">
                <VolumeX size={18} />
                <span>Vol -</span>
              </button>
            </div>
          </div>

          <div className="control-section">
            <h4>Scroll</h4>
            <div className="control-buttons">
              <button className="ctrl-btn" onClick={() => sendAction('swipe', { x1: screenInfo.width / 2, y1: screenInfo.height * 0.7, x2: screenInfo.width / 2, y2: screenInfo.height * 0.3, durationMs: 300 })} title="Scroll Up">
                <ChevronUp size={18} />
                <span>Up</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendAction('swipe', { x1: screenInfo.width / 2, y1: screenInfo.height * 0.3, x2: screenInfo.width / 2, y2: screenInfo.height * 0.7, durationMs: 300 })} title="Scroll Down">
                <ChevronDown size={18} />
                <span>Down</span>
              </button>
            </div>
          </div>

          <div className="control-section">
            <h4>Text Input</h4>
            {showTextInput ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                <input
                  value={textInput}
                  onChange={e => setTextInput(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && sendText()}
                  placeholder="Type text..."
                  autoFocus
                  style={{ fontSize: 13 }}
                />
                <div style={{ display: 'flex', gap: 6 }}>
                  <button className="btn btn-sm btn-primary" onClick={sendText}>Send</button>
                  <button className="btn btn-sm btn-ghost" onClick={() => { setShowTextInput(false); setTextInput(''); }}>Cancel</button>
                </div>
              </div>
            ) : (
              <button className="ctrl-btn wide" onClick={() => setShowTextInput(true)}>
                <Type size={18} />
                <span>Type Text</span>
              </button>
            )}
          </div>

          <div className="control-section">
            <h4>Quick Keys</h4>
            <div className="control-buttons" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
              <button className="ctrl-btn small" onClick={() => sendKey(66)} title="Enter">Enter</button>
              <button className="ctrl-btn small" onClick={() => sendKey(67)} title="Backspace">Del</button>
              <button className="ctrl-btn small" onClick={() => sendKey(61)} title="Tab">Tab</button>
              <button className="ctrl-btn small" onClick={() => sendKey(62)} title="Space">Space</button>
              <button className="ctrl-btn small" onClick={() => sendKey(111)} title="Escape">Esc</button>
              <button className="ctrl-btn small" onClick={() => sendKey(82)} title="Menu">Menu</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default DeviceScreen;
