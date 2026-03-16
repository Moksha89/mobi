using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MobileControlHub.Domain.Interfaces;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Genymotion SaaS HTTP API client.
/// Manages virtual Android device instances in the cloud via recipes.
/// API docs: https://developer.genymotion.com/saas/
/// </summary>
public class GenymotionService : IGenymotionService
{
    private const string BaseUrl = "https://api.geny.io/cloud/";
    private readonly HttpClient _http;
    private readonly ILogger<GenymotionService> _logger;
    private readonly string? _apiToken;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GenymotionService(ILogger<GenymotionService> logger)
    {
        _logger = logger;
        _apiToken = Environment.GetEnvironmentVariable("GENYMOTION_API_TOKEN");
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        if (!string.IsNullOrEmpty(_apiToken))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-token", _apiToken);
        }
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MobileControlHub/1.0");
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_apiToken);

    public async Task<List<GenymotionRecipe>> GetRecipesAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return new List<GenymotionRecipe>();

        try
        {
            var response = await _http.GetAsync("v1/recipes", ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<RecipesV1Response>(json, JsonOpts);
            if (data == null) return new List<GenymotionRecipe>();

            var result = new List<GenymotionRecipe>();
            foreach (var r in data.Base ?? new List<RawRecipe>())
            {
                result.Add(MapRecipe(r));
            }
            foreach (var r in data.User ?? new List<RawRecipe>())
            {
                result.Add(MapRecipe(r));
            }
            foreach (var r in data.Shared ?? new List<RawRecipe>())
            {
                result.Add(MapRecipe(r));
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Genymotion recipes");
            return new List<GenymotionRecipe>();
        }
    }

    public async Task<List<GenymotionInstance>> GetInstancesAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return new List<GenymotionInstance>();

        try
        {
            var response = await _http.GetAsync("v2/instances", ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<InstancesV2Response>(json, JsonOpts);
            if (data?.Results == null) return new List<GenymotionInstance>();

            return data.Results.Select(MapInstance).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Genymotion instances");
            return new List<GenymotionInstance>();
        }
    }

    public async Task<GenymotionInstance?> GetInstanceAsync(string instanceUuid, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        try
        {
            var response = await _http.GetAsync($"v1/instances/{instanceUuid}", ct);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(ct);
            var raw = JsonSerializer.Deserialize<RawInstance>(json, JsonOpts);
            return raw != null ? MapInstance(raw) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Genymotion instance {Uuid}", instanceUuid);
            return null;
        }
    }

    public async Task<GenymotionInstance?> StartInstanceAsync(string recipeUuid, string instanceName, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        try
        {
            var payload = new { instance_name = instanceName, rename_on_conflict = true };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"v1/recipes/{recipeUuid}/start-disposable", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to start Genymotion instance: {Status} {Body}", response.StatusCode, errBody);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var raw = JsonSerializer.Deserialize<RawInstance>(json, JsonOpts);
            if (raw == null) return null;

            _logger.LogInformation("Started Genymotion instance {Name} ({Uuid}) from recipe {Recipe}",
                raw.Name, raw.Uuid, recipeUuid);

            return MapInstance(raw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Genymotion instance from recipe {Recipe}", recipeUuid);
            return null;
        }
    }

    public async Task<bool> StopInstanceAsync(string instanceUuid, CancellationToken ct = default)
    {
        if (!IsConfigured) return false;

        try
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"v1/instances/{instanceUuid}/stop-disposable", content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Stopped Genymotion instance {Uuid}", instanceUuid);
                return true;
            }

            var errBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to stop Genymotion instance {Uuid}: {Status} {Body}",
                instanceUuid, response.StatusCode, errBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Genymotion instance {Uuid}", instanceUuid);
            return false;
        }
    }

    // --- Internal models for JSON deserialization ---

    private static GenymotionRecipe MapRecipe(RawRecipe r)
    {
        var recipe = new GenymotionRecipe
        {
            Uuid = r.Uuid ?? string.Empty,
            Name = r.Name ?? string.Empty,
            IsOfficial = r.IsOfficial,
        };

        foreach (var item in r.Items ?? new List<RecipeItem>())
        {
            switch (item.Type)
            {
                case "description":
                    recipe.Description = item.Data?.Value ?? string.Empty;
                    break;
                case "ova":
                    recipe.AndroidVersion = item.Data?.AndroidVersion ?? string.Empty;
                    recipe.ApiLevel = item.Data?.ApiLevel ?? 0;
                    break;
                case "screen":
                    recipe.ScreenWidth = item.Data?.Width ?? 0;
                    recipe.ScreenHeight = item.Data?.Height ?? 0;
                    recipe.ScreenDensity = item.Data?.Density ?? 0;
                    break;
                case "memory":
                    recipe.RamMb = item.Data?.Size ?? 0;
                    break;
                case "processor":
                    recipe.CpuCount = item.Data?.Number ?? 0;
                    break;
                case "datadisk":
                    recipe.DiskMb = item.Data?.Size ?? 0;
                    break;
            }
        }

        return recipe;
    }

    private static GenymotionInstance MapInstance(RawInstance r)
    {
        return new GenymotionInstance
        {
            Uuid = r.Uuid ?? string.Empty,
            Name = r.Name ?? string.Empty,
            State = r.State ?? "UNKNOWN",
            RecipeUuid = r.RecipeUuid ?? r.Recipe?.Uuid ?? string.Empty,
            RecipeName = r.Recipe?.Name ?? string.Empty,
            AndroidVersion = r.OsImage?.Name ?? string.Empty,
            FormFactor = r.HardwareProfile?.FormFactor ?? "PHONE",
            CpuCount = r.HardwareProfile?.CpuCount ?? 0,
            RamMb = r.HardwareProfile?.RamSize ?? 0,
            ScreenWidth = r.HardwareProfile?.Width ?? 0,
            ScreenHeight = r.HardwareProfile?.Height ?? 0,
            ScreenDensity = r.HardwareProfile?.Density ?? 0,
            WebrtcUrl = r.WebrtcUrl ?? string.Empty,
            AdbUrl = r.AdbUrl ?? string.Empty,
            StreamerFqdn = r.StreamerFqdn ?? string.Empty,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
        };
    }

    // --- Raw JSON models ---

    private class RecipesV1Response
    {
        public List<RawRecipe>? Base { get; set; }
        public List<RawRecipe>? User { get; set; }
        public List<RawRecipe>? Shared { get; set; }
    }

    private class InstancesV2Response
    {
        public int Count { get; set; }
        public List<RawInstance>? Results { get; set; }
    }

    private class RawRecipe
    {
        public string? Uuid { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("is_official")]
        public bool IsOfficial { get; set; }
        public List<RecipeItem>? Items { get; set; }
    }

    private class RecipeItem
    {
        public string? Uuid { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public RecipeItemData? Data { get; set; }
    }

    private class RecipeItemData
    {
        public string? Value { get; set; }
        [JsonPropertyName("android_version")]
        public string? AndroidVersion { get; set; }
        [JsonPropertyName("api_level")]
        public int? ApiLevel { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? Density { get; set; }
        public int? Size { get; set; }
        public int? Number { get; set; }
    }

    private class RawInstance
    {
        public string? Uuid { get; set; }
        public string? Name { get; set; }
        public string? State { get; set; }
        [JsonPropertyName("recipe_uuid")]
        public string? RecipeUuid { get; set; }
        public RawRecipe? Recipe { get; set; }
        [JsonPropertyName("hardware_profile")]
        public RawHardwareProfile? HardwareProfile { get; set; }
        [JsonPropertyName("os_image")]
        public RawOsImage? OsImage { get; set; }
        [JsonPropertyName("webrtc_url")]
        public string? WebrtcUrl { get; set; }
        [JsonPropertyName("adb_url")]
        public string? AdbUrl { get; set; }
        [JsonPropertyName("streamer_fqdn")]
        public string? StreamerFqdn { get; set; }
        [JsonPropertyName("file_upload_url")]
        public string? FileUploadUrl { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    private class RawHardwareProfile
    {
        public string? Uuid { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("form_factor")]
        public string? FormFactor { get; set; }
        [JsonPropertyName("cpu_count")]
        public int CpuCount { get; set; }
        [JsonPropertyName("ram_size")]
        public int RamSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Density { get; set; }
    }

    private class RawOsImage
    {
        public string? Uuid { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("image_version")]
        public string? ImageVersion { get; set; }
        public string? Arch { get; set; }
    }
}
