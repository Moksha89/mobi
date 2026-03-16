using Microsoft.Data.Sqlite;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Infrastructure.Data;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Api.V2010.Account.AvailablePhoneNumberCountry;
using Twilio.Types;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Manages Twilio phone number provisioning and SMS for cloud Android devices.
/// Each device gets its own US phone number auto-assigned on creation.
/// </summary>
public class TwilioService : ITwilioService
{
    private readonly DatabaseManager _db;
    private readonly ILogService _logService;
    private readonly string _accountSid;
    private readonly string _authToken;

    public TwilioService(DatabaseManager db, ILogService logService)
    {
        _db = db;
        _logService = logService;
        _accountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID") ?? "";
        _authToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN") ?? "";

        if (IsConfigured)
        {
            TwilioClient.Init(_accountSid, _authToken);
        }
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_accountSid) && !string.IsNullOrEmpty(_authToken);

    public async Task<TwilioNumberInfo?> ProvisionNumberAsync(string containerName, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        try
        {
            // Check if container already has a number
            var existing = await GetNumberForContainerAsync(containerName, ct);
            if (existing != null) return existing;

            // Search for available US local numbers
            var availableNumbers = await LocalResource.ReadAsync(
                pathCountryCode: "US",
                smsEnabled: true,
                voiceEnabled: true,
                limit: 1
            );

            if (availableNumbers == null || !availableNumbers.Any())
            {
                await _logService.LogErrorAsync("No available US phone numbers found on Twilio",
                    source: "Twilio");
                return null;
            }

            var numberToBuy = availableNumbers.First();

            // Determine webhook base URL from environment or default
            var webhookBase = Environment.GetEnvironmentVariable("WEBHOOK_BASE_URL") ?? "http://69.197.142.77:5000";
            var smsWebhookUrl = new Uri($"{webhookBase}/api/twilio/webhook/sms");
            var voiceWebhookUrl = new Uri($"{webhookBase}/api/twilio/webhook/voice");

            // Purchase the number with webhook URLs configured
            var purchased = await IncomingPhoneNumberResource.CreateAsync(
                phoneNumber: new PhoneNumber(numberToBuy.PhoneNumber.ToString()),
                friendlyName: $"MCH-{containerName}",
                smsUrl: smsWebhookUrl,
                smsMethod: Twilio.Http.HttpMethod.Post,
                voiceUrl: voiceWebhookUrl,
                voiceMethod: Twilio.Http.HttpMethod.Post
            );

            // Store the assignment in the database
            using var conn = _db.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO TwilioNumbers (PhoneNumber, ContainerName, FriendlyName, TwilioSid, AssignedAt)
                                VALUES (@phone, @container, @friendly, @sid, datetime('now'))";
            cmd.Parameters.AddWithValue("@phone", purchased.PhoneNumber.ToString());
            cmd.Parameters.AddWithValue("@container", containerName);
            cmd.Parameters.AddWithValue("@friendly", $"MCH-{containerName}");
            cmd.Parameters.AddWithValue("@sid", purchased.Sid);
            await cmd.ExecuteNonQueryAsync(ct);

            await _logService.LogInfoAsync(
                $"Provisioned Twilio number {purchased.PhoneNumber} for container '{containerName}'",
                category: "Twilio");

            return new TwilioNumberInfo
            {
                PhoneNumber = purchased.PhoneNumber.ToString(),
                ContainerName = containerName,
                FriendlyName = $"MCH-{containerName}",
                TwilioSid = purchased.Sid,
                AssignedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync(
                $"Failed to provision Twilio number for '{containerName}': {ex.Message}",
                source: "Twilio");
            return null;
        }
    }

    public async Task<bool> ReleaseNumberAsync(string containerName, CancellationToken ct = default)
    {
        if (!IsConfigured) return false;

        try
        {
            var numberInfo = await GetNumberForContainerAsync(containerName, ct);
            if (numberInfo == null) return true; // No number assigned

            // Release from Twilio
            if (!string.IsNullOrEmpty(numberInfo.TwilioSid))
            {
                await IncomingPhoneNumberResource.DeleteAsync(pathSid: numberInfo.TwilioSid);
            }

            // Remove from database
            using var conn = _db.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM TwilioNumbers WHERE ContainerName = @container";
            cmd.Parameters.AddWithValue("@container", containerName);
            await cmd.ExecuteNonQueryAsync(ct);

            // Also clean up SMS records
            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = "DELETE FROM SmsMessages WHERE PhoneNumber = @phone";
            cmd2.Parameters.AddWithValue("@phone", numberInfo.PhoneNumber);
            await cmd2.ExecuteNonQueryAsync(ct);

            await _logService.LogInfoAsync(
                $"Released Twilio number {numberInfo.PhoneNumber} from container '{containerName}'",
                category: "Twilio");

            return true;
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync(
                $"Failed to release Twilio number for '{containerName}': {ex.Message}",
                source: "Twilio");
            return false;
        }
    }

    public async Task<TwilioNumberInfo?> GetNumberForContainerAsync(string containerName, CancellationToken ct = default)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PhoneNumber, ContainerName, FriendlyName, TwilioSid, AssignedAt FROM TwilioNumbers WHERE ContainerName = @container";
        cmd.Parameters.AddWithValue("@container", containerName);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new TwilioNumberInfo
            {
                PhoneNumber = reader.GetString(0),
                ContainerName = reader.GetString(1),
                FriendlyName = reader.GetString(2),
                TwilioSid = reader.GetString(3),
                AssignedAt = DateTime.Parse(reader.GetString(4))
            };
        }
        return null;
    }

    public async Task<List<TwilioNumberInfo>> GetAllNumbersAsync(CancellationToken ct = default)
    {
        var numbers = new List<TwilioNumberInfo>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PhoneNumber, ContainerName, FriendlyName, TwilioSid, AssignedAt FROM TwilioNumbers ORDER BY AssignedAt DESC";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            numbers.Add(new TwilioNumberInfo
            {
                PhoneNumber = reader.GetString(0),
                ContainerName = reader.GetString(1),
                FriendlyName = reader.GetString(2),
                TwilioSid = reader.GetString(3),
                AssignedAt = DateTime.Parse(reader.GetString(4))
            });
        }
        return numbers;
    }

    public async Task<List<SmsMessage>> GetMessagesAsync(string containerName, int limit = 50, CancellationToken ct = default)
    {
        var numberInfo = await GetNumberForContainerAsync(containerName, ct);
        if (numberInfo == null) return new List<SmsMessage>();

        var messages = new List<SmsMessage>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Id, PhoneNumber, FromNumber, ToNumber, Body, Direction, ReceivedAt 
                           FROM SmsMessages WHERE PhoneNumber = @phone 
                           ORDER BY ReceivedAt DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@phone", numberInfo.PhoneNumber);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            messages.Add(new SmsMessage
            {
                Id = reader.GetInt32(0),
                PhoneNumber = reader.GetString(1),
                FromNumber = reader.GetString(2),
                ToNumber = reader.GetString(3),
                Body = reader.GetString(4),
                Direction = reader.GetString(5),
                ReceivedAt = DateTime.Parse(reader.GetString(6))
            });
        }
        return messages;
    }

    public async Task<(bool Success, string Error)> SendSmsAsync(string containerName, string to, string body, CancellationToken ct = default)
    {
        if (!IsConfigured) return (false, "Twilio is not configured");

        try
        {
            var numberInfo = await GetNumberForContainerAsync(containerName, ct);
            if (numberInfo == null) return (false, "No phone number assigned to this device");

            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(to),
                from: new PhoneNumber(numberInfo.PhoneNumber),
                body: body
            );

            // Record the outbound message
            using var conn = _db.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO SmsMessages (PhoneNumber, FromNumber, ToNumber, Body, Direction, ReceivedAt)
                               VALUES (@phone, @from, @to, @body, 'outbound', datetime('now'))";
            cmd.Parameters.AddWithValue("@phone", numberInfo.PhoneNumber);
            cmd.Parameters.AddWithValue("@from", numberInfo.PhoneNumber);
            cmd.Parameters.AddWithValue("@to", to);
            cmd.Parameters.AddWithValue("@body", body);
            await cmd.ExecuteNonQueryAsync(ct);

            await _logService.LogInfoAsync(
                $"Sent SMS from {numberInfo.PhoneNumber} to {to}",
                category: "Twilio");

            if (message.Status == MessageResource.StatusEnum.Failed)
                return (false, $"Twilio rejected the message: {message.ErrorMessage}");

            return (true, "");
        }
        catch (Twilio.Exceptions.ApiException tex)
        {
            var msg = tex.Message;
            if (msg.Contains("unverified", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("trial", StringComparison.OrdinalIgnoreCase))
            {
                msg = $"Twilio trial accounts can only send SMS to verified numbers. Verify the recipient at https://console.twilio.com/us1/develop/phone-numbers/manage/verified -- Original error: {msg}";
            }
            await _logService.LogErrorAsync($"Failed to send SMS: {msg}", source: "Twilio");
            return (false, msg);
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync($"Failed to send SMS: {ex.Message}", source: "Twilio");
            return (false, ex.Message);
        }
    }

    public async Task RecordIncomingSmsAsync(string toNumber, string fromNumber, string body, CancellationToken ct = default)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO SmsMessages (PhoneNumber, FromNumber, ToNumber, Body, Direction, ReceivedAt)
                           VALUES (@phone, @from, @to, @body, 'inbound', datetime('now'))";
        cmd.Parameters.AddWithValue("@phone", toNumber);
        cmd.Parameters.AddWithValue("@from", fromNumber);
        cmd.Parameters.AddWithValue("@to", toNumber);
        cmd.Parameters.AddWithValue("@body", body);
        await cmd.ExecuteNonQueryAsync(ct);

        await _logService.LogInfoAsync(
            $"Received SMS on {toNumber} from {fromNumber}: {(body.Length > 50 ? body[..50] + "..." : body)}",
            category: "Twilio");
    }

    public async Task RecordCallAsync(string phoneNumber, string fromNumber, string toNumber, string direction, string status, int durationSeconds, CancellationToken ct = default)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO CallLogs (PhoneNumber, FromNumber, ToNumber, Direction, Status, DurationSeconds, ReceivedAt)
                           VALUES (@phone, @from, @to, @direction, @status, @duration, datetime('now'))";
        cmd.Parameters.AddWithValue("@phone", phoneNumber);
        cmd.Parameters.AddWithValue("@from", fromNumber);
        cmd.Parameters.AddWithValue("@to", toNumber);
        cmd.Parameters.AddWithValue("@direction", direction);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@duration", durationSeconds);
        await cmd.ExecuteNonQueryAsync(ct);

        await _logService.LogInfoAsync(
            $"Call logged: {direction} {status} on {phoneNumber} from={fromNumber} to={toNumber} duration={durationSeconds}s",
            category: "Twilio");
    }

    public async Task<List<CallLog>> GetCallLogsAsync(string containerName, int limit = 50, CancellationToken ct = default)
    {
        var numberInfo = await GetNumberForContainerAsync(containerName, ct);
        if (numberInfo == null) return new List<CallLog>();

        var logs = new List<CallLog>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Id, PhoneNumber, FromNumber, ToNumber, Direction, Status, DurationSeconds, ReceivedAt
                           FROM CallLogs WHERE PhoneNumber = @phone
                           ORDER BY ReceivedAt DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@phone", numberInfo.PhoneNumber);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            logs.Add(new CallLog
            {
                Id = reader.GetInt32(0),
                PhoneNumber = reader.GetString(1),
                FromNumber = reader.GetString(2),
                ToNumber = reader.GetString(3),
                Direction = reader.GetString(4),
                Status = reader.GetString(5),
                DurationSeconds = reader.GetInt32(6),
                ReceivedAt = DateTime.Parse(reader.GetString(7))
            });
        }
        return logs;
    }

    public async Task<TwilioAccountInfo> GetAccountInfoAsync(CancellationToken ct = default)
    {
        var info = new TwilioAccountInfo { IsConfigured = IsConfigured };

        if (!IsConfigured) return info;

        try
        {
            var account = await Twilio.Rest.Api.V2010.AccountResource.FetchAsync(pathSid: _accountSid);
            info.AccountSid = _accountSid;
            info.FriendlyName = account.FriendlyName ?? "";

            // Get balance
            try
            {
                var balance = await Twilio.Rest.Api.V2010.Account.BalanceResource.FetchAsync();
                info.Balance = balance.Balance ?? "0.00";
                info.Currency = balance.Currency ?? "USD";
            }
            catch
            {
                info.Balance = "N/A";
                info.Currency = "USD";
            }

            // Count active numbers
            var numbers = await GetAllNumbersAsync(ct);
            info.ActiveNumbers = numbers.Count;
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync($"Failed to fetch Twilio account info: {ex.Message}", source: "Twilio");
            info.FriendlyName = "Error fetching account info";
        }

        return info;
    }
}
