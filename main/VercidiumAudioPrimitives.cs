namespace godot_raytraced_audio;

public partial class VercidiumAudio : Node
{
    void CollectPrimitivesRecursive(Node node, vaudio.MaterialType material)
    {
        // Use this specific material rather than the parent material
        if (node.HasMeta(MATERIAL_META_KEY))
            material = GetMaterial(node);

        // Ignore nodes without materials
        if (material == vaudio.MaterialType.Air)
            return;

        if (node is CsgBox3D csgBox)
            CreateVAudioPrimitive(csgBox, material);
        else if (node is CollisionShape3D collisionShape)
            CreateVAudioPrimitive(collisionShape, material);
        else if (node is MeshInstance3D meshInstance)
            CreateVAudioPrimitive(meshInstance, material);
        else
        {
            // TODO - support all Godot 3D objects   
        }

        foreach (Node child in node.GetChildren())
            CollectPrimitivesRecursive(child, material);
    }

    static void ForgetPrimitivesRecursive(Node node)
    {
        if (node.HasMeta(PRIMITIVE_META_KEY))
            node.RemoveMeta(PRIMITIVE_META_KEY);

        foreach (Node child in node.GetChildren())
            ForgetPrimitivesRecursive(child);
    }

    void CreateVAudioPrimitive(CsgBox3D csgBox, vaudio.MaterialType material)
    {
        Debug.Assert(material != vaudio.MaterialType.Air);

        // Skip if it's already been added to the raytracing scene
        if (csgBox.HasMeta(PRIMITIVE_META_KEY))
        {
            Debug.Assert(false);
            return;
        }

        vaudio.PrismPrimitive prim = new()
        {
            size = ToVAudio(csgBox.Size),
            transform = ToVAudio(csgBox.GlobalTransform),
            material = material
        };

        context.AddPrimitive(prim);

        // Store the primitive on the CSG node, so we can update it later if it moves
        csgBox.SetMeta(PRIMITIVE_META_KEY, new VercidiumAudioPrimitiveRef { Primitive = prim });
    }

    void CreateVAudioPrimitive(CollisionShape3D collisionShape, vaudio.MaterialType material)
    {
        Debug.Assert(material != vaudio.MaterialType.Air);

        // Skip if it's already been added to the raytracing scene
        if (collisionShape.HasMeta(PRIMITIVE_META_KEY))
        {
            Debug.Assert(false);
            return;
        }

        var shape = collisionShape.Shape;
        var globalTransform = collisionShape.GlobalTransform;
        var position = globalTransform.Origin;
        var scale = collisionShape.Scale;

        // Create primitive based on shape type
        vaudio.Primitive prim = null;

        if (shape is BoxShape3D box)
        {
            context.AddPrimitive(prim = new vaudio.PrismPrimitive()
            {
                size = ToVAudio(box.Size),
                transform = ToVAudio(globalTransform),
                material = material
            });
        }
        else if (shape is SphereShape3D)
        {
            context.AddPrimitive(prim = new vaudio.SpherePrimitive()
            {
                center = new vaudio.Vector3F(position.X, position.Y, position.Z),
                radius = scale.X,
                material = material
            });
        }
        else if (shape is ConcavePolygonShape3D polygon)
        {
            var triangles = ConvertConcavePolygonToVector3FList(polygon, out var min, out var max);
            var transform = ToVAudio(globalTransform);

            prim = new vaudio.MeshPrimitive(material, triangles, min, max, transform, true);
            context.AddPrimitive(prim);
        }
        else
        {
            // TODO - other primitive types
            GD.PrintErr($"CollisionShape3D not converted to raytraced primitive: {collisionShape.Name}");
            return;
        }

        Debug.Assert(prim != null);

        // Store the primitive on the collision shape, so we can update it later if it moves
        collisionShape.SetMeta(PRIMITIVE_META_KEY, new VercidiumAudioPrimitiveRef { Primitive = prim });
    }

    void CreateVAudioPrimitive(MeshInstance3D meshInstance, vaudio.MaterialType material)
    {
        Debug.Assert(material != vaudio.MaterialType.Air);

        // Skip if it's already been added to the raytracing scene
        if (meshInstance.HasMeta(PRIMITIVE_META_KEY))
        {
            Debug.Assert(false);
            return;
        }

        var mesh = meshInstance.Mesh;
        if (mesh == null)
        {
            // TODO - GD.PrintErr, or throw an exception?
            GD.PrintErr($"MeshInstance3D '{meshInstance.Name}' has no mesh assigned");
            return;
        }

        // Convert mesh to triangle list
        var triangles = ConvertMeshToVector3FList(mesh, out var min, out var max);

        if (triangles.Count == 0)
        {
            GD.PrintErr($"MeshInstance3D '{meshInstance.Name}' has no triangles");
            return;
        }

        var transform = ToVAudio(meshInstance.GlobalTransform);

        vaudio.MeshPrimitive prim = new(material, triangles, min, max, transform, true)
        {
            // TODO - make this a metadata / inspector flag in godot
            supportsPermeation = true
        };

        context.AddPrimitive(prim);

        var wrapper = new VercidiumAudioPrimitiveRef { Primitive = prim };
        meshInstance.SetMeta(PRIMITIVE_META_KEY, wrapper);

        GD.Print($"Created VAudio mesh primitive for '{meshInstance.Name}' with {triangles.Count / 3} triangles");
    }

    static void UpdatePrimitivesRecursive(Node node)
    {
        if (node.HasMeta(PRIMITIVE_META_KEY))
        {
            var wrapper = node.GetMeta(PRIMITIVE_META_KEY).As<VercidiumAudioPrimitiveRef>();
            var primitive = wrapper.Primitive;

            if (node is CsgBox3D csgBox && csgBox.HasMeta(PRIMITIVE_META_KEY))
            {
                var globalTransform = csgBox.GlobalTransform;

                if (primitive is vaudio.PrismPrimitive prism)
                {
                    prism.size = ToVAudio(csgBox.Size);
                    prism.transform = ToVAudio(globalTransform);
                }
            }
            else if (node is CollisionShape3D collisionShape && collisionShape.HasMeta(PRIMITIVE_META_KEY))
            {
                var globalTransform = collisionShape.GlobalTransform;

                // Update position/transform of vaudio primitives
                if (primitive is vaudio.MeshPrimitive mesh)
                {
                    mesh.transform = ToVAudio(globalTransform);
                }
                else if (primitive is vaudio.SpherePrimitive sphere)
                {
                    sphere.center = ToVAudio(globalTransform.Origin);
                    sphere.radius = collisionShape.Scale.X;
                }
                else if (primitive is vaudio.PrismPrimitive prism)
                {
                    var box = collisionShape.Shape as BoxShape3D;

                    prism.size = ToVAudio(box.Size);
                    prism.transform = ToVAudio(globalTransform);
                }
            }
            else if (node is MeshInstance3D meshInstance && meshInstance.HasMeta(PRIMITIVE_META_KEY))
            {
                var globalTransform = meshInstance.GlobalTransform;

                // Update transform of mesh primitive
                if (primitive is vaudio.MeshPrimitive mesh)
                {
                    mesh.transform = ToVAudio(globalTransform);
                }
            }
        }

        foreach (Node child in node.GetChildren())
        {
            UpdatePrimitivesRecursive(child);
        }
    }
}
