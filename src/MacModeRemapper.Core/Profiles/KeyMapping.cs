using System.Text.Json.Serialization;
using MacModeRemapper.Core.Engine;

namespace MacModeRemapper.Core.Profiles;

/// <summary>
/// Represents a single mapping rule as stored in profile JSON.
/// </summary>
public sealed class KeyMappingDto
{
    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;
}

/// <summary>
/// Parsed/compiled form of a mapping rule for fast lookup.
/// </summary>
public sealed class CompiledMapping
{
    /// <summary>Modifiers that must be active alongside Left Alt for the trigger.</summary>
    public ModifierFlags TriggerModifiers { get; init; }

    /// <summary>The virtual key code of the trigger key (the non-modifier key).</summary>
    public int TriggerVk { get; init; }

    /// <summary>Modifier virtual key codes to press for the action.</summary>
    public int[] ActionModifierVks { get; init; } = Array.Empty<int>();

    /// <summary>The virtual key code to press for the action.</summary>
    public int ActionVk { get; init; }

    public override string ToString() => $"Trigger=({TriggerModifiers}+VK 0x{TriggerVk:X2}) -> Action=(Mods[{string.Join(",", ActionModifierVks.Select(v => $"0x{v:X2}"))}]+VK 0x{ActionVk:X2})";
}
