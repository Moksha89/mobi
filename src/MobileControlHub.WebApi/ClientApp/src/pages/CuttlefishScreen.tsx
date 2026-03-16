import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  ArrowLeft, RefreshCw, Loader2, Phone, MessageSquare, Cpu, HardDrive, Server,
  MapPin, Battery, Wifi, WifiOff, RotateCw, Sun, Power, Volume2, VolumeX,
  Home, Square, ChevronLeft, Camera, Fingerprint, Navigation, Thermometer,
  Signal, Plane, Smartphone, Monitor, Maximize2, Minimize2
} from 'lucide-react';
import { cuttlefishActions } from '../hooks/useApi';
import type { CuttlefishDevice } from '../types';

function CuttlefishScreen() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const navigate = useNavigate();
  const iframeRef = useRef<HTMLIFrameElement>(null);

  const [device, setDevice] = useState<CuttlefishDevice | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [streamUrl, setStreamUrl] = useState<string | null>(null);
  const [actionMsg, setActionMsg] = useState<string | null>(null);
  const [fullscreen, setFullscreen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Environment controls state
  const [gpsLat, setGpsLat] = useState('37.4220');
  const [gpsLng, setGpsLng] = useState('-122.0841');
  const [batteryLevel, setBatteryLevel] = useState(100);
  const [batteryStatus, setBatteryStatus] = useState('charging');
  const [networkMode, setNetworkMode] = useState('wifi');
  const [orientation, setOrientation] = useState('portrait');
  const [showSidebar, setShowSidebar] = useState(true);
  const [activePanel, setActivePanel] = useState<string | null>(null);

  useEffect(() => {
    if (!deviceId) return;
    setLoading(true);
    cuttlefishActions.getDevice(deviceId)
      .then(dev => {
        setDevice(dev);
        if (dev.batteryLevel) setBatteryLevel(dev.batteryLevel);
        if (dev.batteryStatus) setBatteryStatus(dev.batteryStatus);
        if (dev.networkMode) setNetworkMode(dev.networkMode);
        if (dev.orientation) setOrientation(dev.orientation);
        if (dev.gpsLocation) {
          const [lat, lng] = dev.gpsLocation.split(',');
          if (lat) setGpsLat(lat);
          if (lng) setGpsLng(lng);
        }
        if (dev.state !== 'running') {
          setError(`Device is ${dev.state}. It must be running to view the screen.`);
        } else if (dev.webRtcUrl) {
          setStreamUrl(dev.webRtcUrl);
        } else {
          cuttlefishActions.getStreamUrl(deviceId)
            .then(r => setStreamUrl(r.streamUrl))
            .catch(() => setError('Could not get WebRTC stream URL'));
        }
      })
      .catch(e => setError((e as Error).message))
      .finally(() => setLoading(false));
  }, [deviceId]);

  const showMsg = (msg: string) => {
    setActionMsg(msg);
    setTimeout(() => setActionMsg(null), 3000);
  };

  const handleSetGps = async () => {
    if (!deviceId) return;
    try {
      await cuttlefishActions.setGps(deviceId, parseFloat(gpsLat), parseFloat(gpsLng));
      showMsg(`GPS set to ${gpsLat}, ${gpsLng}`);
    } catch (e) {
      showMsg(`GPS failed: ${(e as Error).message}`);
    }
  };

  const handleSetBattery = async () => {
    if (!deviceId) return;
    try {
      await cuttlefishActions.setBattery(deviceId, batteryLevel, batteryStatus);
      showMsg(`Battery set to ${batteryLevel}% (${batteryStatus})`);
    } catch (e) {
      showMsg(`Battery failed: ${(e as Error).message}`);
    }
  };

  const handleSetNetwork = async (mode: string) => {
    if (!deviceId) return;
    setNetworkMode(mode);
    try {
      await cuttlefishActions.setNetwork(deviceId, mode);
      showMsg(`Network: ${mode}`);
    } catch (e) {
      showMsg(`Network failed: ${(e as Error).message}`);
    }
  };

  const handleRotate = async (orient: string) => {
    if (!deviceId) return;
    setOrientation(orient);
    try {
      await cuttlefishActions.rotate(deviceId, orient);
      showMsg(`Rotated: ${orient}`);
    } catch (e) {
      showMsg(`Rotate failed: ${(e as Error).message}`);
    }
  };

  const handleShell = async (command: string) => {
    if (!deviceId) return;
    try {
      await cuttlefishActions.shell(deviceId, command);
      showMsg('Command sent');
    } catch (e) {
      showMsg(`Shell failed: ${(e as Error).message}`);
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

  if (!deviceId) return <div className="loading-overlay">No device selected</div>;

  const sidebarBtns: Array<{ id: string; icon: React.ReactNode; label: string }> = [
    { id: 'gps', icon: <MapPin size={18} />, label: 'GPS' },
    { id: 'battery', icon: <Battery size={18} />, label: 'Battery' },
    { id: 'network', icon: <Wifi size={18} />, label: 'Network' },
    { id: 'rotate', icon: <RotateCw size={18} />, label: 'Rotate' },
    { id: 'nav', icon: <Home size={18} />, label: 'Navigation' },
    { id: 'power', icon: <Power size={18} />, label: 'Power' },
    { id: 'camera', icon: <Camera size={18} />, label: 'Camera' },
    { id: 'biometrics', icon: <Fingerprint size={18} />, label: 'Biometrics' },
  ];

  return (
    <div ref={containerRef} style={{ display: 'flex', flexDirection: 'column', height: '100vh', background: '#1a1a1a' }}>
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
            {device?.name || 'Cuttlefish VM'}
          </h3>
          {device && (
            <>
              <span className="badge info" style={{ fontSize: 11 }}>
                {device.brand} {device.model}
              </span>
              <span className="badge info" style={{ fontSize: 11 }}>
                Android {device.androidVersion}
              </span>
              <span className={`badge ${device.state === 'running' ? 'online' : 'warning'}`} style={{ fontSize: 11 }}>
                <span className="badge-dot" />
                {device.state}
              </span>
              {device.gpuAccelerated && (
                <span className="badge info" style={{ fontSize: 11, background: 'rgba(166,227,161,0.15)', color: '#a6e3a1' }}>
                  GPU
                </span>
              )}
              <span className="badge info" style={{ fontSize: 11, background: 'rgba(137,180,250,0.15)', color: '#89b4fa' }}>
                Cuttlefish VM
              </span>
            </>
          )}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {device?.phoneNumber && (
            <span style={{ fontSize: 12, color: '#a6e3a1', display: 'flex', alignItems: 'center', gap: 4 }}>
              <Phone size={12} /> {device.phoneNumber}
            </span>
          )}
          <button className="btn btn-sm btn-ghost" onClick={() => setShowSidebar(!showSidebar)}
            title={showSidebar ? 'Hide controls' : 'Show controls'}>
            {showSidebar ? <ChevronLeft size={14} /> : <Smartphone size={14} />}
          </button>
          <button className="btn btn-sm btn-ghost" onClick={toggleFullscreen}>
            {fullscreen ? <Minimize2 size={14} /> : <Maximize2 size={14} />}
          </button>
          <button className="btn btn-sm btn-ghost" onClick={() => {
            if (iframeRef.current && streamUrl) iframeRef.current.src = streamUrl;
          }} title="Reconnect">
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
          <span><Server size={11} style={{ verticalAlign: 'middle' }} /> {device.storageGb} GB</span>
          {device.hasGapps && <span style={{ color: '#a6e3a1' }}>GApps</span>}
          {device.hasModem && <span style={{ color: '#89b4fa' }}>Modem</span>}
          {device.hasGps && <span style={{ color: '#f9e2af' }}>GPS</span>}
          {device.hasSensors && <span style={{ color: '#cba6f7' }}>Sensors</span>}
          {device.hasCamera && <span style={{ color: '#f38ba8' }}>Camera</span>}
          {device.hasBiometrics && <span style={{ color: '#94e2d5' }}>Biometrics</span>}
          {device.phoneNumber && <span><MessageSquare size={11} style={{ verticalAlign: 'middle' }} /> SMS/Voice</span>}
        </div>
      )}

      {/* Action message toast */}
      {actionMsg && (
        <div style={{
          position: 'fixed', top: 80, right: 20, zIndex: 1000,
          padding: '8px 16px', borderRadius: 8, fontSize: 13,
          background: 'rgba(166,227,161,0.9)', color: '#000', fontWeight: 500,
        }}>
          {actionMsg}
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
          <span style={{ marginLeft: 12 }}>Loading Cuttlefish VM...</span>
        </div>
      )}

      {/* Main content: WebRTC viewer + Sidebar */}
      {!loading && (
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {/* WebRTC Screen Viewer */}
          {streamUrl ? (
            <iframe
              ref={iframeRef}
              src={streamUrl}
              style={{ flex: 1, border: 'none', minHeight: 0, background: '#000' }}
              allow="clipboard-read; clipboard-write; autoplay; camera; microphone"
              title="Cuttlefish WebRTC Stream"
            />
          ) : (
            <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-muted)', flexDirection: 'column', gap: 8 }}>
              <Monitor size={48} style={{ opacity: 0.3 }} />
              <p>WebRTC stream not available. VM may still be booting.</p>
              <p style={{ fontSize: 12 }}>Cuttlefish VMs take 30-90 seconds to boot completely.</p>
              <button className="btn btn-sm btn-primary" onClick={() => window.location.reload()}>
                <RefreshCw size={12} /> Retry
              </button>
            </div>
          )}

          {/* Genymotion-like Sidebar Controls */}
          {showSidebar && (
            <div style={{
              width: activePanel ? 320 : 56, flexShrink: 0,
              background: 'var(--surface)', borderLeft: '1px solid var(--border)',
              display: 'flex', transition: 'width 0.2s ease',
            }}>
              {/* Icon strip */}
              <div style={{
                width: 56, display: 'flex', flexDirection: 'column', alignItems: 'center',
                gap: 2, paddingTop: 8, borderRight: activePanel ? '1px solid var(--border)' : 'none',
              }}>
                {sidebarBtns.map(btn => (
                  <button
                    key={btn.id}
                    onClick={() => setActivePanel(activePanel === btn.id ? null : btn.id)}
                    title={btn.label}
                    style={{
                      width: 44, height: 44, display: 'flex', flexDirection: 'column',
                      alignItems: 'center', justifyContent: 'center', gap: 2,
                      border: 'none', borderRadius: 8, cursor: 'pointer', fontSize: 9,
                      background: activePanel === btn.id ? 'rgba(166,227,161,0.15)' : 'transparent',
                      color: activePanel === btn.id ? '#a6e3a1' : 'var(--text-muted)',
                    }}
                  >
                    {btn.icon}
                    <span>{btn.label}</span>
                  </button>
                ))}
              </div>

              {/* Expanded panel */}
              {activePanel && (
                <div style={{ flex: 1, padding: 12, overflowY: 'auto', fontSize: 13 }}>
                  {/* GPS Panel */}
                  {activePanel === 'gps' && (
                    <div>
                      <h4 style={{ margin: '0 0 12px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }}>
                        <MapPin size={16} /> GPS Location
                      </h4>
                      <div style={{ marginBottom: 8 }}>
                        <label style={{ fontSize: 11, color: 'var(--text-secondary)' }}>Latitude</label>
                        <input type="text" value={gpsLat} onChange={e => setGpsLat(e.target.value)}
                          style={{ width: '100%', padding: '6px 8px', borderRadius: 6, border: '1px solid var(--border)', background: 'var(--bg)', color: 'var(--text)', fontSize: 13 }} />
                      </div>
                      <div style={{ marginBottom: 8 }}>
                        <label style={{ fontSize: 11, color: 'var(--text-secondary)' }}>Longitude</label>
                        <input type="text" value={gpsLng} onChange={e => setGpsLng(e.target.value)}
                          style={{ width: '100%', padding: '6px 8px', borderRadius: 6, border: '1px solid var(--border)', background: 'var(--bg)', color: 'var(--text)', fontSize: 13 }} />
                      </div>
                      <button className="btn btn-sm btn-primary" onClick={handleSetGps} style={{ width: '100%', marginBottom: 12 }}>
                        <Navigation size={12} /> Set Location
                      </button>
                      <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                        <p style={{ margin: '4px 0' }}>Quick locations:</p>
                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                          {[
                            { label: 'New York', lat: '40.7128', lng: '-74.0060' },
                            { label: 'San Francisco', lat: '37.7749', lng: '-122.4194' },
                            { label: 'London', lat: '51.5074', lng: '-0.1278' },
                            { label: 'Tokyo', lat: '35.6762', lng: '139.6503' },
                            { label: 'Mumbai', lat: '19.0760', lng: '72.8777' },
                            { label: 'Sydney', lat: '-33.8688', lng: '151.2093' },
                          ].map(loc => (
                            <button key={loc.label} className="btn btn-sm btn-ghost"
                              style={{ fontSize: 10, padding: '2px 6px' }}
                              onClick={() => { setGpsLat(loc.lat); setGpsLng(loc.lng); }}>
                              {loc.label}
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>
                  )}

                  {/* Battery Panel */}
                  {activePanel === 'battery' && (
                    <div>
                      <h4 style={{ margin: '0 0 12px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }}>
                        <Battery size={16} /> Battery
                      </h4>
                      <div style={{ marginBottom: 12 }}>
                        <label style={{ fontSize: 11, color: 'var(--text-secondary)', display: 'flex', justifyContent: 'space-between' }}>
                          <span>Level</span><span>{batteryLevel}%</span>
                        </label>
                        <input type="range" min={0} max={100} value={batteryLevel}
                          onChange={e => setBatteryLevel(Number(e.target.value))}
                          style={{ width: '100%', accentColor: batteryLevel > 20 ? '#a6e3a1' : '#f38ba8' }} />
                      </div>
                      <div style={{ marginBottom: 12 }}>
                        <label style={{ fontSize: 11, color: 'var(--text-secondary)' }}>Status</label>
                        <select value={batteryStatus} onChange={e => setBatteryStatus(e.target.value)}
                          style={{ width: '100%', padding: '6px 8px', borderRadius: 6, border: '1px solid var(--border)', background: 'var(--bg)', color: 'var(--text)', fontSize: 13 }}>
                          <option value="charging">Charging</option>
                          <option value="discharging">Discharging</option>
                          <option value="full">Full</option>
                          <option value="not_charging">Not Charging</option>
                        </select>
                      </div>
                      <button className="btn btn-sm btn-primary" onClick={handleSetBattery} style={{ width: '100%' }}>
                        Apply Battery Settings
                      </button>
                      <div style={{ display: 'flex', gap: 4, marginTop: 8, flexWrap: 'wrap' }}>
                        {[100, 75, 50, 25, 10, 5].map(lvl => (
                          <button key={lvl} className="btn btn-sm btn-ghost"
                            style={{ fontSize: 10, padding: '2px 6px' }}
                            onClick={() => setBatteryLevel(lvl)}>
                            {lvl}%
                          </button>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Network Panel */}
                  {activePanel === 'network' && (
                    <div>
                      <h4 style={{ margin: '0 0 12px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }}>
                        <Wifi size={16} /> Network
                      </h4>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                        {[
                          { mode: 'wifi', icon: <Wifi size={16} />, label: 'Wi-Fi', desc: 'Connected to Wi-Fi network' },
                          { mode: 'cellular', icon: <Signal size={16} />, label: 'Cellular', desc: 'Mobile data (4G/5G)' },
                          { mode: 'airplane', icon: <Plane size={16} />, label: 'Airplane Mode', desc: 'All radios off' },
                          { mode: 'off', icon: <WifiOff size={16} />, label: 'No Connection', desc: 'Wi-Fi and data disabled' },
                        ].map(opt => (
                          <button key={opt.mode}
                            onClick={() => handleSetNetwork(opt.mode)}
                            style={{
                              display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px',
                              borderRadius: 8, border: '1px solid var(--border)', cursor: 'pointer',
                              background: networkMode === opt.mode ? 'rgba(166,227,161,0.15)' : 'var(--bg)',
                              color: networkMode === opt.mode ? '#a6e3a1' : 'var(--text)',
                            }}>
                            {opt.icon}
                            <div style={{ textAlign: 'left' }}>
                              <div style={{ fontWeight: 500, fontSize: 13 }}>{opt.label}</div>
                              <div style={{ fontSize: 10, color: 'var(--text-muted)' }}>{opt.desc}</div>
                            </div>
                          </button>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Rotate Panel */}
                  {activePanel === 'rotate' && (
                    <div>
                      <h4 style={{ margin: '0 0 12px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }}>
                        <RotateCw size={16} /> Display Rotation
                      </h4>
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                        {[
                          { orient: 'portrait', label: 'Portrait', icon: <Smartphone size={24} /> },
                          { orient: 'landscape', label: 'Landscape', icon: <Monitor size={24} /> },
                          { orient: 'reverse_portrait', label: 'Reverse Portrait', icon: <Smartphone size={24} style={{ transform: 'rotate(180deg)' }} /> },
                          { orient: 'reverse_landscape', label: 'Reverse Landscape', icon: <Monitor size={24} style={{ transform: 'rotate(180deg)' }} /> },
                        ].map(opt => (
                          <button key={opt.orient}
                            onClick={() => handleRotate(opt.orient)}
                            style={{
                              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
                              padding: '12px 8px', borderRadius: 8, border: '1px solid var(--border)',
                              cursor: 'pointer',
                              background: orientation === opt.orient ? 'rgba(166,227,161,0.15)' : 'var(--bg)',
                              color: orientation === opt.orient ? '#a6e3a1' : 'var(--text)',
                            }}>
                            {opt.icon}
                            <span style={{ fontSize: 10 }}>{opt.label}</span>
                          </button>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Navigation Panel */}
                  {activePanel === 'nav' && (
                    <div>
                      <h4 style={{ margin: '0 0 12px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }}>
                        <Home size={16} /> Navigation
                      </h4>
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                        <button className="btn btn-sm btn-ghost" style={{ padding: '12px 8px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}
                          onClick={() => handleShell('input keyevent 3')}>
                          <Home size={20} /> <span style={{ fontSize: 10 }}>Home</span>
                        </button>
                        <button className="btn btn-sm btn-ghost" style={{ padding: '12px 8px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}
                          onClick={() => handleShell('input keyevent 4')}>
                          <ChevronLeft size={20} /> <span style={{ fontSize: 10 }}>Back</span>
                        </button>
                        <button className="btn btn-sm btn-ghost" style={{ padding: '12px 8px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}
                          onClick={() => handleShell('input keyevent 187')}>
                          <Square size={20} /> <span style={{ fontSize: 10 }}>Recent</span>
                        </button>
                        <button className="btn btn-sm btn-ghost" style={{ padding: '12px 8px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}
                          onClick={() => handleShell('input keyevent 26')}>
                          <Power size={20} /> <span style={{ fontSize: 10 }}>Power</span>
                        </button>
                      </div>
                    </div>
                  )}

                  {/* Power Panel */}
                  {activePanel === 'power' && (
                    <div>
                      <h4 style={{ margin: '0 0 12px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }}>
                        <Power size={16} /> Power & Volume
                      </h4>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                        <button className="btn btn-sm btn-ghost" style={{ justifyContent: 'flex-start', gap: 8 }}
                          onClick={() => handleShell('input keyevent 26')}>
                          <Power size={16} /> Power Button
                        </button>
                        <button className="btn btn-sm btn-ghost" style={{ justifyContent: 'flex-start', gap: 8 }}
                          onClick={() => handleShell('input keyevent 24')}>
                          <Volume2 size={16} /> Volume Up
                        </button>
                        <button className="btn btn-sm btn-ghost" style={{ justifyContent: 'flex-start', gap: 8 }}
                          onClick={() => handleShell('input keyevent 25')}>
                          <VolumeX size={16} /> Volume Down
                        </button>
                        <button className="btn btn-sm btn-ghost" style={{ justifyContent: 'flex-start', gap: 8 }}
                          onClick={() => handleShell('input keyevent 164')}>
                          <VolumeX size={16} /> Mute
                        </button>
                        <button className="btn btn-sm btn-ghost" style={{ justifyContent: 'flex-start', gap: 8 }}
                          onClick={() => handleShell('svc power stayon true && input keyevent 224')}>
                          <Sun size={16} /> Wake Screen
                        </button>
                        <button className="btn btn-sm btn-ghost" style={{ justifyContent: 'flex-start', gap: 8 }}
                          onClick={() => handleShell('reboot')}>
                          <RefreshCw size={16} /> Reboot Device
                        </button>
                      </div>
                    </div>
                  )}

                  {/* Camera Panel */}
                  {activePanel === 'camera' && (
                    <div>
                      <h4 style={{ margin: '0 0 12px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }}>
                        <Camera size={16} /> Camera
                      </h4>
                      <p style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 12 }}>
                        Cuttlefish provides a virtual camera that displays a test pattern or can use the host webcam feed.
                      </p>
                      <button className="btn btn-sm btn-ghost" style={{ justifyContent: 'flex-start', gap: 8, width: '100%', marginBottom: 6 }}
                        onClick={() => handleShell('am start -a android.media.action.IMAGE_CAPTURE')}>
                        <Camera size={16} /> Open Camera App
                      </button>
                      <button className="btn btn-sm btn-ghost" style={{ justifyContent: 'flex-start', gap: 8, width: '100%' }}
                        onClick={() => handleShell('screencap -p /sdcard/screenshot.png')}>
                        <Camera size={16} /> Take Screenshot
                      </button>
                    </div>
                  )}

                  {/* Biometrics Panel */}
                  {activePanel === 'biometrics' && (
                    <div>
                      <h4 style={{ margin: '0 0 12px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }}>
                        <Fingerprint size={16} /> Biometrics
                      </h4>
                      <p style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 12 }}>
                        Simulate fingerprint and face authentication events.
                      </p>
                      <button className="btn btn-sm btn-primary" style={{ width: '100%', marginBottom: 8 }}
                        onClick={() => handleShell('cmd fingerprint auth 1')}>
                        <Fingerprint size={14} /> Simulate Fingerprint Match
                      </button>
                      <button className="btn btn-sm btn-ghost" style={{ width: '100%', marginBottom: 8 }}
                        onClick={() => handleShell('cmd fingerprint auth 0')}>
                        <Fingerprint size={14} /> Simulate Fingerprint Fail
                      </button>
                      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 8 }}>
                        <p>Virtual biometrics allows testing:</p>
                        <ul style={{ paddingLeft: 16, margin: '4px 0' }}>
                          <li>Fingerprint enrollment</li>
                          <li>App-level biometric auth</li>
                          <li>Payment confirmation</li>
                          <li>Screen unlock</li>
                        </ul>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
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

export default CuttlefishScreen;
