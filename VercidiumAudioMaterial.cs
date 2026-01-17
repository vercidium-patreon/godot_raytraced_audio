using Godot;
using System;
using System.Collections.Generic;

namespace OpenALAudio;

/// <summary>
/// Custom acoustic material resource for VAudio raytraced audio.
/// Can be created in the Godot editor and assigned to collision shapes.
/// Define as a child Node of a VercidiumAudio node.
/// </summary>
[Tool]
[GlobalClass]
public partial class VercidiumAudioMaterial : Node
{
    private int _materialId = 1000;
    private string _materialName = "CustomMaterial";

    /// <summary>
    /// Unique material ID. Must be >= 1000 to avoid conflicts with built-in materials.
    /// </summary>
    [Export(PropertyHint.Range, "1000,9999,1,or_greater,no_slider")]
    public int MaterialId
    {
        get => _materialId;
        set
        {
            _materialId = value;
            UpdateConfigurationWarnings();
        }
    }

    /// <summary>
    /// Material name for debugging and identification
    /// </summary>
    [Export(PropertyHint.None, "")]
    public string MaterialName
    {
        get => _materialName;
        set
        {
            _materialName = value;
            UpdateConfigurationWarnings();
        }
    }

    /// <summary>
    /// Low-frequency absorption coefficient (0.0 to 1.0).
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")] public float AbsorptionLF { get; set; } = 0.02f;

    /// <summary>
    /// High-frequency absorption coefficient (0.0 to 1.0).
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")] public float AbsorptionHF { get; set; } = 0.1f;

    /// <summary>
    /// Low-frequency scattering coefficient (0.0 to 1.0).
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")] public float ScatteringLF { get; set; } = 0.3f;

    /// <summary>
    /// High-frequency scattering coefficient (0.0 to 1.0).
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")] public float ScatteringHF { get; set; } = 0.5f;

    /// <summary>
    /// Low-frequency transmission in dB/m (0.0 or greater).
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1000.0")] public float TransmissionLF { get; set; } = 100;

    /// <summary>
    /// High-frequency transmission in dB/m (0.0 or greater).
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1000.0")] public float TransmissionHF { get; set; } = 150;

    /// <summary>
    /// Debug color for visualization in VAudio's debug renderer
    /// </summary>
    [Export] public Godot.Color DebugColor { get; set; } = new Godot.Color(1, 0, 1); // Pink default

    /// <summary>
    /// Creates the VAudio material properties from this resource
    /// </summary>
    public vaudio.MaterialProperties CreateProperties()
    {
        return new vaudio.MaterialProperties(
            AbsorptionLF,
            AbsorptionHF,
            ScatteringLF,
            ScatteringHF,
            TransmissionLF,
            TransmissionHF
        );
    }

    /// <summary>
    /// Gets the debug color as RGB byte values for VAudio
    /// </summary>
    public (byte r, byte g, byte b) GetDebugColorRGB()
    {
        return (
            (byte)(DebugColor.R * 255),
            (byte)(DebugColor.G * 255),
            (byte)(DebugColor.B * 255)
        );
    }

    /// <summary>
    /// Validates the material configuration and returns warnings
    /// </summary>
    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (MaterialId < 1000)
        {
            warnings.Add($"Material ID must be >= 1000 (current: {MaterialId}). IDs 0-999 are reserved for built-in materials.");
        }

        if (string.IsNullOrWhiteSpace(MaterialName))
        {
            warnings.Add("Material Name should not be empty.");
        }

        return warnings.ToArray();
    }
}
