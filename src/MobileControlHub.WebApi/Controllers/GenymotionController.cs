using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

/// <summary>
/// Manages Genymotion SaaS virtual Android devices.
/// Supports listing recipes, starting/stopping instances, and querying instance state.
/// </summary>
[ApiController]
[Route("api/genymotion")]
public class GenymotionController : ControllerBase
{
    private readonly IGenymotionService _genyService;
    private readonly ITwilioService _twilioService;
    private readonly ILogService _logService;

    public GenymotionController(
        IGenymotionService genyService,
        ITwilioService twilioService,
        ILogService logService)
    {
        _genyService = genyService;
        _twilioService = twilioService;
        _logService = logService;
    }

    /// <summary>Check if Genymotion API is configured.</summary>
    [HttpGet("status")]
    public ActionResult GetStatus()
    {
        return Ok(new { isConfigured = _genyService.IsConfigured });
    }

    /// <summary>List available device recipes (templates).</summary>
    [HttpGet("recipes")]
    public async Task<ActionResult<List<GenymotionRecipe>>> GetRecipes(CancellationToken ct)
    {
        if (!_genyService.IsConfigured)
            return BadRequest(ApiResult.Fail("Genymotion API token not configured. Set GENYMOTION_API_TOKEN environment variable."));

        var recipes = await _genyService.GetRecipesAsync(ct);
        return Ok(recipes);
    }

    /// <summary>List all running Genymotion instances.</summary>
    [HttpGet("instances")]
    public async Task<ActionResult<List<GenymotionInstance>>> GetInstances(CancellationToken ct)
    {
        if (!_genyService.IsConfigured)
            return BadRequest(ApiResult.Fail("Genymotion API token not configured."));

        var instances = await _genyService.GetInstancesAsync(ct);
        return Ok(instances);
    }

    /// <summary>Get a single instance by UUID.</summary>
    [HttpGet("instances/{uuid}")]
    public async Task<ActionResult<GenymotionInstance>> GetInstance(string uuid, CancellationToken ct)
    {
        var instance = await _genyService.GetInstanceAsync(uuid, ct);
        if (instance == null)
            return NotFound(ApiResult.Fail("Instance not found"));
        return Ok(instance);
    }

    /// <summary>Start a new Genymotion virtual device from a recipe.</summary>
    [HttpPost("instances/start")]
    public async Task<ActionResult> StartInstance([FromBody] StartGenymotionRequest request, CancellationToken ct)
    {
        if (!_genyService.IsConfigured)
            return BadRequest(ApiResult.Fail("Genymotion API token not configured."));

        if (string.IsNullOrEmpty(request.RecipeUuid))
            return BadRequest(ApiResult.Fail("Recipe UUID is required."));

        var name = string.IsNullOrWhiteSpace(request.InstanceName)
            ? $"mch-device-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
            : request.InstanceName;

        var instance = await _genyService.StartInstanceAsync(request.RecipeUuid, name, ct);
        if (instance == null)
            return BadRequest(ApiResult.Fail("Failed to start Genymotion instance. Check API token and recipe UUID."));

        // Auto-provision Twilio number if configured
        string? phoneNumber = null;
        if (request.AssignPhoneNumber && _twilioService.IsConfigured)
        {
            var containerKey = $"geny-{instance.Uuid[..8]}";
            var number = await _twilioService.ProvisionNumberAsync(containerKey, ct);
            phoneNumber = number?.PhoneNumber;
        }

        await _logService.LogInfoAsync(
            $"Started Genymotion device '{instance.Name}' ({instance.RecipeName}, Android {instance.AndroidVersion})" +
            (phoneNumber != null ? $" with phone {phoneNumber}" : ""),
            category: "Genymotion");

        return Ok(new
        {
            success = true,
            message = $"Started '{instance.Name}' successfully",
            instance,
            phoneNumber
        });
    }

    /// <summary>Stop and destroy a Genymotion instance.</summary>
    [HttpPost("instances/{uuid}/stop")]
    public async Task<ActionResult<ApiResult>> StopInstance(string uuid, CancellationToken ct)
    {
        if (!_genyService.IsConfigured)
            return BadRequest(ApiResult.Fail("Genymotion API token not configured."));

        // Release Twilio number if assigned
        var containerKey = $"geny-{uuid[..8]}";
        if (_twilioService.IsConfigured)
        {
            await _twilioService.ReleaseNumberAsync(containerKey, ct);
        }

        var success = await _genyService.StopInstanceAsync(uuid, ct);
        if (!success)
            return BadRequest(ApiResult.Fail("Failed to stop instance."));

        await _logService.LogInfoAsync($"Stopped Genymotion instance {uuid}", category: "Genymotion");
        return Ok(ApiResult.Ok("Instance stopped successfully"));
    }
}
