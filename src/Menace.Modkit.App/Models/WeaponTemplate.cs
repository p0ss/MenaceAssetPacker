using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Weapon template with combat stats
/// </summary>
public class WeaponTemplate : DataTemplate
{
    [JsonPropertyName("MinRange")]
    public int MinRange { get; set; }

    [JsonPropertyName("IdealRange")]
    public int IdealRange { get; set; }

    [JsonPropertyName("MaxRange")]
    public int MaxRange { get; set; }

    [JsonPropertyName("AccuracyBonus")]
    public float AccuracyBonus { get; set; }

    [JsonPropertyName("AccuracyDropoff")]
    public float AccuracyDropoff { get; set; }

    [JsonPropertyName("Damage")]
    public float Damage { get; set; }

    [JsonPropertyName("DamageDropoff")]
    public float DamageDropoff { get; set; }

    [JsonPropertyName("ArmorPenetration")]
    public float ArmorPenetration { get; set; }

    [JsonPropertyName("ArmorPenetrationDropoff")]
    public float ArmorPenetrationDropoff { get; set; }
}
