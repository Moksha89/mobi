namespace MobileControlHub.Domain.Interfaces;

/// <summary>
/// Service for managing Twilio virtual phone numbers and SMS.
/// Auto-provisions US numbers for cloud devices.
/// </summary>
public interface ITwilioService
{
    /// <summary>Check if Twilio is configured with valid credentials.</summary>
    bool IsConfigured { get; }

    /// <summary>Purchase a new US phone number and assign it to a device container.</summary>
    Task<TwilioNumberInfo?> ProvisionNumberAsync(string containerName, CancellationToken ct = default);

    /// <summary>Release a phone number when a device is removed.</summary>
    Task<bool> ReleaseNumberAsync(string containerName, CancellationToken ct = default);

    /// <summary>Get the assigned number for a container.</summary>
    Task<TwilioNumberInfo?> GetNumberForContainerAsync(string containerName, CancellationToken ct = default);

    /// <summary>Get all assigned numbers.</summary>
    Task<List<TwilioNumberInfo>> GetAllNumbersAsync(CancellationToken ct = default);

    /// <summary>Get SMS messages for a specific phone number.</summary>
    Task<List<SmsMessage>> GetMessagesAsync(string containerName, int limit = 50, CancellationToken ct = default);

    /// <summary>Send an SMS from a device's assigned number. Returns (success, errorMessage).</summary>
    Task<(bool Success, string Error)> SendSmsAsync(string containerName, string to, string body, CancellationToken ct = default);

    /// <summary>Record an incoming SMS (called by webhook).</summary>
    Task RecordIncomingSmsAsync(string toNumber, string fromNumber, string body, CancellationToken ct = default);

    /// <summary>Get Twilio account info (balance, etc).</summary>
    Task<TwilioAccountInfo> GetAccountInfoAsync(CancellationToken ct = default);

    /// <summary>Record an incoming or outgoing call (called by webhook).</summary>
    Task RecordCallAsync(string phoneNumber, string fromNumber, string toNumber, string direction, string status, int durationSeconds, CancellationToken ct = default);

    /// <summary>Get call logs for a container's phone number.</summary>
    Task<List<CallLog>> GetCallLogsAsync(string containerName, int limit = 50, CancellationToken ct = default);
}

/// <summary>Info about an assigned Twilio phone number.</summary>
public class TwilioNumberInfo
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string TwilioSid { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}

/// <summary>SMS message record.</summary>
public class SmsMessage
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
    public string ToNumber { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // "inbound" or "outbound"
    public DateTime ReceivedAt { get; set; }
}

/// <summary>Call log record.</summary>
public class CallLog
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
    public string ToNumber { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // "inbound" or "outbound"
    public string Status { get; set; } = string.Empty; // "ringing", "in-progress", "completed", "no-answer", "busy", "failed"
    public int DurationSeconds { get; set; }
    public DateTime ReceivedAt { get; set; }
}

/// <summary>Twilio account summary.</summary>
public class TwilioAccountInfo
{
    public bool IsConfigured { get; set; }
    public string AccountSid { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string Balance { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public int ActiveNumbers { get; set; }
}
