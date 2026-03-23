import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDevices, deviceActions, sessionActions, virtualDeviceActions, twilioActions, genymotionActions, cloudPlatformActions, cuttlefishActions, remoteDeviceActions } from '../hooks/useApi';
import { DeviceConnectionState } from '../types';
import type { VirtualDeviceInfo, SmsMessage, GenymotionRecipe, GenymotionInstance, HardwareProfile, OsImage, CloudDevice, CloudPlatformStatus, RemotePhysicalDevice, RemoteBridgeStatus, CuttlefishProfile, CuttlefishImage, CuttlefishDevice, CuttlefishStatus } from '../types';
import {
  Smartphone, RefreshCw, Monitor, RotateCcw, Camera,
  Power, Copy, Search, AlertTriangle, Edit2, Eye, Cloud,
  Plus, Trash2, RotateCw, Play, Square, Phone, MessageSquare, Send, X, Zap, StopCircle, Cpu, HardDrive,
  Server, Layers, Database, Wifi, WifiOff, Usb
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
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [createName, setCreateName] = useState('');
  const [createRam, setCreateRam] = useState(3);
  const [createCpus, setCreateCpus] = useState(2);
  const [creating, setCreating] = useState(false);
  const [removing, setRemoving] = useState<string | null>(null);
  const [cloudDevices, setCloudDevices] = useState<VirtualDeviceInfo[]>([]);
  const [showCloudPanel, setShowCloudPanel] = useState(true);
  const [smsContainer, setSmsContainer] = useState<string | null>(null);
  const [smsMessages, setSmsMessages] = useState<SmsMessage[]>([]);
  const [smsLoading, setSmsLoading] = useState(false);
  const [sendTo, setSendTo] = useState('');
  const [sendBody, setSendBody] = useState('');
  const [sending, setSending] = useState(false);

  // Genymotion state
  const [genyInstances, setGenyInstances] = useState<GenymotionInstance[]>([]);
  const [genyRecipes, setGenyRecipes] = useState<GenymotionRecipe[]>([]);
  const [showGenyPanel, setShowGenyPanel] = useState(true);
  const [showGenyCreate, setShowGenyCreate] = useState(false);
  const [genyRecipeSearch, setGenyRecipeSearch] = useState('');
  const [genyStarting, setGenyStarting] = useState(false);
  const [genyStopping, setGenyStopping] = useState<string | null>(null);
  const [genyConfigured, setGenyConfigured] = useState(false);
  const [genyInstanceName, setGenyInstanceName] = useState('');

  // K8s Cloud Platform state
  const [k8sStatus, setK8sStatus] = useState<CloudPlatformStatus | null>(null);
  const [k8sDevices, setK8sDevices] = useState<CloudDevice[]>([]);
  const [k8sProfiles, setK8sProfiles] = useState<HardwareProfile[]>([]);
  const [k8sImages, setK8sImages] = useState<OsImage[]>([]);
  const [showK8sPanel, setShowK8sPanel] = useState(true);
  const [showK8sCreate, setShowK8sCreate] = useState(false);
  const [k8sProfileSearch, setK8sProfileSearch] = useState('');
  const [k8sSelectedProfile, setK8sSelectedProfile] = useState<string>('');
  const [k8sSelectedImage, setK8sSelectedImage] = useState<string>('');
  const [k8sDeviceName, setK8sDeviceName] = useState('');
  const [k8sEnableGpu, setK8sEnableGpu] = useState(true);
  const [k8sPersistStorage, setK8sPersistStorage] = useState(true);
  const [k8sCreating, setK8sCreating] = useState(false);
  const [k8sRemoving, setK8sRemoving] = useState<string | null>(null);

  // Cuttlefish VM Platform state (Genymotion-like)
  const [cfStatus, setCfStatus] = useState<CuttlefishStatus | null>(null);
  const [cfDevices, setCfDevices] = useState<CuttlefishDevice[]>([]);
  const [cfProfiles, setCfProfiles] = useState<CuttlefishProfile[]>([]);
  const [cfImages, setCfImages] = useState<CuttlefishImage[]>([]);
  const [showCfPanel, setShowCfPanel] = useState(true);
  const [showCfCreate, setShowCfCreate] = useState(false);
  const [cfProfileSearch, setCfProfileSearch] = useState('');
  const [cfSelectedProfile, setCfSelectedProfile] = useState<string>('');
  const [cfSelectedImage, setCfSelectedImage] = useState<string>('');
  const [cfDeviceName, setCfDeviceName] = useState('');
  const [cfEnableGpu, setCfEnableGpu] = useState(true);
  const [cfCreating, setCfCreating] = useState(false);
  const [cfRemoving, setCfRemoving] = useState<string | null>(null);

  // Remote Physical Devices state (ADB bridge from PC)
  const [remoteDevices, setRemoteDevices] = useState<RemotePhysicalDevice[]>([]);
  const [remoteBridgeStatus, setRemoteBridgeStatus] = useState<RemoteBridgeStatus | null>(null);
  const [showRemotePanel, setShowRemotePanel] = useState(true);
  const [remoteRefreshing, setRemoteRefreshing] = useState(false);

  const loadCloudDevices = useCallback(async () => {
    try {
      const data = await virtualDeviceActions.getAll();
      setCloudDevices(data);
    } catch { /* ignore */ }
  }, []);

  const loadGenyInstances = useCallback(async () => {
    try {
      const status = await genymotionActions.getStatus();
      setGenyConfigured(status.isConfigured);
      if (status.isConfigured) {
        const instances = await genymotionActions.getInstances();
        setGenyInstances(instances);
      }
    } catch { /* ignore */ }
  }, []);

  const loadGenyRecipes = useCallback(async () => {
    try {
      const recipes = await genymotionActions.getRecipes();
      setGenyRecipes(recipes);
    } catch { /* ignore */ }
  }, []);

  const loadK8sDevices = useCallback(async () => {
    try {
      const status = await cloudPlatformActions.getStatus();
      setK8sStatus(status);
      if (status.isConfigured) {
        const devices = await cloudPlatformActions.getDevices();
        setK8sDevices(devices);
      }
    } catch { /* ignore */ }
  }, []);

  const loadK8sProfiles = useCallback(async () => {
    try {
      const [profiles, images] = await Promise.all([
        cloudPlatformActions.getProfiles(),
        cloudPlatformActions.getImages(),
      ]);
      setK8sProfiles(profiles);
      setK8sImages(images);
      if (images.length > 0) setK8sSelectedImage(images.find(i => i.isDefault)?.id || images[0].id);
    } catch { /* ignore */ }
  }, []);

  useEffect(() => {
    loadCloudDevices();
    const id = setInterval(loadCloudDevices, 8000);
    return () => clearInterval(id);
  }, [loadCloudDevices]);

  useEffect(() => {
    loadGenyInstances();
    const id = setInterval(loadGenyInstances, 10000);
    return () => clearInterval(id);
  }, [loadGenyInstances]);

  useEffect(() => {
    loadK8sDevices();
    const id = setInterval(loadK8sDevices, 10000);
    return () => clearInterval(id);
  }, [loadK8sDevices]);

  const loadCfDevices = useCallback(async () => {
    try {
      const status = await cuttlefishActions.getStatus();
      setCfStatus(status);
      const devices = await cuttlefishActions.getDevices();
      setCfDevices(devices);
    } catch { /* ignore */ }
  }, []);

  const loadCfProfiles = useCallback(async () => {
    try {
      const [profiles, images] = await Promise.all([
        cuttlefishActions.getProfiles(),
        cuttlefishActions.getImages(),
      ]);
      setCfProfiles(profiles);
      setCfImages(images);
      if (images.length > 0) setCfSelectedImage(images.find(i => i.isDefault)?.id || images[0].id);
    } catch { /* ignore */ }
  }, []);

  useEffect(() => {
    loadCfDevices();
    const id = setInterval(loadCfDevices, 10000);
    return () => clearInterval(id);
  }, [loadCfDevices]);

  const loadRemoteDevices = useCallback(async () => {
    try {
      const [devs, status] = await Promise.all([
        remoteDeviceActions.getDevices(),
        remoteDeviceActions.getStatus(),
      ]);
      setRemoteDevices(devs);
      setRemoteBridgeStatus(status);
    } catch { /* ignore */ }
  }, []);

  useEffect(() => {
    loadRemoteDevices();
    const id = setInterval(loadRemoteDevices, 8000);
    return () => clearInterval(id);
  }, [loadRemoteDevices]);

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

  const handleCreate = async () => {
    setCreating(true);
    try {
      await virtualDeviceActions.create(createName || undefined, createRam, createCpus);
      setActionMsg({ type: 'success', text: 'Cloud device created successfully' });
      setTimeout(() => setActionMsg(null), 3000);
      setShowCreateModal(false);
      setCreateName('');
      await loadCloudDevices();
      await refreshDevices();
    } catch (e) {
      setActionMsg({ type: 'error', text: `Create failed: ${(e as Error).message}` });
    } finally {
      setCreating(false);
    }
  };

  const handleRemove = async (containerName: string) => {
    if (!confirm(`Remove cloud device "${containerName}"? This will delete the container and all its data.`)) return;
    setRemoving(containerName);
    try {
      await virtualDeviceActions.remove(containerName);
      setActionMsg({ type: 'success', text: `Removed ${containerName}` });
      setTimeout(() => setActionMsg(null), 3000);
      await loadCloudDevices();
      await refreshDevices();
    } catch (e) {
      setActionMsg({ type: 'error', text: `Remove failed: ${(e as Error).message}` });
    } finally {
      setRemoving(null);
    }
  };

  const handleRestart = async (containerName: string) => {
    await doAction(`Restart ${containerName}`, async () => {
      await virtualDeviceActions.restart(containerName);
      await loadCloudDevices();
      await refreshDevices();
    });
  };

  const openSmsInbox = async (containerName: string) => {
    setSmsContainer(containerName);
    setSmsLoading(true);
    try {
      const msgs = await twilioActions.getMessages(containerName);
      setSmsMessages(msgs);
    } catch { setSmsMessages([]); }
    setSmsLoading(false);
  };

  const handleSendSms = async () => {
    if (!smsContainer || !sendTo || !sendBody) return;
    setSending(true);
    try {
      await twilioActions.sendSms(smsContainer, sendTo, sendBody);
      setSendTo('');
      setSendBody('');
      const msgs = await twilioActions.getMessages(smsContainer);
      setSmsMessages(msgs);
      setActionMsg({ type: 'success', text: 'SMS sent' });
      setTimeout(() => setActionMsg(null), 3000);
    } catch (e) {
      setActionMsg({ type: 'error', text: `Send failed: ${(e as Error).message}` });
    }
    setSending(false);
  };

  if (loading && devices.length === 0) {
    return <div className="loading-overlay"><div className="spinner" /> Loading devices...</div>;
  }

  return (
    <div>
      <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h2>Devices</h2>
          <p>{devices.length} device(s) detected | {cloudDevices.length} cloud container(s)</p>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-primary" onClick={async () => {
            setConnectingCloud(true);
            try {
              await virtualDeviceActions.connectAll();
              await refreshDevices();
              await loadCloudDevices();
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

      {/* Cloud Device Management Panel */}
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, padding: 16, marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: showCloudPanel ? 12 : 0 }}>
          <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8, fontSize: 15 }}>
            <Cloud size={18} style={{ color: '#a6e3a1' }} />
            Cloud Devices (Virtual Android)
            <span style={{ fontSize: 12, color: 'var(--text-muted)', fontWeight: 'normal' }}>
              {cloudDevices.length} container(s)
            </span>
          </h3>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-sm btn-success" onClick={() => setShowCreateModal(true)}>
              <Plus size={12} /> Create Device
            </button>
            <button className="btn btn-sm btn-ghost" onClick={() => setShowCloudPanel(!showCloudPanel)}>
              {showCloudPanel ? 'Collapse' : 'Expand'}
            </button>
          </div>
        </div>

        {showCloudPanel && (
          cloudDevices.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '20px 0', color: 'var(--text-muted)' }}>
              <Cloud size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
              <p style={{ margin: 0 }}>No cloud devices yet. Click "Create Device" to add a virtual Android.</p>
            </div>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: 12 }}>
              {cloudDevices.map(cd => (
                <div key={cd.containerName} style={{
                  background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: 8,
                  padding: 12, display: 'flex', flexDirection: 'column', gap: 8
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <Cloud size={16} style={{ color: cd.connected ? '#a6e3a1' : '#666' }} />
                      <strong style={{ fontSize: 13 }}>{cd.friendlyName}</strong>
                    </div>
                    <span className={`badge ${cd.connected ? 'online' : 'offline'}`} style={{ fontSize: 10 }}>
                      <span className="badge-dot" />
                      {cd.connected ? 'Online' : cd.containerStatus.includes('Up') ? 'Running' : 'Stopped'}
                    </span>
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', display: 'grid', gridTemplateColumns: '80px 1fr', gap: '2px 8px' }}>
                    <span>Container:</span><span style={{ fontFamily: 'monospace' }}>{cd.containerName}</span>
                    <span>ADB Port:</span><span style={{ fontFamily: 'monospace' }}>{cd.port}</span>
                    <span>Serial:</span><span style={{ fontFamily: 'monospace' }}>{cd.serial}</span>
                    <span>Android:</span><span>{cd.androidVersion}</span>
                    <span>Status:</span><span>{cd.containerStatus}</span>
                    {cd.phoneNumber && (
                      <><span>Phone:</span><span style={{ fontFamily: 'monospace', color: '#a6e3a1', fontWeight: 'bold' }}>
                        <Phone size={10} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 3 }} />
                        {cd.phoneNumber}
                      </span></>
                    )}
                  </div>
                  <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                    {cd.connected ? (
                      <button className="btn btn-sm btn-primary"
                        onClick={() => navigate(`/devices/${cd.serial}/screen?container=${cd.containerName}`)}
                        title="View screen">
                        <Eye size={10} /> View Screen
                      </button>
                    ) : (
                      <button className="btn btn-sm btn-primary"
                        onClick={async () => {
                          await doAction('Connect', async () => {
                            await virtualDeviceActions.connect('localhost', cd.port, cd.friendlyName);
                            await loadCloudDevices();
                            await refreshDevices();
                          });
                        }}>
                        <Play size={10} /> Connect
                      </button>
                    )}
                    <button className="btn btn-sm btn-ghost"
                      onClick={() => handleRestart(cd.containerName)}
                      title="Restart container">
                      <RotateCw size={10} /> Restart
                    </button>
                    {cd.phoneNumber && (
                      <button className="btn btn-sm btn-ghost"
                        style={{ color: '#89b4fa' }}
                        onClick={() => openSmsInbox(cd.containerName)}
                        title="View SMS inbox">
                        <MessageSquare size={10} /> SMS
                      </button>
                    )}
                    <button className="btn btn-sm btn-ghost"
                      style={{ color: '#f38ba8' }}
                      onClick={() => handleRemove(cd.containerName)}
                      disabled={removing === cd.containerName}
                      title="Remove container permanently">
                      <Trash2 size={10} /> {removing === cd.containerName ? 'Removing...' : 'Remove'}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )
        )}
      </div>

      {/* SMS Inbox Modal */}
      {smsContainer && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center',
          zIndex: 1000
        }} onClick={() => setSmsContainer(null)}>
          <div style={{
            background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12,
            padding: 24, width: 500, maxWidth: '95vw', maxHeight: '80vh', display: 'flex', flexDirection: 'column'
          }} onClick={e => e.stopPropagation()}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
                <MessageSquare size={18} /> SMS Inbox - {smsContainer}
              </h3>
              <button className="btn btn-sm btn-ghost" onClick={() => setSmsContainer(null)}>
                <X size={14} />
              </button>
            </div>

            {/* Send SMS form */}
            <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
              <input
                value={sendTo}
                onChange={e => setSendTo(e.target.value)}
                placeholder="To: +1234567890"
                style={{ flex: '0 0 140px', fontSize: 12 }}
              />
              <input
                value={sendBody}
                onChange={e => setSendBody(e.target.value)}
                placeholder="Message..."
                style={{ flex: 1, fontSize: 12 }}
                onKeyDown={e => e.key === 'Enter' && handleSendSms()}
              />
              <button className="btn btn-sm btn-primary" onClick={handleSendSms} disabled={sending || !sendTo || !sendBody}>
                <Send size={12} /> {sending ? '...' : 'Send'}
              </button>
            </div>

            {/* Messages list */}
            <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 8 }}>
              {smsLoading ? (
                <div style={{ textAlign: 'center', padding: 20, color: 'var(--text-muted)' }}>Loading messages...</div>
              ) : smsMessages.length === 0 ? (
                <div style={{ textAlign: 'center', padding: 20, color: 'var(--text-muted)' }}>
                  <MessageSquare size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
                  <p style={{ margin: 0 }}>No messages yet. Send an SMS to this number to see it here.</p>
                </div>
              ) : (
                smsMessages.map(msg => (
                  <div key={msg.id} style={{
                    padding: '8px 12px', borderRadius: 8,
                    background: msg.direction === 'inbound' ? 'var(--bg)' : '#1e3a5f',
                    border: '1px solid var(--border)',
                    alignSelf: msg.direction === 'inbound' ? 'flex-start' : 'flex-end',
                    maxWidth: '85%'
                  }}>
                    <div style={{ fontSize: 10, color: 'var(--text-muted)', marginBottom: 4 }}>
                      {msg.direction === 'inbound' ? `From: ${msg.fromNumber}` : `To: ${msg.toNumber}`}
                      {' '}&middot;{' '}
                      {new Date(msg.receivedAt).toLocaleString()}
                    </div>
                    <div style={{ fontSize: 13 }}>{msg.body}</div>
                  </div>
                ))
              )}
            </div>

            <div style={{ marginTop: 12, display: 'flex', justifyContent: 'flex-end' }}>
              <button className="btn btn-sm btn-ghost" onClick={async () => {
                setSmsLoading(true);
                try {
                  const msgs = await twilioActions.getMessages(smsContainer);
                  setSmsMessages(msgs);
                } catch { /* ignore */ }
                setSmsLoading(false);
              }}>
                <RefreshCw size={12} /> Refresh
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Genymotion Cloud Devices Panel */}
      {genyConfigured && (
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, padding: 16, marginBottom: 20 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: showGenyPanel ? 12 : 0 }}>
            <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8, fontSize: 15 }}>
              <Zap size={18} style={{ color: '#f9e2af' }} />
              Genymotion Cloud Devices
              <span style={{ fontSize: 12, color: 'var(--text-muted)', fontWeight: 'normal' }}>
                {genyInstances.length} instance(s)
              </span>
            </h3>
            <div style={{ display: 'flex', gap: 8 }}>
              <button className="btn btn-sm btn-success" onClick={() => {
                setShowGenyCreate(true);
                if (genyRecipes.length === 0) loadGenyRecipes();
              }}>
                <Plus size={12} /> New Device
              </button>
              <button className="btn btn-sm btn-ghost" onClick={loadGenyInstances}>
                <RefreshCw size={12} />
              </button>
              <button className="btn btn-sm btn-ghost" onClick={() => setShowGenyPanel(!showGenyPanel)}>
                {showGenyPanel ? 'Collapse' : 'Expand'}
              </button>
            </div>
          </div>

          {showGenyPanel && (
            genyInstances.length === 0 ? (
              <div style={{ textAlign: 'center', padding: '20px 0', color: 'var(--text-muted)' }}>
                <Zap size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
                <p style={{ margin: 0 }}>No Genymotion devices running. Click "New Device" to start one.</p>
              </div>
            ) : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 12 }}>
                {genyInstances.map(gi => (
                  <div key={gi.uuid} style={{
                    background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: 8,
                    padding: 12, display: 'flex', flexDirection: 'column', gap: 8
                  }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <Zap size={16} style={{ color: gi.state === 'ONLINE' ? '#a6e3a1' : gi.state === 'CREATING' || gi.state === 'BOOTING' ? '#f9e2af' : '#666' }} />
                        <strong style={{ fontSize: 13 }}>{gi.name}</strong>
                      </div>
                      <span className={`badge ${gi.state === 'ONLINE' ? 'online' : gi.state === 'CREATING' || gi.state === 'BOOTING' ? 'warning' : 'offline'}`} style={{ fontSize: 10 }}>
                        <span className="badge-dot" />
                        {gi.state}
                      </span>
                    </div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', display: 'grid', gridTemplateColumns: '80px 1fr', gap: '2px 8px' }}>
                      <span>Recipe:</span><span>{gi.recipeName}</span>
                      <span>Android:</span><span>{gi.androidVersion}</span>
                      <span>CPU/RAM:</span><span>{gi.cpuCount} cores / {gi.ramMb >= 1024 ? `${(gi.ramMb / 1024).toFixed(0)} GB` : `${gi.ramMb} MB`}</span>
                      <span>Screen:</span><span>{gi.screenWidth}x{gi.screenHeight} @ {gi.screenDensity}dpi</span>
                      {gi.webrtcUrl && <><span>WebRTC:</span><span style={{ fontFamily: 'monospace', fontSize: 10 }}>{gi.streamerFqdn}</span></>}
                    </div>
                    <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                      {gi.state === 'ONLINE' && gi.webrtcUrl && (
                        <button className="btn btn-sm btn-primary"
                          onClick={() => window.location.href = `/genymotion/${gi.uuid}/screen`}
                          title="Open Genymotion device screen viewer">
                          <Eye size={10} /> View Screen
                        </button>
                      )}
                      <button className="btn btn-sm btn-ghost"
                        style={{ color: '#f38ba8' }}
                        onClick={async () => {
                          if (!confirm(`Stop Genymotion device "${gi.name}"? This will destroy the instance.`)) return;
                          setGenyStopping(gi.uuid);
                          try {
                            await genymotionActions.stopInstance(gi.uuid);
                            setActionMsg({ type: 'success', text: `Stopped ${gi.name}` });
                            setTimeout(() => setActionMsg(null), 3000);
                            await loadGenyInstances();
                          } catch (e) {
                            setActionMsg({ type: 'error', text: `Stop failed: ${(e as Error).message}` });
                          } finally {
                            setGenyStopping(null);
                          }
                        }}
                        disabled={genyStopping === gi.uuid}>
                        <StopCircle size={10} /> {genyStopping === gi.uuid ? 'Stopping...' : 'Stop'}
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )
          )}
        </div>
      )}

      {/* Genymotion Create Device Modal */}
      {showGenyCreate && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center',
          zIndex: 1000
        }} onClick={() => !genyStarting && setShowGenyCreate(false)}>
          <div style={{
            background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12,
            padding: 24, width: 600, maxWidth: '95vw', maxHeight: '85vh', display: 'flex', flexDirection: 'column'
          }} onClick={e => e.stopPropagation()}>
            <h3 style={{ margin: '0 0 16px', display: 'flex', alignItems: 'center', gap: 8 }}>
              <Zap size={18} style={{ color: '#f9e2af' }} /> Start Genymotion Device
            </h3>

            <div style={{ marginBottom: 12 }}>
              <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>Instance Name (optional)</label>
              <input
                value={genyInstanceName}
                onChange={e => setGenyInstanceName(e.target.value)}
                placeholder="e.g., my-test-device"
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ marginBottom: 8 }}>
              <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>Select a Device Recipe</label>
              <input
                value={genyRecipeSearch}
                onChange={e => setGenyRecipeSearch(e.target.value)}
                placeholder="Search recipes... (e.g., Samsung, Pixel, Android 14)"
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 6, minHeight: 200, maxHeight: '50vh' }}>
              {genyRecipes.length === 0 ? (
                <div style={{ textAlign: 'center', padding: 20, color: 'var(--text-muted)' }}>Loading recipes...</div>
              ) : (
                genyRecipes
                  .filter(r => {
                    const q = genyRecipeSearch.toLowerCase();
                    return !q || r.name.toLowerCase().includes(q) || r.androidVersion.toLowerCase().includes(q) || r.description.toLowerCase().includes(q);
                  })
                  .slice(0, 30)
                  .map(recipe => (
                    <div key={recipe.uuid} style={{
                      padding: '10px 12px', borderRadius: 8, border: '1px solid var(--border)',
                      background: 'var(--bg)', cursor: genyStarting ? 'default' : 'pointer',
                      display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                      transition: 'border-color 0.15s',
                    }}
                      onMouseEnter={e => { if (!genyStarting) (e.currentTarget as HTMLDivElement).style.borderColor = '#a6e3a1'; }}
                      onMouseLeave={e => { (e.currentTarget as HTMLDivElement).style.borderColor = 'var(--border)'; }}
                      onClick={async () => {
                        if (genyStarting) return;
                        setGenyStarting(true);
                        try {
                          const result = await genymotionActions.startInstance(recipe.uuid, genyInstanceName || undefined);
                          setActionMsg({ type: 'success', text: `Started ${result.instance.name}${result.phoneNumber ? ` with phone ${result.phoneNumber}` : ''}` });
                          setTimeout(() => setActionMsg(null), 5000);
                          setShowGenyCreate(false);
                          setGenyInstanceName('');
                          setGenyRecipeSearch('');
                          await loadGenyInstances();
                        } catch (e) {
                          setActionMsg({ type: 'error', text: `Start failed: ${(e as Error).message}` });
                        } finally {
                          setGenyStarting(false);
                        }
                      }}>
                      <div>
                        <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 2 }}>{recipe.name}</div>
                        <div style={{ fontSize: 11, color: 'var(--text-muted)', display: 'flex', gap: 12 }}>
                          <span>Android {recipe.androidVersion}</span>
                          <span><Cpu size={10} style={{ display: 'inline', verticalAlign: 'middle' }} /> {recipe.cpuCount} cores</span>
                          <span><HardDrive size={10} style={{ display: 'inline', verticalAlign: 'middle' }} /> {recipe.ramMb >= 1024 ? `${(recipe.ramMb / 1024).toFixed(0)} GB` : `${recipe.ramMb} MB`}</span>
                          <span>{recipe.screenWidth}x{recipe.screenHeight}</span>
                        </div>
                      </div>
                      <button className="btn btn-sm btn-success" disabled={genyStarting} style={{ flexShrink: 0 }}>
                        {genyStarting ? 'Starting...' : 'Start'}
                      </button>
                    </div>
                  ))
              )}
            </div>

            <div style={{ marginTop: 12, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                {genyRecipes.length} recipes available | Powered by Genymotion SaaS
              </span>
              <button className="btn btn-ghost" onClick={() => setShowGenyCreate(false)} disabled={genyStarting}>
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      {/* K8s Cloud Platform Panel */}
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, padding: 16, marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: showK8sPanel ? 12 : 0 }}>
          <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8, fontSize: 15 }}>
            <Server size={18} style={{ color: '#89b4fa' }} />
            K8s Cloud Platform
            <span style={{ fontSize: 12, color: 'var(--text-muted)', fontWeight: 'normal' }}>
              {k8sDevices.length} device(s) {k8sStatus?.clusterReachable ? '' : '| Cluster not connected'}
            </span>
          </h3>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-sm btn-success" onClick={() => {
              setShowK8sCreate(true);
              if (k8sProfiles.length === 0) loadK8sProfiles();
            }}>
              <Plus size={12} /> New Device
            </button>
            <button className="btn btn-sm btn-ghost" onClick={loadK8sDevices}>
              <RefreshCw size={12} />
            </button>
            <button className="btn btn-sm btn-ghost" onClick={() => setShowK8sPanel(!showK8sPanel)}>
              {showK8sPanel ? 'Collapse' : 'Expand'}
            </button>
          </div>
        </div>

        {showK8sPanel && (
          k8sDevices.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '20px 0', color: 'var(--text-muted)' }}>
              <Server size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
              <p style={{ margin: 0 }}>
                {k8sStatus?.isConfigured
                  ? 'No K8s devices running. Click "New Device" to create one.'
                  : 'K8s cluster not configured. Set KUBECONFIG or add cluster credentials in Settings.'}
              </p>
              {k8sStatus && k8sStatus.isConfigured && (
                <p style={{ margin: '8px 0 0', fontSize: 11 }}>
                  Cluster: {k8sStatus.kubernetesVersion} | Nodes: {k8sStatus.readyNodes}/{k8sStatus.totalNodes}
                  {k8sStatus.gpuAvailable && ` | GPU: ${k8sStatus.gpuModel}`}
                </p>
              )}
            </div>
          ) : (
            <>
              {k8sStatus && (
                <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10, display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                  <span><Layers size={11} style={{ verticalAlign: 'middle' }} /> K8s {k8sStatus.kubernetesVersion}</span>
                  <span><Server size={11} style={{ verticalAlign: 'middle' }} /> {k8sStatus.readyNodes}/{k8sStatus.totalNodes} nodes</span>
                  <span><Cpu size={11} style={{ verticalAlign: 'middle' }} /> {Math.round(k8sStatus.resources.usedCpuMillicores / 10) / 100}/{Math.round(k8sStatus.resources.totalCpuMillicores / 10) / 100} CPU</span>
                  <span><HardDrive size={11} style={{ verticalAlign: 'middle' }} /> {Math.round(k8sStatus.resources.usedMemoryMb / 1024 * 10) / 10}/{Math.round(k8sStatus.resources.totalMemoryMb / 1024 * 10) / 10} GB RAM</span>
                  {k8sStatus.gpuAvailable && <span><Database size={11} style={{ verticalAlign: 'middle' }} /> GPU: {k8sStatus.gpuModel}</span>}
                </div>
              )}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 12 }}>
                {k8sDevices.map(kd => (
                  <div key={kd.id} style={{
                    background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: 8,
                    padding: 12, display: 'flex', flexDirection: 'column', gap: 8
                  }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <Server size={16} style={{ color: kd.state === 'Running' ? '#a6e3a1' : kd.state === 'Pending' ? '#f9e2af' : '#666' }} />
                        <strong style={{ fontSize: 13 }}>{kd.name}</strong>
                      </div>
                      <span className={`badge ${kd.state === 'Running' ? 'online' : kd.state === 'Pending' ? 'warning' : 'offline'}`} style={{ fontSize: 10 }}>
                        <span className="badge-dot" />
                        {kd.state}
                      </span>
                    </div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', display: 'grid', gridTemplateColumns: '80px 1fr', gap: '2px 8px' }}>
                      <span>Device:</span><span>{kd.brand} {kd.model}</span>
                      <span>Android:</span><span>{kd.androidVersion}</span>
                      <span>CPU/RAM:</span><span>{kd.cpuCores} cores / {kd.ramMb >= 1024 ? `${(kd.ramMb / 1024).toFixed(0)} GB` : `${kd.ramMb} MB`}</span>
                      <span>Screen:</span><span>{kd.screenWidth}x{kd.screenHeight} @ {kd.screenDpi}dpi</span>
                      <span>Storage:</span><span>{kd.storageGb} GB {kd.persistentStorage ? '(persistent)' : ''}</span>
                      {kd.gpuAccelerated && <><span>GPU:</span><span style={{ color: '#a6e3a1' }}>Accelerated</span></>}
                      {kd.hasGapps && <><span>GApps:</span><span style={{ color: '#a6e3a1' }}>Installed</span></>}
                      {kd.phoneNumber && (
                        <><span>Phone:</span><span style={{ fontFamily: 'monospace', color: '#a6e3a1', fontWeight: 'bold' }}>
                          <Phone size={10} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 3 }} />
                          {kd.phoneNumber}
                        </span></>
                      )}
                      <span>Uptime:</span><span>{kd.uptimeSeconds > 3600 ? `${Math.floor(kd.uptimeSeconds / 3600)}h ${Math.floor((kd.uptimeSeconds % 3600) / 60)}m` : `${Math.floor(kd.uptimeSeconds / 60)}m`}</span>
                    </div>
                    <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                      {kd.state === 'Running' && kd.streamUrl && (
                        <button className="btn btn-sm btn-primary"
                          onClick={() => window.location.href = `/cloud-device/${kd.id}/screen`}
                          title="View device screen via WebRTC">
                          <Eye size={10} /> View Screen
                        </button>
                      )}
                      <button className="btn btn-sm btn-ghost"
                        onClick={async () => {
                          try {
                            await cloudPlatformActions.restartDevice(kd.id);
                            setActionMsg({ type: 'success', text: `Restarting ${kd.name}` });
                            setTimeout(() => setActionMsg(null), 3000);
                            await loadK8sDevices();
                          } catch (e) {
                            setActionMsg({ type: 'error', text: `Restart failed: ${(e as Error).message}` });
                          }
                        }}>
                        <RotateCw size={10} /> Restart
                      </button>
                      {kd.phoneNumber && (
                        <button className="btn btn-sm btn-ghost"
                          onClick={() => openSmsInbox(kd.name)}>
                          <MessageSquare size={10} /> SMS
                        </button>
                      )}
                      <button className="btn btn-sm btn-ghost"
                        style={{ color: '#f38ba8' }}
                        onClick={async () => {
                          if (!confirm(`Remove K8s device "${kd.name}"? This will delete the pod and data.`)) return;
                          setK8sRemoving(kd.id);
                          try {
                            await cloudPlatformActions.removeDevice(kd.id);
                            setActionMsg({ type: 'success', text: `Removed ${kd.name}` });
                            setTimeout(() => setActionMsg(null), 3000);
                            await loadK8sDevices();
                          } catch (e) {
                            setActionMsg({ type: 'error', text: `Remove failed: ${(e as Error).message}` });
                          } finally {
                            setK8sRemoving(null);
                          }
                        }}
                        disabled={k8sRemoving === kd.id}>
                        <Trash2 size={10} /> {k8sRemoving === kd.id ? 'Removing...' : 'Remove'}
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </>
          )
        )}
      </div>

      {/* K8s Create Device Modal */}
      {showK8sCreate && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center',
          zIndex: 1000
        }} onClick={() => !k8sCreating && setShowK8sCreate(false)}>
          <div style={{
            background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12,
            padding: 24, width: 650, maxWidth: '95vw', maxHeight: '85vh', display: 'flex', flexDirection: 'column'
          }} onClick={e => e.stopPropagation()}>
            <h3 style={{ margin: '0 0 16px', display: 'flex', alignItems: 'center', gap: 8 }}>
              <Server size={18} style={{ color: '#89b4fa' }} /> Create K8s Cloud Device
            </h3>

            <div style={{ marginBottom: 12 }}>
              <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>Device Name</label>
              <input
                value={k8sDeviceName}
                onChange={e => setK8sDeviceName(e.target.value)}
                placeholder="e.g., my-galaxy-s25"
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 12 }}>
              <div>
                <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>Android Version</label>
                <select value={k8sSelectedImage} onChange={e => setK8sSelectedImage(e.target.value)}
                  style={{ width: '100%', padding: '8px 12px', background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--text)' }}>
                  {k8sImages.map(img => (
                    <option key={img.id} value={img.id}>{img.name} {img.hasGapps ? '(GApps)' : ''}</option>
                  ))}
                </select>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6, paddingTop: 22 }}>
                <label style={{ fontSize: 12, display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
                  <input type="checkbox" checked={k8sEnableGpu} onChange={e => setK8sEnableGpu(e.target.checked)} />
                  GPU Acceleration
                </label>
                <label style={{ fontSize: 12, display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
                  <input type="checkbox" checked={k8sPersistStorage} onChange={e => setK8sPersistStorage(e.target.checked)} />
                  Persistent Storage
                </label>
              </div>
            </div>

            <div style={{ marginBottom: 8 }}>
              <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>Select Hardware Profile</label>
              <input
                value={k8sProfileSearch}
                onChange={e => setK8sProfileSearch(e.target.value)}
                placeholder="Search profiles... (e.g., Samsung, Pixel, Galaxy S25)"
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 6, minHeight: 200, maxHeight: '45vh' }}>
              {k8sProfiles.length === 0 ? (
                <div style={{ textAlign: 'center', padding: 20, color: 'var(--text-muted)' }}>Loading hardware profiles...</div>
              ) : (
                k8sProfiles
                  .filter(p => {
                    const q = k8sProfileSearch.toLowerCase();
                    return !q || p.name.toLowerCase().includes(q) || p.brand.toLowerCase().includes(q) || p.model.toLowerCase().includes(q) || p.category.toLowerCase().includes(q);
                  })
                  .map(profile => (
                    <div key={profile.id} style={{
                      padding: '10px 12px', borderRadius: 8,
                      border: k8sSelectedProfile === profile.id ? '2px solid #89b4fa' : '1px solid var(--border)',
                      background: k8sSelectedProfile === profile.id ? 'rgba(137,180,250,0.1)' : 'var(--bg)',
                      cursor: k8sCreating ? 'default' : 'pointer',
                      display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                      transition: 'border-color 0.15s',
                    }}
                      onClick={() => !k8sCreating && setK8sSelectedProfile(profile.id)}
                      onMouseEnter={e => { if (!k8sCreating && k8sSelectedProfile !== profile.id) (e.currentTarget as HTMLDivElement).style.borderColor = '#89b4fa'; }}
                      onMouseLeave={e => { if (k8sSelectedProfile !== profile.id) (e.currentTarget as HTMLDivElement).style.borderColor = 'var(--border)'; }}
                    >
                      <div>
                        <div style={{ fontWeight: 600, fontSize: 13, display: 'flex', alignItems: 'center', gap: 6 }}>
                          <Smartphone size={14} style={{ color: '#89b4fa' }} />
                          {profile.name}
                          <span style={{ fontSize: 10, padding: '1px 6px', borderRadius: 4, background: 'rgba(137,180,250,0.2)', color: '#89b4fa' }}>
                            {profile.category}
                          </span>
                        </div>
                        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
                          {profile.cpuCores} cores | {profile.ramMb >= 1024 ? `${(profile.ramMb / 1024).toFixed(0)} GB` : `${profile.ramMb} MB`} RAM | {profile.screenWidth}x{profile.screenHeight} @ {profile.screenDpi}dpi | {profile.storageGb} GB
                        </div>
                      </div>
                      {k8sSelectedProfile === profile.id && (
                        <span style={{ color: '#89b4fa', fontWeight: 'bold', fontSize: 16 }}>&#10003;</span>
                      )}
                    </div>
                  ))
              )}
            </div>

            <div style={{ marginTop: 12, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                {k8sProfiles.length} profiles available | Auto-assigns US phone number via Twilio
              </span>
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="btn btn-ghost" onClick={() => setShowK8sCreate(false)} disabled={k8sCreating}>
                  Cancel
                </button>
                <button className="btn btn-success" disabled={k8sCreating || !k8sSelectedProfile}
                  onClick={async () => {
                    setK8sCreating(true);
                    try {
                      const result = await cloudPlatformActions.createDevice({
                        name: k8sDeviceName || undefined as unknown as string,
                        hardwareProfileId: k8sSelectedProfile,
                        osImageId: k8sSelectedImage,
                        assignPhoneNumber: true,
                        persistentStorage: k8sPersistStorage,
                        enableGpu: k8sEnableGpu,
                      });
                      setActionMsg({ type: 'success', text: `Created ${result.device.name} (${result.device.brand} ${result.device.model})` });
                      setTimeout(() => setActionMsg(null), 5000);
                      setShowK8sCreate(false);
                      setK8sDeviceName('');
                      setK8sSelectedProfile('');
                      await loadK8sDevices();
                    } catch (e) {
                      setActionMsg({ type: 'error', text: `Create failed: ${(e as Error).message}` });
                    } finally {
                      setK8sCreating(false);
                    }
                  }}>
                  {k8sCreating ? 'Creating...' : 'Create Device'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Cuttlefish VM Platform Panel (Genymotion-like) */}
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, padding: 16, marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: showCfPanel ? 12 : 0 }}>
          <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8, fontSize: 15 }}>
            <Cpu size={18} style={{ color: '#cba6f7' }} />
            Cuttlefish VM Platform
            <span style={{ fontSize: 12, color: 'var(--text-muted)', fontWeight: 'normal' }}>
              {cfDevices.length} device(s) {cfStatus?.isAvailable ? '| QEMU/KVM' : '| Not configured'}
            </span>
          </h3>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-sm btn-success" onClick={() => {
              setShowCfCreate(true);
              if (cfProfiles.length === 0) loadCfProfiles();
            }}>
              <Plus size={12} /> New VM
            </button>
            <button className="btn btn-sm btn-ghost" onClick={loadCfDevices}>
              <RefreshCw size={12} />
            </button>
            <button className="btn btn-sm btn-ghost" onClick={() => setShowCfPanel(!showCfPanel)}>
              {showCfPanel ? 'Collapse' : 'Expand'}
            </button>
          </div>
        </div>

        {showCfPanel && (
          cfDevices.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '20px 0', color: 'var(--text-muted)' }}>
              <Cpu size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
              <p style={{ margin: 0 }}>
                {cfStatus?.isAvailable
                  ? 'No Cuttlefish VMs running. Click "New VM" to create one with full Android kernel + virtual hardware.'
                  : 'Cuttlefish host not configured. Run cuttlefish-setup.sh on your baremetal server.'}
              </p>
              {cfStatus && (
                <p style={{ margin: '8px 0 0', fontSize: 11 }}>
                  Host: {cfStatus.hostAddress || 'localhost'} | Docker: {cfStatus.dockerVersion || 'N/A'}
                  {cfStatus.kvmAvailable && ' | KVM: Available'}
                </p>
              )}
            </div>
          ) : (
            <>
              {cfStatus && (
                <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10, display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                  <span><Cpu size={11} style={{ verticalAlign: 'middle' }} /> QEMU/KVM</span>
                  <span><Server size={11} style={{ verticalAlign: 'middle' }} /> {cfStatus.hostAddress || 'localhost'}</span>
                  <span><HardDrive size={11} style={{ verticalAlign: 'middle' }} /> Docker {cfStatus.dockerVersion || 'N/A'}</span>
                  {cfStatus.kvmAvailable && <span style={{ color: '#a6e3a1' }}>KVM Available</span>}
                  <span>{cfDevices.filter(d => d.state === 'Running').length}/{cfDevices.length} running</span>
                </div>
              )}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 12 }}>
                {cfDevices.map(cd => (
                  <div key={cd.id} style={{
                    background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: 8,
                    padding: 12, display: 'flex', flexDirection: 'column', gap: 8
                  }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <Cpu size={16} style={{ color: cd.state === 'Running' ? '#a6e3a1' : cd.state === 'Starting' ? '#f9e2af' : '#666' }} />
                        <strong style={{ fontSize: 13 }}>{cd.name}</strong>
                      </div>
                      <span className={`badge ${cd.state === 'Running' ? 'online' : cd.state === 'Starting' ? 'warning' : 'offline'}`} style={{ fontSize: 10 }}>
                        <span className="badge-dot" />
                        {cd.state}
                      </span>
                    </div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', display: 'grid', gridTemplateColumns: '80px 1fr', gap: '2px 8px' }}>
                      <span>Device:</span><span>{cd.brand} {cd.model}</span>
                      <span>Android:</span><span>{cd.androidVersion}</span>
                      <span>CPU/RAM:</span><span>{cd.cpuCores} cores / {cd.ramMb >= 1024 ? `${(cd.ramMb / 1024).toFixed(0)} GB` : `${cd.ramMb} MB`}</span>
                      <span>Screen:</span><span>{cd.screenWidth}x{cd.screenHeight} @ {cd.screenDpi}dpi</span>
                      <span>Storage:</span><span>{cd.storageGb} GB</span>
                      {cd.hasGapps && <><span>GApps:</span><span style={{ color: '#a6e3a1' }}>Installed</span></>}
                      {cd.hasModem && <><span>Modem:</span><span style={{ color: '#a6e3a1' }}>Virtual RIL</span></>}
                      {cd.hasGps && <><span>GPS:</span><span style={{ color: '#a6e3a1' }}>Virtual</span></>}
                      {cd.hasSensors && <><span>Sensors:</span><span style={{ color: '#a6e3a1' }}>Full suite</span></>}
                      {cd.hasCamera && <><span>Camera:</span><span style={{ color: '#a6e3a1' }}>Virtual</span></>}
                      {cd.hasBiometrics && <><span>Biometrics:</span><span style={{ color: '#a6e3a1' }}>Fingerprint</span></>}
                      {cd.phoneNumber && (
                        <><span>Phone:</span><span style={{ fontFamily: 'monospace', color: '#a6e3a1', fontWeight: 'bold' }}>
                          <Phone size={10} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 3 }} />
                          {cd.phoneNumber}
                        </span></>
                      )}
                      <span>Uptime:</span><span>{cd.uptimeSeconds > 3600 ? `${Math.floor(cd.uptimeSeconds / 3600)}h ${Math.floor((cd.uptimeSeconds % 3600) / 60)}m` : `${Math.floor(cd.uptimeSeconds / 60)}m`}</span>
                    </div>
                    <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                      {cd.state === 'Running' && cd.webRtcUrl && (
                        <button className="btn btn-sm btn-primary"
                          onClick={() => window.location.href = `/cuttlefish/${cd.id}/screen`}
                          title="View device screen via WebRTC with full controls">
                          <Eye size={10} /> View Screen
                        </button>
                      )}
                      <button className="btn btn-sm btn-ghost"
                        onClick={async () => {
                          try {
                            await cuttlefishActions.restartDevice(cd.id);
                            setActionMsg({ type: 'success', text: `Restarting ${cd.name}` });
                            setTimeout(() => setActionMsg(null), 3000);
                            await loadCfDevices();
                          } catch (e) {
                            setActionMsg({ type: 'error', text: `Restart failed: ${(e as Error).message}` });
                          }
                        }}>
                        <RotateCw size={10} /> Restart
                      </button>
                      {cd.phoneNumber && (
                        <button className="btn btn-sm btn-ghost"
                          onClick={() => openSmsInbox(cd.name)}>
                          <MessageSquare size={10} /> SMS
                        </button>
                      )}
                      <button className="btn btn-sm btn-ghost"
                        style={{ color: '#f38ba8' }}
                        onClick={async () => {
                          if (!confirm(`Remove Cuttlefish VM "${cd.name}"? This will delete the container and data.`)) return;
                          setCfRemoving(cd.id);
                          try {
                            await cuttlefishActions.removeDevice(cd.id);
                            setActionMsg({ type: 'success', text: `Removed ${cd.name}` });
                            setTimeout(() => setActionMsg(null), 3000);
                            await loadCfDevices();
                          } catch (e) {
                            setActionMsg({ type: 'error', text: `Remove failed: ${(e as Error).message}` });
                          } finally {
                            setCfRemoving(null);
                          }
                        }}
                        disabled={cfRemoving === cd.id}>
                        <Trash2 size={10} /> {cfRemoving === cd.id ? 'Removing...' : 'Remove'}
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </>
          )
        )}
      </div>

      {/* Cuttlefish Create VM Modal */}
      {showCfCreate && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center',
          zIndex: 1000
        }} onClick={() => !cfCreating && setShowCfCreate(false)}>
          <div style={{
            background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12,
            padding: 24, width: 650, maxWidth: '95vw', maxHeight: '85vh', display: 'flex', flexDirection: 'column'
          }} onClick={e => e.stopPropagation()}>
            <h3 style={{ margin: '0 0 16px', display: 'flex', alignItems: 'center', gap: 8 }}>
              <Cpu size={18} style={{ color: '#cba6f7' }} /> Create Cuttlefish Android VM
            </h3>

            <div style={{ marginBottom: 12 }}>
              <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>VM Name</label>
              <input
                value={cfDeviceName}
                onChange={e => setCfDeviceName(e.target.value)}
                placeholder="e.g., my-pixel-9"
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 12 }}>
              <div>
                <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>Android Version</label>
                <select value={cfSelectedImage} onChange={e => setCfSelectedImage(e.target.value)}
                  style={{ width: '100%', padding: '8px 12px', background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--text)' }}>
                  {cfImages.map(img => (
                    <option key={img.id} value={img.id}>{img.name} {img.hasGapps ? '(GApps)' : ''}</option>
                  ))}
                </select>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6, paddingTop: 22 }}>
                <label style={{ fontSize: 12, display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
                  <input type="checkbox" checked={cfEnableGpu} onChange={e => setCfEnableGpu(e.target.checked)} />
                  GPU Passthrough
                </label>
              </div>
            </div>

            <div style={{ marginBottom: 8 }}>
              <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>Select Hardware Profile</label>
              <input
                value={cfProfileSearch}
                onChange={e => setCfProfileSearch(e.target.value)}
                placeholder="Search profiles... (e.g., Samsung, Pixel, OnePlus)"
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 6, minHeight: 200, maxHeight: '45vh' }}>
              {cfProfiles.length === 0 ? (
                <div style={{ textAlign: 'center', padding: 20, color: 'var(--text-muted)' }}>Loading hardware profiles...</div>
              ) : (
                cfProfiles
                  .filter(p => {
                    const q = cfProfileSearch.toLowerCase();
                    return !q || p.name.toLowerCase().includes(q) || p.brand.toLowerCase().includes(q) || p.model.toLowerCase().includes(q) || p.category.toLowerCase().includes(q);
                  })
                  .map(profile => (
                    <div key={profile.id} style={{
                      padding: '10px 12px', borderRadius: 8,
                      border: cfSelectedProfile === profile.id ? '2px solid #cba6f7' : '1px solid var(--border)',
                      background: cfSelectedProfile === profile.id ? 'rgba(203,166,247,0.1)' : 'var(--bg)',
                      cursor: cfCreating ? 'default' : 'pointer',
                      display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                      transition: 'border-color 0.15s',
                    }}
                      onClick={() => !cfCreating && setCfSelectedProfile(profile.id)}
                      onMouseEnter={e => { if (!cfCreating && cfSelectedProfile !== profile.id) (e.currentTarget as HTMLDivElement).style.borderColor = '#cba6f7'; }}
                      onMouseLeave={e => { if (cfSelectedProfile !== profile.id) (e.currentTarget as HTMLDivElement).style.borderColor = 'var(--border)'; }}
                    >
                      <div>
                        <div style={{ fontWeight: 600, fontSize: 13, display: 'flex', alignItems: 'center', gap: 6 }}>
                          <Smartphone size={14} style={{ color: '#cba6f7' }} />
                          {profile.name}
                          <span style={{ fontSize: 10, padding: '1px 6px', borderRadius: 4, background: 'rgba(203,166,247,0.2)', color: '#cba6f7' }}>
                            {profile.category}
                          </span>
                        </div>
                        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
                          {profile.cpuCores} cores | {profile.ramMb >= 1024 ? `${(profile.ramMb / 1024).toFixed(0)} GB` : `${profile.ramMb} MB`} RAM | {profile.screenWidth}x{profile.screenHeight} @ {profile.screenDpi}dpi | {profile.storageGb} GB
                          {profile.hasModem && ' | Modem'}{profile.hasGps && ' | GPS'}{profile.hasSensors && ' | Sensors'}{profile.hasCamera && ' | Camera'}{profile.hasBiometrics && ' | Biometrics'}
                        </div>
                      </div>
                      {cfSelectedProfile === profile.id && (
                        <span style={{ color: '#cba6f7', fontWeight: 'bold', fontSize: 16 }}>&#10003;</span>
                      )}
                    </div>
                  ))
              )}
            </div>

            <div style={{ marginTop: 12, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                {cfProfiles.length} profiles | Full Android kernel + QEMU/KVM | Auto-assigns US phone via Twilio
              </span>
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="btn btn-ghost" onClick={() => setShowCfCreate(false)} disabled={cfCreating}>
                  Cancel
                </button>
                <button className="btn btn-success" disabled={cfCreating || !cfSelectedProfile}
                  onClick={async () => {
                    setCfCreating(true);
                    try {
                      const result = await cuttlefishActions.createDevice({
                        name: cfDeviceName || undefined as unknown as string,
                        profileId: cfSelectedProfile,
                        imageId: cfSelectedImage,
                        assignPhoneNumber: true,
                        enableGpu: cfEnableGpu,
                      });
                      setActionMsg({ type: 'success', text: `Created ${result.device.name} (${result.device.brand} ${result.device.model})` });
                      setTimeout(() => setActionMsg(null), 5000);
                      setShowCfCreate(false);
                      setCfDeviceName('');
                      setCfSelectedProfile('');
                      await loadCfDevices();
                    } catch (e) {
                      setActionMsg({ type: 'error', text: `Create failed: ${(e as Error).message}` });
                    } finally {
                      setCfCreating(false);
                    }
                  }}>
                  {cfCreating ? 'Creating VM...' : 'Create VM'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Create Cloud Device Modal */}
      {showCreateModal && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center',
          zIndex: 1000
        }} onClick={() => !creating && setShowCreateModal(false)}>
          <div style={{
            background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12,
            padding: 24, width: 400, maxWidth: '90vw'
          }} onClick={e => e.stopPropagation()}>
            <h3 style={{ margin: '0 0 16px', display: 'flex', alignItems: 'center', gap: 8 }}>
              <Plus size={18} /> Create Cloud Android Device
            </h3>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div>
                <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>Device Name (optional)</label>
                <input
                  value={createName}
                  onChange={e => setCreateName(e.target.value)}
                  placeholder="e.g., Mail Device, Social Media"
                  style={{ width: '100%' }}
                />
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <div>
                  <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>RAM (GB)</label>
                  <select value={createRam} onChange={e => setCreateRam(Number(e.target.value))}
                    style={{ width: '100%', padding: '8px 12px', background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--text)' }}>
                    <option value={1}>1 GB</option>
                    <option value={2}>2 GB</option>
                    <option value={3}>3 GB</option>
                    <option value={4}>4 GB</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', marginBottom: 4, fontSize: 13 }}>CPUs</label>
                  <select value={createCpus} onChange={e => setCreateCpus(Number(e.target.value))}
                    style={{ width: '100%', padding: '8px 12px', background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--text)' }}>
                    <option value={1}>1 CPU</option>
                    <option value={2}>2 CPUs</option>
                    <option value={3}>3 CPUs</option>
                    <option value={4}>4 CPUs</option>
                  </select>
                </div>
              </div>
              <p style={{ fontSize: 11, color: 'var(--text-muted)', margin: 0 }}>
                Each device runs Android 14 in a Docker container. The device will be available 24/7 and auto-restart on reboot.
              </p>
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                <button className="btn btn-ghost" onClick={() => setShowCreateModal(false)} disabled={creating}>
                  Cancel
                </button>
                <button className="btn btn-success" onClick={handleCreate} disabled={creating}>
                  {creating ? 'Creating...' : 'Create Device'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Remote Physical Devices Panel (ADB Bridge from PC) */}
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, padding: 16, marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: showRemotePanel ? 12 : 0 }}>
          <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8, fontSize: 15 }}>
            <Smartphone size={18} style={{ color: '#89b4fa' }} />
            Physical Devices (USB via PC)
            <span style={{ fontSize: 12, color: 'var(--text-muted)', fontWeight: 'normal' }}>
              {remoteDevices.length} device(s)
            </span>
            {remoteBridgeStatus && (
              <span style={{ fontSize: 11 }}>
                {remoteBridgeStatus.pushActive
                  ? <span style={{ color: '#a6e3a1', display: 'inline-flex', alignItems: 'center', gap: 3 }}><Wifi size={12} /> PC Connected</span>
                  : remoteBridgeStatus.hosts.length > 0 && remoteBridgeStatus.hosts[0].isReachable
                    ? <span style={{ color: '#a6e3a1', display: 'inline-flex', alignItems: 'center', gap: 3 }}><Wifi size={12} /> ADB Bridge Connected</span>
                    : <span style={{ color: '#f38ba8', display: 'inline-flex', alignItems: 'center', gap: 3 }}><WifiOff size={12} /> PC Offline</span>
                }
              </span>
            )}
          </h3>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-sm btn-primary" onClick={async () => {
              setRemoteRefreshing(true);
              try {
                const devs = await remoteDeviceActions.refresh();
                setRemoteDevices(devs);
                setActionMsg({ type: 'success', text: `Found ${devs.length} physical device(s)` });
                setTimeout(() => setActionMsg(null), 3000);
              } catch (e) {
                setActionMsg({ type: 'error', text: `Refresh failed: ${(e as Error).message}` });
              } finally { setRemoteRefreshing(false); }
            }} disabled={remoteRefreshing}>
              <RefreshCw size={12} /> {remoteRefreshing ? 'Scanning...' : 'Refresh'}
            </button>
            <button className="btn btn-sm btn-ghost" onClick={() => setShowRemotePanel(!showRemotePanel)}>
              {showRemotePanel ? 'Collapse' : 'Expand'}
            </button>
          </div>
        </div>

        {showRemotePanel && (
          remoteDevices.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '20px 0', color: 'var(--text-muted)' }}>
              <Smartphone size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
              <p style={{ margin: '0 0 4px' }}>
                {remoteBridgeStatus && (remoteBridgeStatus.pushActive || (remoteBridgeStatus.hosts.length > 0 && remoteBridgeStatus.hosts[0].isReachable))
                  ? 'PC connected but no physical devices found. Connect a phone via USB to your PC.'
                  : 'No PC connected. Run setup-all.bat on your PC to sync physical devices.'}
              </p>
              <p style={{ margin: 0, fontSize: 11, color: 'var(--text-muted)' }}>
                Physical phones connected via USB to your PC will appear here automatically.
              </p>
            </div>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: 12 }}>
              {remoteDevices.map(rd => (
                <div key={rd.serial} style={{
                  background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: 8,
                  padding: 12, display: 'flex', flexDirection: 'column', gap: 8
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <Smartphone size={16} style={{ color: rd.connectionState === 'Online' ? '#89b4fa' : '#666' }} />
                      <strong style={{ fontSize: 13 }}>{rd.friendlyName || rd.model || rd.serial}</strong>
                    </div>
                    <span className={`badge ${rd.connectionState === 'Online' ? 'online' : rd.connectionState === 'Unauthorized' ? 'warning' : 'offline'}`} style={{ fontSize: 10 }}>
                      <span className="badge-dot" />
                      {rd.connectionState}
                    </span>
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', display: 'grid', gridTemplateColumns: '90px 1fr', gap: '2px 8px' }}>
                    <span>Serial:</span><span style={{ fontFamily: 'monospace' }}>{rd.serial}</span>
                    <span>Model:</span><span>{rd.model || 'Unknown'}</span>
                    <span>Manufacturer:</span><span>{rd.manufacturer || 'Unknown'}</span>
                    <span>Android:</span><span>{rd.androidVersion || 'N/A'}</span>
                    <span>Connection:</span><span style={{ display: 'flex', alignItems: 'center', gap: 3 }}><Usb size={10} /> USB (Remote PC)</span>
                    <span>Battery:</span><span>{rd.batteryLevel >= 0 ? <BatteryIndicator level={rd.batteryLevel} /> : 'N/A'}</span>
                    <span>Source:</span><span style={{ fontFamily: 'monospace' }}>{rd.source}</span>
                  </div>
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    <button className="btn btn-sm btn-primary"
                      onClick={() => navigate(`/devices/${rd.serial}/screen`)}
                      title="View and control phone screen in browser"
                      disabled={rd.connectionState !== 'Online'}>
                      <Eye size={12} /> View Screen
                    </button>
                    <button className="btn btn-sm btn-ghost"
                      onClick={() => { navigator.clipboard.writeText(`${rd.serial} | ${rd.model} | ${rd.manufacturer}`); setActionMsg({ type: 'success', text: 'Copied to clipboard' }); setTimeout(() => setActionMsg(null), 2000); }}>
                      <Copy size={12} /> Copy Info
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )
        )}
      </div>

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
          <p>Click "Connect Cloud Devices" above or connect Android phones via USB</p>
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
