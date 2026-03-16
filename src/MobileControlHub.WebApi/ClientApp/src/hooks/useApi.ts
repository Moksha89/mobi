import { useState, useEffect, useCallback } from 'react';
import type { SystemStatus, AndroidDevice, ScrcpySession, LogEntry, AppConfiguration, VpsConfiguration, TwilioAccountInfo, SmsMessage, GenymotionRecipe, GenymotionInstance, HardwareProfile, OsImage, CloudDevice, CloudPlatformStatus, RemotePhysicalDevice, RemoteBridgeStatus } from '../types';

const API_BASE = '/api';

async function fetchJson<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${url}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: res.statusText }));
    throw new Error(err.message || res.statusText);
  }
  return res.json();
}

export function useStatus(autoRefresh = 5000) {
  const [status, setStatus] = useState<SystemStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      const data = await fetchJson<SystemStatus>('/status');
      setStatus(data);
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
    if (autoRefresh > 0) {
      const id = setInterval(refresh, autoRefresh);
      return () => clearInterval(id);
    }
  }, [refresh, autoRefresh]);

  return { status, loading, error, refresh };
}

export function useDevices(autoRefresh = 5000) {
  const [devices, setDevices] = useState<AndroidDevice[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      const data = await fetchJson<AndroidDevice[]>('/devices');
      setDevices(data);
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
    if (autoRefresh > 0) {
      const id = setInterval(refresh, autoRefresh);
      return () => clearInterval(id);
    }
  }, [refresh, autoRefresh]);

  const refreshDevices = async () => {
    try {
      const data = await fetchJson<AndroidDevice[]>('/devices/refresh', { method: 'POST' });
      setDevices(data);
    } catch (e) {
      setError((e as Error).message);
    }
  };

  return { devices, loading, error, refresh, refreshDevices };
}

export function useSessions(autoRefresh = 3000) {
  const [sessions, setSessions] = useState<ScrcpySession[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      const data = await fetchJson<ScrcpySession[]>('/sessions');
      setSessions(data);
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
    if (autoRefresh > 0) {
      const id = setInterval(refresh, autoRefresh);
      return () => clearInterval(id);
    }
  }, [refresh, autoRefresh]);

  return { sessions, loading, error, refresh };
}

export function useLogs() {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchLogs = useCallback(async (params?: {
    level?: string;
    category?: string;
    deviceSerial?: string;
    limit?: number;
  }) => {
    try {
      setLoading(true);
      const query = new URLSearchParams();
      if (params?.level) query.set('level', params.level);
      if (params?.category) query.set('category', params.category);
      if (params?.deviceSerial) query.set('deviceSerial', params.deviceSerial);
      if (params?.limit) query.set('limit', params.limit.toString());
      const url = `/logs${query.toString() ? '?' + query.toString() : ''}`;
      const data = await fetchJson<LogEntry[]>(url);
      setLogs(data);
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchRecent = useCallback(async (count = 50) => {
    try {
      setLoading(true);
      const data = await fetchJson<LogEntry[]>(`/logs/recent?count=${count}`);
      setLogs(data);
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchRecent();
  }, [fetchRecent]);

  return { logs, loading, error, fetchLogs, fetchRecent };
}

export function useSettings() {
  const [config, setConfig] = useState<AppConfiguration | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const data = await fetchJson<AppConfiguration>('/settings');
      setConfig(data);
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const save = async (updates: Record<string, unknown>) => {
    try {
      await fetchJson('/settings', {
        method: 'PUT',
        body: JSON.stringify(updates),
      });
      await load();
      return true;
    } catch (e) {
      setError((e as Error).message);
      return false;
    }
  };

  return { config, loading, error, load, save };
}

export function useVps() {
  const [vps, setVps] = useState<VpsConfiguration | null>(null);
  const [loading, setLoading] = useState(true);
  const [testing, setTesting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const data = await fetchJson<VpsConfiguration>('/settings/vps');
      setVps(data);
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const save = async (config: Record<string, unknown>) => {
    try {
      await fetchJson('/settings/vps', {
        method: 'PUT',
        body: JSON.stringify(config),
      });
      await load();
      return true;
    } catch (e) {
      setError((e as Error).message);
      return false;
    }
  };

  const test = async () => {
    try {
      setTesting(true);
      const data = await fetchJson<VpsConfiguration>('/settings/vps/test', { method: 'POST' });
      setVps(data);
      setError(null);
      return data;
    } catch (e) {
      setError((e as Error).message);
      return null;
    } finally {
      setTesting(false);
    }
  };

  return { vps, loading, testing, error, load, save, test };
}

// Virtual device actions
export const virtualDeviceActions = {
  getAll: () => fetchJson<import('../types').VirtualDeviceInfo[]>('/virtual-devices'),
  connectAll: () => fetchJson('/virtual-devices/connect-all', { method: 'POST' }),
  disconnectAll: () => fetchJson('/virtual-devices/disconnect-all', { method: 'POST' }),
  connect: (host: string, port: number, friendlyName?: string) =>
    fetchJson('/virtual-devices/connect', { method: 'POST', body: JSON.stringify({ host, port, friendlyName }) }),
  disconnect: (host: string, port: number) =>
    fetchJson('/virtual-devices/disconnect', { method: 'POST', body: JSON.stringify({ host, port }) }),
  create: (name?: string, ramGB?: number, cpus?: number) =>
    fetchJson('/virtual-devices/create', { method: 'POST', body: JSON.stringify({ name: name || '', ramGB: ramGB || 3, cpus: cpus || 2 }) }),
  remove: (containerName: string) =>
    fetchJson('/virtual-devices/remove', { method: 'POST', body: JSON.stringify({ containerName }) }),
  restart: (containerName: string) =>
    fetchJson('/virtual-devices/restart', { method: 'POST', body: JSON.stringify({ containerName }) }),
};

// Twilio actions
export const twilioActions = {
  getStatus: () => fetchJson<TwilioAccountInfo>('/twilio/status'),
  getNumbers: () => fetchJson<import('../types').TwilioNumberInfo[]>('/twilio/numbers'),
  getNumberForContainer: (containerName: string) => fetchJson<import('../types').TwilioNumberInfo>(`/twilio/numbers/${containerName}`),
  provisionNumber: (containerName: string) => fetchJson('/twilio/numbers/provision', { method: 'POST', body: JSON.stringify({ containerName }) }),
  releaseNumber: (containerName: string) => fetchJson('/twilio/numbers/release', { method: 'POST', body: JSON.stringify({ containerName }) }),
  getMessages: (containerName: string, limit = 50) => fetchJson<SmsMessage[]>(`/twilio/sms/${containerName}?limit=${limit}`),
  sendSms: (containerName: string, to: string, body: string) => fetchJson('/twilio/sms/send', { method: 'POST', body: JSON.stringify({ containerName, to, body }) }),
  getCallLogs: (containerName: string, limit = 50) => fetchJson<import('../types').CallLog[]>(`/twilio/calls/${containerName}?limit=${limit}`),
};

// Genymotion actions
export const genymotionActions = {
  getStatus: () => fetchJson<{ isConfigured: boolean }>('/genymotion/status'),
  getRecipes: () => fetchJson<GenymotionRecipe[]>('/genymotion/recipes'),
  getInstances: () => fetchJson<GenymotionInstance[]>('/genymotion/instances'),
  getInstance: (uuid: string) => fetchJson<GenymotionInstance>(`/genymotion/instances/${uuid}`),
  startInstance: (recipeUuid: string, instanceName?: string, assignPhoneNumber = true) =>
    fetchJson<{ success: boolean; message: string; instance: GenymotionInstance; phoneNumber?: string }>(
      '/genymotion/instances/start',
      { method: 'POST', body: JSON.stringify({ recipeUuid, instanceName: instanceName || '', assignPhoneNumber }) }
    ),
  stopInstance: (uuid: string) =>
    fetchJson('/genymotion/instances/' + uuid + '/stop', { method: 'POST' }),
  getAccessToken: (uuid: string) =>
    fetchJson<{ accessToken: string }>('/genymotion/instances/' + uuid + '/access-token', { method: 'POST' }),
};

// K8s Cloud Platform actions
export const cloudPlatformActions = {
  getStatus: () => fetchJson<CloudPlatformStatus>('/cloud-platform/status'),
  getProfiles: () => fetchJson<HardwareProfile[]>('/cloud-platform/profiles'),
  getImages: () => fetchJson<OsImage[]>('/cloud-platform/images'),
  getDevices: () => fetchJson<CloudDevice[]>('/cloud-platform/devices'),
  getDevice: (deviceId: string) => fetchJson<CloudDevice>(`/cloud-platform/devices/${deviceId}`),
  createDevice: (data: { name: string; hardwareProfileId: string; osImageId: string; assignPhoneNumber?: boolean; persistentStorage?: boolean; enableGpu?: boolean }) =>
    fetchJson<{ success: boolean; message: string; device: CloudDevice }>('/cloud-platform/devices/create', { method: 'POST', body: JSON.stringify(data) }),
  removeDevice: (deviceId: string) =>
    fetchJson('/cloud-platform/devices/' + deviceId + '/remove', { method: 'POST' }),
  restartDevice: (deviceId: string) =>
    fetchJson('/cloud-platform/devices/' + deviceId + '/restart', { method: 'POST' }),
  getStreamUrl: (deviceId: string) =>
    fetchJson<{ streamUrl: string }>('/cloud-platform/devices/' + deviceId + '/stream-url'),
};

// Remote physical device actions (ADB bridge from PC)
export const remoteDeviceActions = {
  getDevices: () => fetchJson<RemotePhysicalDevice[]>('/remote-devices'),
  refresh: () => fetchJson<RemotePhysicalDevice[]>('/remote-devices/refresh', { method: 'POST' }),
  getStatus: () => fetchJson<RemoteBridgeStatus>('/remote-devices/status'),
};

// Device action helpers
export const deviceActions = {
  reconnect: (serial: string) => fetchJson(`/devices/${serial}/reconnect`, { method: 'POST' }),
  reboot: (serial: string) => fetchJson(`/devices/${serial}/reboot`, { method: 'POST' }),
  screenshot: (serial: string) => fetchJson(`/devices/${serial}/screenshot`, { method: 'POST' }),
  install: (serial: string, apkPath: string) => fetchJson(`/devices/${serial}/install`, { method: 'POST', body: JSON.stringify({ apkPath }) }),
  push: (serial: string, localPath: string, remotePath: string) => fetchJson(`/devices/${serial}/push`, { method: 'POST', body: JSON.stringify({ localPath, remotePath }) }),
  pull: (serial: string, remotePath: string, localPath: string) => fetchJson(`/devices/${serial}/pull`, { method: 'POST', body: JSON.stringify({ remotePath, localPath }) }),
  shell: (serial: string, command: string) => fetchJson(`/devices/${serial}/shell`, { method: 'POST', body: JSON.stringify({ command }) }),
  setName: (serial: string, friendlyName: string) => fetchJson(`/devices/${serial}/name`, { method: 'PUT', body: JSON.stringify({ friendlyName }) }),
};

export const sessionActions = {
  start: (serial: string, customArgs?: string, autoRestart = false) =>
    fetchJson(`/sessions/${serial}/start`, { method: 'POST', body: JSON.stringify({ customArgs, autoRestart }) }),
  stop: (sessionId: string) => fetchJson(`/sessions/${sessionId}/stop`, { method: 'POST' }),
  stopAll: () => fetchJson('/sessions/stop-all', { method: 'POST' }),
  restart: (sessionId: string) => fetchJson(`/sessions/${sessionId}/restart`, { method: 'POST' }),
};
