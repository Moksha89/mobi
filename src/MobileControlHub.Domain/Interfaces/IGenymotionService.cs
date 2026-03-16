namespace MobileControlHub.Domain.Interfaces;

/// <summary>
/// Service for managing Genymotion SaaS virtual Android devices via their HTTP API.
/// Supports listing recipes, starting/stopping disposable instances, and querying instance state.
/// </summary>
public interface IGenymotionService
{
    /// <summary>Whether the Genymotion API is configured with a valid token.</summary>
    bool IsConfigured { get; }

    /// <summary>List available device recipes (templates) from Genymotion SaaS.</summary>
    Task<List<GenymotionRecipe>> GetRecipesAsync(CancellationToken ct = default);

    /// <summary>List all running/creating instances.</summary>
    Task<List<GenymotionInstance>> GetInstancesAsync(CancellationToken ct = default);

    /// <summary>Get a single instance by UUID.</summary>
    Task<GenymotionInstance?> GetInstanceAsync(string instanceUuid, CancellationToken ct = default);

    /// <summary>Start a new disposable instance from a recipe UUID.</summary>
    Task<GenymotionInstance?> StartInstanceAsync(string recipeUuid, string instanceName, CancellationToken ct = default);

    /// <summary>Stop and destroy a disposable instance.</summary>
    Task<bool> StopInstanceAsync(string instanceUuid, CancellationToken ct = default);
}

/// <summary>A Genymotion recipe (device template).</summary>
public class GenymotionRecipe
{
    public string Uuid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AndroidVersion { get; set; } = string.Empty;
    public int ApiLevel { get; set; }
    public string FormFactor { get; set; } = "PHONE";
    public int CpuCount { get; set; }
    public int RamMb { get; set; }
    public int DiskMb { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public int ScreenDensity { get; set; }
    public bool IsOfficial { get; set; }
}

/// <summary>A running Genymotion virtual device instance.</summary>
public class GenymotionInstance
{
    public string Uuid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty; // CREATING, BOOTING, ONLINE, SAVING, STOPPING, RECYCLING, DELETED
    public string RecipeUuid { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string AndroidVersion { get; set; } = string.Empty;
    public string FormFactor { get; set; } = "PHONE";
    public int CpuCount { get; set; }
    public int RamMb { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public int ScreenDensity { get; set; }
    public string WebrtcUrl { get; set; } = string.Empty;
    public string AdbUrl { get; set; } = string.Empty;
    public string StreamerFqdn { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
