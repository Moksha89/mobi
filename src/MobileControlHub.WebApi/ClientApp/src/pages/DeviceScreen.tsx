import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate, useSearchParams } from 'react-router-dom';
import {
  ArrowLeft, RotateCcw, Home, Square, ChevronUp, ChevronDown, ChevronLeft, ChevronRight,
  Power, Type, Volume2, VolumeX, Maximize2, Minimize2, Sun, Lock, Unlock, Bell,
  Camera, RotateCw, Trash2, Clipboard, Search, Settings, MessageSquare, Phone, Send, RefreshCw
} from 'lucide-react';
import { twilioActions } from '../hooks/useApi';
import type { SmsMessage, TwilioNumberInfo } from '../types';

const API_BASE = '/api';

interface ScreenInfo {
  width: number;
  height: number;
}

function DeviceScreen() {
  const { serial } = useParams<{ serial: string }>();
  const [searchParams] = useSearchParams();
  const containerName = searchParams.get('container') || '';
  const navigate = useNavigate();
  const imgRef = useRef<HTMLImageElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const [screenInfo, setScreenInfo] = useState<ScreenInfo>({ width: 1080, height: 1920 });
  const [streaming, setStreaming] = useState(true);
  const [fps, setFps] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [textInput, setTextInput] = useState('');
  const [pinInput, setPinInput] = useState('');
  const [showTextInput, setShowTextInput] = useState(false);
  const [fullscreen, setFullscreen] = useState(false);
  const [actionMsg, setActionMsg] = useState<string | null>(null);
  const [imgQuality, setImgQuality] = useState(50);
  const [imgScale, setImgScale] = useState(0.5);
  const [frameSize, setFrameSize] = useState(0);

  // SMS state
  const [showSms, setShowSms] = useState(false);
  const [smsMessages, setSmsMessages] = useState<SmsMessage[]>([]);
  const [smsLoading, setSmsLoading] = useState(false);
  const [phoneInfo, setPhoneInfo] = useState<TwilioNumberInfo | null>(null);
  const [smsSendTo, setSmsSendTo] = useState('');
  const [smsSendBody, setSmsSendBody] = useState('');
  const [smsSending, setSmsSending] = useState(false);
  const [smsError, setSmsError] = useState<string | null>(null);
  const [smsSuccess, setSmsSuccess] = useState<string | null>(null);

  // Touch tracking refs to avoid stale closure issues
  const swipeStartRef = useRef<{ x: number; y: number; time: number } | null>(null);
  const isDraggingRef = useRef(false);

  const frameCountRef = useRef(0);
  const streamingRef = useRef(true);
  const fetchingRef = useRef(false);

  // Fetch screen info and phone number on mount
  useEffect(() => {
    if (!serial) return;
    fetch(`${API_BASE}/devices/${serial}/screen/info`)
      .then(r => r.json())
      .then(data => {
        if (data.width && data.height) setScreenInfo(data);
      })
      .catch(() => {});
  }, [serial]);

  useEffect(() => {
    if (!containerName) return;
    twilioActions.getNumberForContainer(containerName)
      .then(info => setPhoneInfo(info))
      .catch(() => setPhoneInfo(null));
  }, [containerName]);

  const loadSmsMessages = async () => {
    if (!containerName) return;
    setSmsLoading(true);
    try {
      const msgs = await twilioActions.getMessages(containerName);
      setSmsMessages(msgs);
    } catch { setSmsMessages([]); }
    setSmsLoading(false);
  };

  const handleSmsSend = async () => {
    if (!containerName || !smsSendTo || !smsSendBody) return;
    setSmsSending(true);
    setSmsError(null);
    setSmsSuccess(null);
    try {
      await twilioActions.sendSms(containerName, smsSendTo, smsSendBody);
      setSmsSendTo('');
      setSmsSendBody('');
      setSmsSuccess('SMS sent!');
      setTimeout(() => setSmsSuccess(null), 3000);
      await loadSmsMessages();
    } catch (e) {
      setSmsError((e as Error).message);
    }
    setSmsSending(false);
  };

  // Screenshot streaming loop — optimized: fetch as fast as network allows
  useEffect(() => {
    if (!serial || !streaming) return;
    streamingRef.current = true;
    fetchingRef.current = false;
    let cancelled = false;

    const fetchFrame = async () => {
      while (!cancelled && streamingRef.current) {
        if (fetchingRef.current) {
          await new Promise(r => setTimeout(r, 30));
          continue;
        }
        fetchingRef.current = true;
        try {
          const res = await fetch(`${API_BASE}/devices/${serial}/screen?quality=${imgQuality}&scale=${imgScale}&_t=${Date.now()}`);
          if (!res.ok) {
            setError(`Screenshot failed (${res.status})`);
            await new Promise(r => setTimeout(r, 1000));
            fetchingRef.current = false;
            continue;
          }
          const blob = await res.blob();
          setFrameSize(blob.size);
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
            await new Promise(r => setTimeout(r, 1500));
          }
        }
        fetchingRef.current = false;
        await new Promise(r => setTimeout(r, 30));
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
  }, [serial, streaming, imgQuality, imgScale]);

  // Map click coordinates on the displayed image to device coordinates
  const mapCoordinates = useCallback((clientX: number, clientY: number): { x: number; y: number } | null => {
    const img = imgRef.current;
    if (!img) return null;
    const rect = img.getBoundingClientRect();
    const clampedX = Math.max(rect.left, Math.min(clientX, rect.right));
    const clampedY = Math.max(rect.top, Math.min(clientY, rect.bottom));
    const relX = (clampedX - rect.left) / rect.width;
    const relY = (clampedY - rect.top) / rect.height;
    return {
      x: Math.round(Math.max(0, Math.min(relX * screenInfo.width, screenInfo.width - 1))),
      y: Math.round(Math.max(0, Math.min(relY * screenInfo.height, screenInfo.height - 1))),
    };
  }, [screenInfo]);

  const sendAction = useCallback(async (url: string, body: unknown) => {
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
  }, [serial]);

  const showMsg = (msg: string) => {
    setActionMsg(msg);
    setTimeout(() => setActionMsg(null), 2000);
  };

  // Pointer handlers (work for both mouse and touch)
  const handlePointerDown = (e: React.PointerEvent) => {
    e.preventDefault();
    const coords = mapCoordinates(e.clientX, e.clientY);
    if (coords) {
      swipeStartRef.current = { ...coords, time: Date.now() };
      isDraggingRef.current = true;
      (e.target as HTMLElement).setPointerCapture(e.pointerId);
    }
  };

  const handlePointerUp = (e: React.PointerEvent) => {
    e.preventDefault();
    if (!isDraggingRef.current || !swipeStartRef.current) {
      isDraggingRef.current = false;
      swipeStartRef.current = null;
      return;
    }
    const endCoords = mapCoordinates(e.clientX, e.clientY);
    const start = swipeStartRef.current;
    isDraggingRef.current = false;
    swipeStartRef.current = null;
    if (!endCoords) return;

    const dx = endCoords.x - start.x;
    const dy = endCoords.y - start.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    const elapsed = Date.now() - start.time;

    if (dist < 30) {
      sendAction('tap', { x: start.x, y: start.y });
    } else {
      const duration = Math.max(150, Math.min(elapsed, 800));
      sendAction('swipe', {
        x1: start.x, y1: start.y,
        x2: endCoords.x, y2: endCoords.y,
        durationMs: duration,
      });
    }
  };

  const handlePointerCancel = () => {
    isDraggingRef.current = false;
    swipeStartRef.current = null;
  };

  const sendKey = useCallback((keyCode: number) => sendAction('key', { keyCode }), [sendAction]);

  const sendText = () => {
    if (textInput.trim()) {
      sendAction('text', { text: textInput });
      setTextInput('');
    }
  };

  // PIN pad helpers — sends Android keycodes for digits 0-9
  const sendPinDigit = (digit: number) => {
    const keycode = digit === 0 ? 7 : 7 + digit; // KEYCODE_0=7, KEYCODE_1=8, ..., KEYCODE_9=16
    sendKey(keycode);
    setPinInput(prev => prev + digit.toString());
  };

  const sendPinEnter = () => {
    sendKey(66); // KEYCODE_ENTER
    setPinInput('');
  };

  const clearPinDigit = () => {
    sendKey(67); // KEYCODE_DEL
    setPinInput(prev => prev.slice(0, -1));
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
              style={{ touchAction: 'none' }}
              onPointerDown={handlePointerDown}
              onPointerUp={handlePointerUp}
              onPointerCancel={handlePointerCancel}
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
            <h4>Stream Performance</h4>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 8 }}>
              {fps} FPS | {frameSize > 0 ? `${(frameSize / 1024).toFixed(0)} KB/frame` : '—'}
            </div>
            <div style={{ marginBottom: 8 }}>
              <label style={{ fontSize: 11, color: 'var(--text-secondary)', display: 'flex', justifyContent: 'space-between' }}>
                <span>Quality</span><span>{imgQuality}%</span>
              </label>
              <input type="range" min={10} max={100} step={5} value={imgQuality}
                onChange={e => setImgQuality(Number(e.target.value))}
                style={{ width: '100%', accentColor: 'var(--accent)' }} />
            </div>
            <div style={{ marginBottom: 8 }}>
              <label style={{ fontSize: 11, color: 'var(--text-secondary)', display: 'flex', justifyContent: 'space-between' }}>
                <span>Resolution</span><span>{Math.round(imgScale * 100)}%</span>
              </label>
              <input type="range" min={20} max={100} step={10} value={imgScale * 100}
                onChange={e => setImgScale(Number(e.target.value) / 100)}
                style={{ width: '100%', accentColor: 'var(--accent)' }} />
            </div>
            <div style={{ display: 'flex', gap: 4 }}>
              <button className="ctrl-btn small" style={{ flex: 1 }} onClick={() => { setImgQuality(30); setImgScale(0.3); }} title="Low quality, fastest">Fast</button>
              <button className="ctrl-btn small" style={{ flex: 1 }} onClick={() => { setImgQuality(50); setImgScale(0.5); }} title="Balanced">Medium</button>
              <button className="ctrl-btn small" style={{ flex: 1 }} onClick={() => { setImgQuality(85); setImgScale(1.0); }} title="High quality, slower">HD</button>
            </div>
          </div>

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
              <button className="ctrl-btn" onClick={() => sendKey(223)} title="Lock Screen">
                <Lock size={18} />
                <span>Lock</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendKey(164)} title="Mute">
                <VolumeX size={18} />
                <span>Mute</span>
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
            <h4>Scroll / Swipe</h4>
            <div className="control-buttons">
              <button className="ctrl-btn" onClick={() => sendAction('swipe', { x1: screenInfo.width / 2, y1: screenInfo.height * 0.7, x2: screenInfo.width / 2, y2: screenInfo.height * 0.3, durationMs: 300 })} title="Scroll Up">
                <ChevronUp size={18} />
                <span>Up</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendAction('swipe', { x1: screenInfo.width / 2, y1: screenInfo.height * 0.3, x2: screenInfo.width / 2, y2: screenInfo.height * 0.7, durationMs: 300 })} title="Scroll Down">
                <ChevronDown size={18} />
                <span>Down</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendAction('swipe', { x1: screenInfo.width * 0.8, y1: screenInfo.height / 2, x2: screenInfo.width * 0.2, y2: screenInfo.height / 2, durationMs: 300 })} title="Swipe Left">
                <ChevronLeft size={18} />
                <span>Left</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendAction('swipe', { x1: screenInfo.width * 0.2, y1: screenInfo.height / 2, x2: screenInfo.width * 0.8, y2: screenInfo.height / 2, durationMs: 300 })} title="Swipe Right">
                <ChevronRight size={18} />
                <span>Right</span>
              </button>
            </div>
          </div>

          <div className="control-section">
            <h4>More Controls</h4>
            <div className="control-buttons">
              <button className="ctrl-btn" onClick={() => sendKey(220)} title="Brightness Down">
                <Sun size={18} />
                <span>Bright -</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendKey(221)} title="Brightness Up">
                <Sun size={18} />
                <span>Bright +</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendKey(120)} title="Take Screenshot">
                <Camera size={18} />
                <span>Screenshot</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendKey(84)} title="Search">
                <Search size={18} />
                <span>Search</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendAction('swipe', { x1: screenInfo.width / 2, y1: 0, x2: screenInfo.width / 2, y2: screenInfo.height * 0.3, durationMs: 300 })} title="Notifications">
                <Bell size={18} />
                <span>Notifs</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendAction('swipe', { x1: screenInfo.width / 2, y1: 0, x2: screenInfo.width / 2, y2: screenInfo.height * 0.6, durationMs: 400 })} title="Quick Settings">
                <Settings size={18} />
                <span>Quick Set</span>
              </button>
              <button className="ctrl-btn" onClick={() => { sendKey(3); setTimeout(() => sendKey(3), 200); }} title="App Switch (Double Home)">
                <Clipboard size={18} />
                <span>App Switch</span>
              </button>
              <button className="ctrl-btn" onClick={() => sendKey(280)} title="Rotate Screen">
                <RotateCw size={18} />
                <span>Rotate</span>
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
              <button className="ctrl-btn small" onClick={() => sendKey(112)} title="Delete Forward">FwdDel</button>
              <button className="ctrl-btn small" onClick={() => sendKey(122)} title="Move Home">MvHome</button>
              <button className="ctrl-btn small" onClick={() => sendKey(123)} title="Move End">MvEnd</button>
            </div>
          </div>

          {/* SMS Inbox Panel */}
          {containerName && phoneInfo && (
            <div className="control-section">
              <h4 style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <MessageSquare size={14} /> SMS Inbox
                </span>
                <button className="ctrl-btn small" onClick={() => { setShowSms(!showSms); if (!showSms) loadSmsMessages(); }}>
                  {showSms ? 'Hide' : 'Show'}
                </button>
              </h4>
              <div style={{ fontSize: 11, color: '#a6e3a1', fontFamily: 'monospace', marginBottom: 6 }}>
                <Phone size={10} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 4 }} />
                {phoneInfo.phoneNumber}
              </div>
              {showSms && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {/* Send SMS form */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, padding: 8, background: 'var(--bg)', borderRadius: 6, border: '1px solid var(--border)' }}>
                    <input
                      value={smsSendTo}
                      onChange={e => setSmsSendTo(e.target.value)}
                      placeholder="To: +1234567890"
                      style={{ fontSize: 12, padding: '4px 8px' }}
                    />
                    <div style={{ display: 'flex', gap: 4 }}>
                      <input
                        value={smsSendBody}
                        onChange={e => setSmsSendBody(e.target.value)}
                        onKeyDown={e => e.key === 'Enter' && handleSmsSend()}
                        placeholder="Message..."
                        style={{ fontSize: 12, padding: '4px 8px', flex: 1 }}
                      />
                      <button
                        className="btn btn-sm btn-primary"
                        onClick={handleSmsSend}
                        disabled={smsSending || !smsSendTo || !smsSendBody}
                        style={{ padding: '4px 8px', fontSize: 11 }}
                      >
                        <Send size={10} /> {smsSending ? '...' : 'Send'}
                      </button>
                    </div>
                    {smsError && <div style={{ fontSize: 10, color: '#f38ba8', wordBreak: 'break-word' }}>{smsError}</div>}
                    {smsSuccess && <div style={{ fontSize: 10, color: '#a6e3a1' }}>{smsSuccess}</div>}
                  </div>

                  {/* Messages list */}
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{smsMessages.length} message(s)</span>
                    <button className="ctrl-btn small" onClick={loadSmsMessages} disabled={smsLoading}>
                      <RefreshCw size={10} /> Refresh
                    </button>
                  </div>
                  <div style={{ maxHeight: 200, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 4 }}>
                    {smsLoading ? (
                      <div style={{ textAlign: 'center', padding: 12, color: 'var(--text-muted)', fontSize: 11 }}>Loading...</div>
                    ) : smsMessages.length === 0 ? (
                      <div style={{ textAlign: 'center', padding: 12, color: 'var(--text-muted)', fontSize: 11 }}>
                        No messages yet.
                      </div>
                    ) : (
                      smsMessages.map(msg => (
                        <div key={msg.id} style={{
                          padding: '6px 8px', borderRadius: 6, fontSize: 11,
                          background: msg.direction === 'inbound' ? 'rgba(166,227,161,0.1)' : 'rgba(137,180,250,0.1)',
                          border: `1px solid ${msg.direction === 'inbound' ? 'rgba(166,227,161,0.2)' : 'rgba(137,180,250,0.2)'}`,
                        }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                            <span style={{ fontWeight: 600, color: msg.direction === 'inbound' ? '#a6e3a1' : '#89b4fa' }}>
                              {msg.direction === 'inbound' ? `From: ${msg.fromNumber}` : `To: ${msg.toNumber}`}
                            </span>
                            <span style={{ color: 'var(--text-muted)', fontSize: 10 }}>
                              {new Date(msg.receivedAt).toLocaleTimeString()}
                            </span>
                          </div>
                          <div style={{ color: 'var(--text-primary)' }}>{msg.body}</div>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              )}
            </div>
          )}

          <div className="control-section">
            <h4>PIN / Password Unlock</h4>
            <div style={{ marginBottom: 8, padding: '8px 12px', background: 'var(--bg-surface)', borderRadius: 8, fontFamily: 'monospace', fontSize: 18, textAlign: 'center', letterSpacing: 4, minHeight: 32, color: 'var(--text-primary)' }}>
              {pinInput ? '*'.repeat(pinInput.length) : <span style={{ color: 'var(--text-muted)', fontSize: 12, letterSpacing: 0 }}>Enter PIN/Password</span>}
            </div>
            <div className="pin-pad">
              {[1, 2, 3, 4, 5, 6, 7, 8, 9].map(d => (
                <button key={d} className="pin-btn" onClick={() => sendPinDigit(d)}>{d}</button>
              ))}
              <button className="pin-btn pin-del" onClick={clearPinDigit}><Trash2 size={16} /> Del</button>
              <button className="pin-btn" onClick={() => sendPinDigit(0)}>0</button>
              <button className="pin-btn pin-del" onClick={() => { setPinInput(''); }} title="Clear All">Clr</button>
            </div>
            <button className="pin-btn pin-enter" onClick={sendPinEnter}>
              <Unlock size={16} /> Enter / Unlock
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

export default DeviceScreen;
