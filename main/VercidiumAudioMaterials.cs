namespace godot_raytraced_audio;

public partial class VercidiumAudio : Node
{
    private Dictionary<int, VercidiumAudioMaterial> customMaterials = [];

    /// <summary>
    /// Create materials from all child RaytracedAudioMaterial nodes.
    /// Call this before creating the RaytracingContext.
    /// </summary>
    void RegisterCustomMaterials(vaudio.RaytracingContextSettings settings)
    {
        customMaterials.Clear();

        foreach (var child in GetChildren())
        {
            var material = child as VercidiumAudioMaterial;
            if (material != null)
            {
                // Validate material ID
                if (material.MaterialId < 1000)
                {
                    GD.PrintErr($"Custom material '{material.MaterialName}' has invalid ID {material.MaterialId}. Must be >= 1000. Skipping.");
                    continue;
                }

                if (customMaterials.ContainsKey(material.MaterialId))
                {
                    GD.PrintErr($"Duplicate material ID {material.MaterialId} for '{material.MaterialName}'. Skipping.");
                    continue;
                }

                // Register with VAudio context settings
                var materialType = (vaudio.MaterialType)material.MaterialId;
                settings.materials.properties[materialType] = material.CreateProperties();

                var rgb = material.GetDebugColorRGB();
                settings.materials.colors[materialType] = new(rgb.r, rgb.g, rgb.b);

                // Store in registry
                customMaterials[material.MaterialId] = material;

                GD.Print($"Registered custom material: '{material.MaterialName}' (ID: {material.MaterialId})");
            }
        }
    }

    // TODO - remove this, and update the context when the materials themselves are edited.
    /// <summary>
    /// Apply material property updates after a context has been created.
    /// </summary>
    void ApplyMaterialUpdates()
    {
        foreach (var kvp in customMaterials)
        {
            var material = kvp.Value;
            var materialType = (vaudio.MaterialType)kvp.Key;
            var contextMaterial = context.GetMaterial(materialType);

            contextMaterial.AbsorptionLF = material.AbsorptionLF;
            contextMaterial.AbsorptionHF = material.AbsorptionHF;
            contextMaterial.ScatteringLF = material.ScatteringLF;
            contextMaterial.ScatteringHF = material.ScatteringHF;
            contextMaterial.TransmissionLF = material.TransmissionLF;
            contextMaterial.TransmissionHF = material.TransmissionHF;
        }
    }

    /// <summary>
    /// Register a material at runtime
    /// </summary>
    public void RegisterMaterial(VercidiumAudioMaterial material)
    {
        // TODO - unsure if we'll allow creating custom materials after the context has been created
        //if (context != null)
        //    throw new InvalidOperationException("Cannot create materials after the context has been created");

        if (material == null)
            throw new InvalidOperationException("Cannot register a null material");

        if (material.MaterialId < 1000)
            throw new InvalidOperationException($"Custom material '{material.MaterialName}' has invalid ID {material.MaterialId}. Must be >= 1000.");

        if (customMaterials.TryGetValue(material.MaterialId, out var existingMaterial))
            throw new InvalidOperationException($"Material ID {material.MaterialId} is already used by the {existingMaterial.Name} material.");

        // Register with active context
        var materialType = (vaudio.MaterialType)material.MaterialId;
        var contextMaterial = context.GetMaterial(materialType);

        // Update material properties (these are the properties exposed by vaudio.Material)
        contextMaterial.AbsorptionLF = material.AbsorptionLF;
        contextMaterial.AbsorptionHF = material.AbsorptionHF;
        contextMaterial.ScatteringLF = material.ScatteringLF;
        contextMaterial.ScatteringHF = material.ScatteringHF;
        contextMaterial.TransmissionLF = material.TransmissionLF;
        contextMaterial.TransmissionHF = material.TransmissionHF;

        customMaterials[material.MaterialId] = material;

        GD.Print($"Custom material registered: '{material.MaterialName}' (ID: {material.MaterialId})");
    }

    /// <summary>
    /// Extract a material type from a node. Returns Air if no materials are found
    /// </summary>
    vaudio.MaterialType GetMaterial(Node node)
    {
        // Priority 2: Check for legacy string-based material
        if (node.HasMeta(MATERIAL_META_KEY))
        {
            var materialString = node.GetMeta(MATERIAL_META_KEY).As<string>();

            // Check if it's a custom material name
            foreach (var kvp in customMaterials)
            {
                if (kvp.Value.MaterialName.ToLower() == materialString.ToLower())
                {
                    return (vaudio.MaterialType)kvp.Key;
                }
            }

            // Fall back to built-in materials
            if (DefaultMaterialDict.TryGetValue(materialString, out var type))
                return type;

            GD.PrintErr($"Unknown material string for {node.Name}: {materialString}, defaulting to Air");
        }

        return vaudio.MaterialType.Air;
    }

}
