export interface AndroidDevice {
  serialNumber: string;
  model: string;
  manufacturer: string;
  androidVersion: string;
  connectionType: string;
  connectionState: DeviceConnectionState;
  batteryLevel: number;
  isScreenOn: boolean | null;
  friendlyName: string;
  firstSeen: string;
  lastSeen: string;
  hasActiveSession: boolean;
  isSelected: boolean;
  displayName: string;
  isVirtual: boolean;
  vpsHost: string;
}

export interface VirtualDeviceInfo {
  host: string;
  port: number;
  serial: string;
  friendlyName: string;
  connected: boolean;
  androidVersion: string;
  model: string;
  containerName: string;
  containerStatus: string;
  phoneNumber?: string;
}

export interface TwilioAccountInfo {
  isConfigured: boolean;
  accountSid: string;
  friendlyName: string;
  balance: string;
  currency: string;
  activeNumbers: number;
}

export interface TwilioNumberInfo {
  phoneNumber: string;
  containerName: string;
  friendlyName: string;
  twilioSid: string;
  assignedAt: string;
}

export interface SmsMessage {
  id: number;
  phoneNumber: string;
  fromNumber: string;
  toNumber: string;
  body: string;
  direction: string;
  receivedAt: string;
}

export interface CallLog {
  id: number;
  phoneNumber: string;
  fromNumber: string;
  toNumber: string;
  direction: string;
  status: string;
  durationSeconds: number;
  receivedAt: string;
}

export interface GenymotionRecipe {
  uuid: string;
  name: string;
  description: string;
  androidVersion: string;
  apiLevel: number;
  formFactor: string;
  cpuCount: number;
  ramMb: number;
  diskMb: number;
  screenWidth: number;
  screenHeight: number;
  screenDensity: number;
  isOfficial: boolean;
}

export interface GenymotionInstance {
  uuid: string;
  name: string;
  state: string;
  recipeUuid: string;
  recipeName: string;
  androidVersion: string;
  formFactor: string;
  cpuCount: number;
  ramMb: number;
  screenWidth: number;
  screenHeight: number;
  screenDensity: number;
  webrtcUrl: string;
  adbUrl: string;
  streamerFqdn: string;
  createdAt: string;
  updatedAt: string;
}

// K8s Cloud Platform types
export interface HardwareProfile {
  id: string;
  name: string;
  brand: string;
  model: string;
  category: string;
  cpuCores: number;
  ramMb: number;
  storageGb: number;
  screenWidth: number;
  screenHeight: number;
  screenDpi: number;
  gpuMode: string;
  description: string;
  supportedAndroidVersions: string[];
}

export interface OsImage {
  id: string;
  name: string;
  androidVersion: string;
  apiLevel: number;
  dockerImage: string;
  hasGapps: boolean;
  isDefault: boolean;
  description: string;
}

export interface CloudDevice {
  id: string;
  name: string;
  state: string;
  hardwareProfileId: string;
  hardwareProfileName: string;
  osImageId: string;
  androidVersion: string;
  brand: string;
  model: string;
  cpuCores: number;
  ramMb: number;
  storageGb: number;
  screenWidth: number;
  screenHeight: number;
  screenDpi: number;
  adbPort: number;
  streamPort: number;
  adbAddress: string;
  streamUrl: string;
  phoneNumber?: string;
  podName: string;
  nodeName: string;
  createdAt: string;
  startedAt?: string;
  uptimeSeconds: number;
  cpuUsagePercent: number;
  memoryUsageMb: number;
  gpuAccelerated: boolean;
  hasGapps: boolean;
  persistentStorage: boolean;
}

export interface CloudPlatformStatus {
  isConfigured: boolean;
  clusterReachable: boolean;
  kubernetesVersion: string;
  totalNodes: number;
  readyNodes: number;
  totalDevices: number;
  runningDevices: number;
  gpuAvailable: boolean;
  gpuModel: string;
  resources: ClusterResources;
}

export interface ClusterResources {
  totalCpuMillicores: number;
  usedCpuMillicores: number;
  totalMemoryMb: number;
  usedMemoryMb: number;
  totalStorageGb: number;
  usedStorageGb: number;
  maxDevices: number;
}

export enum DeviceConnectionState {
  Unknown = 0,
  Online = 1,
  Offline = 2,
  Unauthorized = 3,
  Disconnected = 4,
  Recovery = 5,
  Sideload = 6,
  NoPermissions = 7,
}

export enum SessionState {
  Stopped = 0,
  Starting = 1,
  Running = 2,
  Error = 3,
  Reconnecting = 4,
}

export enum AppLogLevel {
  Debug = 0,
  Info = 1,
  Warning = 2,
  Error = 3,
  Critical = 4,
}

export interface ScrcpySession {
  sessionId: string;
  deviceSerial: string;
  state: SessionState;
  processId: number;
  startedAt: string;
  endedAt: string | null;
  customArguments: string;
  autoRestart: boolean;
  restartCount: number;
  lastError: string | null;
}

export interface LogEntry {
  id: number;
  timestamp: string;
  level: AppLogLevel;
  category: string;
  message: string;
  deviceSerial: string | null;
  details: string | null;
  source: string | null;
}

export interface SystemStatus {
  adbAvailable: boolean;
  adbVersion: string | null;
  scrcpyAvailable: boolean;
  scrcpyVersion: string | null;
  rustDeskInstalled: boolean;
  rustDeskRunning: boolean;
  vpsConfigured: boolean;
  vpsReachable: boolean;
  connectedDevices: number;
  activeSessions: number;
  monitorRunning: boolean;
  timestamp: string;
}

export interface AppConfiguration {
  adbPath: string;
  scrcpyPath: string;
  rustDeskPath: string;
  devicePollIntervalSeconds: number;
  adbTimeoutSeconds: number;
  maxRetryAttempts: number;
  retryDelaySeconds: number;
  autoReconnectDevices: boolean;
  autoRestartScrcpy: boolean;
  startOnWindowsBoot: boolean;
  startMonitoringOnLaunch: boolean;
  restoreSessionsOnLaunch: boolean;
  defaultScrcpyArgs: string;
  vpsConfig: VpsConfiguration;
  deviceFriendlyNames: Record<string, string>;
}

export interface VpsConfiguration {
  host: string;
  sshPort: number;
  rustDeskRelayServer: string;
  rustDeskIdServer: string;
  rustDeskRelayPort: number;
  rustDeskIdPort: number;
  rustDeskApiPort: number;
  isConfigured: boolean;
  isReachable: boolean;
  isRelayReachable: boolean;
  isIdServerReachable: boolean;
  lastTestedAt: string | null;
}

export interface ApiResult {
  success: boolean;
  message: string;
}

export interface ApiResultData<T> extends ApiResult {
  data: T;
}
