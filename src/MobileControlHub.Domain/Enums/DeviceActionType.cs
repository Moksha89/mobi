namespace MobileControlHub.Domain.Enums;

/// <summary>
/// Types of actions that can be performed on a connected device.
/// </summary>
public enum DeviceActionType
{
    OpenScrcpy,
    CloseScrcpy,
    ReconnectAdb,
    OpenShell,
    CaptureScreenshot,
    RecordScreen,
    InstallApk,
    PushFile,
    PullFile,
    RebootDevice,
    CopyInfo
}
