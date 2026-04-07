namespace godot_raytraced_audio;

public partial class VercidiumAudio : Node
{
    private Dictionary<int, VercidiumAudioMaterial> customMaterials = [];

    /// <summary>
    /// Create materials from all child RaytracedAudioMaterial nodes.
    /// Call this after creating the RaytracingContext.
    /// </summary>
    void RegisterCustomMaterials()
    {
        customMaterials.Clear();

        foreach (var child in GetChildren())
            if (child is VercidiumAudioMaterial material)
                RegisterMaterial(material);
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

            if (contextMaterial.AbsorptionLF != material.AbsorptionLF)
            {
                contextMaterial.AbsorptionLF = material.AbsorptionLF;
                context.MaterialsDirty = true;
            }

            if (contextMaterial.AbsorptionHF != material.AbsorptionHF)
            {
                contextMaterial.AbsorptionHF = material.AbsorptionHF;
                context.MaterialsDirty = true;
            }

            if (contextMaterial.Scattering != material.Scattering)
            {
                contextMaterial.Scattering = material.Scattering;
                context.MaterialsDirty = true;
            }

            if (contextMaterial.TransmissionLF != material.TransmissionLF)
            {
                contextMaterial.TransmissionLF = material.TransmissionLF;
                context.MaterialsDirty = true;
            }

            if (contextMaterial.TransmissionHF != material.TransmissionHF)
            {
                contextMaterial.TransmissionHF = material.TransmissionHF;
                context.MaterialsDirty = true;
            }
        }
    }

    /// <summary>
    /// Register a material at runtime
    /// </summary>
    public void RegisterMaterial(VercidiumAudioMaterial material)
    {
        if (material == null)
        {
            LogError("Cannot register a null material");
            return;
        }

        if (material.MaterialId < 1000)
        {
            LogError($"Custom material {material.MaterialName} has an invalid ID: {material.MaterialId}. Must be >= 1000");
            return;
        }

        if (customMaterials.TryGetValue(material.MaterialId, out var existingMaterial))
        {
            LogError($"Custom material {material.MaterialName} has a duplicate ID: {material.MaterialId} which is already used by the {existingMaterial.Name} material");
            return;
        }

        var materialType = (vaudio.MaterialType)material.MaterialId;
        var (r, g, b) = material.GetDebugColorRGB();
        var vaudioColour = new vaudio.Color(r, g, b, 255);

        if (context.HasMaterial(materialType))
        {
            var mat = context.GetMaterial(materialType);

            mat.AbsorptionLF = material.AbsorptionLF;
            mat.AbsorptionHF = material.AbsorptionHF;
            mat.Scattering = material.Scattering;
            mat.TransmissionLF = material.TransmissionLF;
            mat.TransmissionHF = material.TransmissionHF;

            context.SetMaterialColor(materialType, vaudioColour);
        }
        else
        {
            context.AddMaterial(materialType, material.CreateProperties(), vaudioColour);
        }

        customMaterials[material.MaterialId] = material;

        context.MaterialsDirty = true;
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
                if (kvp.Value.MaterialName.Equals(materialString, StringComparison.CurrentCultureIgnoreCase))
                {
                    return (vaudio.MaterialType)kvp.Key;
                }
            }

            // Fall back to built-in materials
            if (DefaultMaterialDict.TryGetValue(materialString, out var type))
                return type;

            LogWarning($"Unknown material string for node {node.Name}: {materialString}. Defaulting to 'air'");
        }

        return vaudio.MaterialType.Air;
    }
}
