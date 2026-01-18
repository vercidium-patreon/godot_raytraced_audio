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
        else if (node is CsgCylinder3D csgCylinder)
        {
            // TODO
        }
        else if (node is CsgSphere3D csgSphere)
        {
            // TODO
        }
        else if (node is CollisionShape3D collisionShape)
            CreateVAudioPrimitive(collisionShape, material);
        else if (node is MeshInstance3D meshInstance)
            CreateVAudioPrimitive(meshInstance, material);

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
        else if (shape is SphereShape3D sphere)
        {
            context.AddPrimitive(prim = new vaudio.SpherePrimitive()
            {
                center = new vaudio.Vector3F(position.X, position.Y, position.Z),
                radius = sphere.Radius * scale.X,
                material = material
            });
        }
        else if (shape is CapsuleShape3D capsule)
        {
            // Godot CapsuleShape3D: height is total height including hemispherical caps, radius is the radius
            // VAudio CapsulePrimitive: length is the cylinder portion (not including caps), radius is the radius
            // The cylinder length = total height - 2 * radius
            float cylinderLength = capsule.Height - 2 * capsule.Radius;
            if (cylinderLength < 0) cylinderLength = 0;

            context.AddPrimitive(prim = new vaudio.CapsulePrimitive()
            {
                radius = capsule.Radius * scale.X,
                length = cylinderLength * scale.Y,
                transform = ToVAudio(globalTransform),
                material = material
            });
        }
        else if (shape is CylinderShape3D cylinder)
        {
            context.AddPrimitive(prim = new vaudio.CylinderPrimitive()
            {
                radius = cylinder.Radius * scale.X,
                length = cylinder.Height * scale.Y,
                transform = ToVAudio(globalTransform),
                material = material
            });
        }
        else if (shape is WorldBoundaryShape3D worldBoundary)
        {
            // WorldBoundaryShape3D represents an infinite plane, we approximate with a large plane
            var plane = worldBoundary.Plane;
            var normal = plane.Normal;

            // Create a transform that aligns the plane primitive with the world boundary
            // VAudio PlanePrimitive lies in XZ plane at Y=0 in local space, with Y-up as the normal
            // So we need basisY to be the plane normal
            var basisY = new Vector3(normal.X, normal.Y, normal.Z);
            var basisX = basisY.Cross(Vector3.Forward).Normalized();
            if (basisX.LengthSquared() < 0.001f)
                basisX = basisY.Cross(Vector3.Right).Normalized();
            var basisZ = basisX.Cross(basisY).Normalized();

            // The plane position is: point on plane (normal * D) + the collision shape's global position
            var planePosition = normal * plane.D + globalTransform.Origin;

            var planeTransform = new Transform3D(
                new Basis(basisX, basisY, basisZ),
                planePosition
            );

            context.AddPrimitive(prim = new vaudio.PlanePrimitive()
            {
                width = 1000f,  // Large approximation for infinite plane
                height = 1000f,
                transform = ToVAudio(planeTransform),
                material = material
            });
        }
        else if (shape is ConvexPolygonShape3D convexPolygon)
        {
            var triangles = ConvertConvexPolygonToVector3FList(convexPolygon, out var min, out var max);
            var transform = ToVAudio(globalTransform);

            if (triangles.Count > 0)
            {
                prim = new vaudio.MeshPrimitive(material, triangles, min, max, transform, true);
                context.AddPrimitive(prim);
            }
            else
            {
                GD.PrintErr($"ConvexPolygonShape3D '{collisionShape.Name}' has no valid triangles");
                return;
            }
        }
        else if (shape is HeightMapShape3D heightMap)
        {
            var triangles = ConvertHeightMapToVector3FList(heightMap, out var min, out var max);
            var transform = ToVAudio(globalTransform);

            if (triangles.Count > 0)
            {
                prim = new vaudio.MeshPrimitive(material, triangles, min, max, transform, true);
                context.AddPrimitive(prim);
            }
            else
            {
                GD.PrintErr($"HeightMapShape3D '{collisionShape.Name}' has no valid triangles");
                return;
            }
        }
        else if (shape is ConcavePolygonShape3D polygon)
        {
            var triangles = ConvertConcavePolygonToVector3FList(polygon, out var min, out var max);
            var transform = ToVAudio(globalTransform);

            prim = new vaudio.MeshPrimitive(material, triangles, min, max, transform, true);
            context.AddPrimitive(prim);
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
                    sphere.radius = sphere.radius * collisionShape.Scale.X;
                }
                else if (primitive is vaudio.PrismPrimitive prism)
                {
                    var box = collisionShape.Shape as BoxShape3D;

                    prism.size = ToVAudio(box.Size);
                    prism.transform = ToVAudio(globalTransform);
                }
                else if (primitive is vaudio.CapsulePrimitive capsulePrim)
                {
                    var capsule = collisionShape.Shape as CapsuleShape3D;
                    var scale = collisionShape.Scale;

                    float cylinderLength = capsule.Height - 2 * capsule.Radius;
                    if (cylinderLength < 0) cylinderLength = 0;

                    capsulePrim.radius = capsule.Radius * scale.X;
                    capsulePrim.length = cylinderLength * scale.Y;
                    capsulePrim.transform = ToVAudio(globalTransform);
                }
                else if (primitive is vaudio.CylinderPrimitive cylinderPrim)
                {
                    var cylinder = collisionShape.Shape as CylinderShape3D;
                    var scale = collisionShape.Scale;

                    cylinderPrim.radius = cylinder.Radius * scale.X;
                    cylinderPrim.length = cylinder.Height * scale.Y;
                    cylinderPrim.transform = ToVAudio(globalTransform);
                }
                else if (primitive is vaudio.PlanePrimitive planePrim)
                {
                    var worldBoundary = collisionShape.Shape as WorldBoundaryShape3D;
                    var plane = worldBoundary.Plane;
                    var normal = plane.Normal;

                    // VAudio PlanePrimitive lies in XZ plane at Y=0 in local space, with Y-up as the normal
                    var basisY = new Vector3(normal.X, normal.Y, normal.Z);
                    var basisX = basisY.Cross(Vector3.Forward).Normalized();
                    if (basisX.LengthSquared() < 0.001f)
                        basisX = basisY.Cross(Vector3.Right).Normalized();
                    var basisZ = basisX.Cross(basisY).Normalized();

                    // The plane position is: point on plane (normal * D) + the collision shape's global position
                    var planePosition = normal * plane.D + globalTransform.Origin;

                    var planeTransform = new Transform3D(
                        new Basis(basisX, basisY, basisZ),
                        planePosition
                    );

                    planePrim.transform = ToVAudio(planeTransform);
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
