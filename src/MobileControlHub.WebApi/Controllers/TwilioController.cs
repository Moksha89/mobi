using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

/// <summary>
/// Manages Twilio virtual phone numbers and SMS for cloud devices.
/// </summary>
[ApiController]
[Route("api/twilio")]
public class TwilioController : ControllerBase
{
    private readonly ITwilioService _twilioService;
    private readonly ILogService _logService;

    public TwilioController(ITwilioService twilioService, ILogService logService)
    {
        _twilioService = twilioService;
        _logService = logService;
    }

    /// <summary>Get Twilio account info and status.</summary>
    [HttpGet("status")]
    public async Task<ActionResult<TwilioAccountInfo>> GetStatus(CancellationToken ct)
    {
        var info = await _twilioService.GetAccountInfoAsync(ct);
        return Ok(info);
    }

    /// <summary>Get all assigned phone numbers.</summary>
    [HttpGet("numbers")]
    public async Task<ActionResult<List<TwilioNumberInfo>>> GetNumbers(CancellationToken ct)
    {
        var numbers = await _twilioService.GetAllNumbersAsync(ct);
        return Ok(numbers);
    }

    /// <summary>Get phone number for a specific container.</summary>
    [HttpGet("numbers/{containerName}")]
    public async Task<ActionResult<TwilioNumberInfo>> GetNumberForContainer(string containerName, CancellationToken ct)
    {
        var number = await _twilioService.GetNumberForContainerAsync(containerName, ct);
        if (number == null)
            return NotFound(ApiResult.Fail($"No number assigned to '{containerName}'"));
        return Ok(number);
    }

    /// <summary>Manually provision a number for a container.</summary>
    [HttpPost("numbers/provision")]
    public async Task<ActionResult<ApiResult>> ProvisionNumber([FromBody] ProvisionNumberRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.ContainerName))
            return BadRequest(ApiResult.Fail("Container name is required"));

        var number = await _twilioService.ProvisionNumberAsync(request.ContainerName, ct);
        if (number == null)
            return BadRequest(ApiResult.Fail("Failed to provision number. Check Twilio credentials and balance."));

        return Ok(ApiResult.Ok($"Provisioned {number.PhoneNumber} for {request.ContainerName}"));
    }

    /// <summary>Release a number from a container.</summary>
    [HttpPost("numbers/release")]
    public async Task<ActionResult<ApiResult>> ReleaseNumber([FromBody] ReleaseNumberRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.ContainerName))
            return BadRequest(ApiResult.Fail("Container name is required"));

        var success = await _twilioService.ReleaseNumberAsync(request.ContainerName, ct);
        if (!success)
            return BadRequest(ApiResult.Fail("Failed to release number"));

        return Ok(ApiResult.Ok($"Released number for {request.ContainerName}"));
    }

    /// <summary>Get SMS messages for a container's phone number.</summary>
    [HttpGet("sms/{containerName}")]
    public async Task<ActionResult<List<SmsMessage>>> GetMessages(string containerName, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var messages = await _twilioService.GetMessagesAsync(containerName, limit, ct);
        return Ok(messages);
    }

    /// <summary>Send an SMS from a container's phone number.</summary>
    [HttpPost("sms/send")]
    public async Task<ActionResult<ApiResult>> SendSms([FromBody] SendSmsRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.ContainerName) || string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.Body))
            return BadRequest(ApiResult.Fail("Container name, 'to' number, and message body are required"));

        var success = await _twilioService.SendSmsAsync(request.ContainerName, request.To, request.Body, ct);
        if (!success)
            return BadRequest(ApiResult.Fail("Failed to send SMS. Check that the container has an assigned number."));

        return Ok(ApiResult.Ok("SMS sent successfully"));
    }

    /// <summary>Webhook endpoint for incoming SMS from Twilio.</summary>
    [HttpPost("webhook/sms")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> IncomingSmsWebhook(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var to = form["To"].ToString();
        var from = form["From"].ToString();
        var body = form["Body"].ToString();

        if (!string.IsNullOrEmpty(to) && !string.IsNullOrEmpty(from))
        {
            await _twilioService.RecordIncomingSmsAsync(to, from, body, ct);
        }

        // Return TwiML empty response
        return Content("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response></Response>", "application/xml");
    }
}
