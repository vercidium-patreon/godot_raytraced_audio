namespace godot_raytraced_audio;

public partial class VercidiumAudio : Node
{
    public Dictionary<int, VercidiumAudioMaterial> customMaterials = [];

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
