using Godot;
using OpenAL.managed;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using vaudio;

namespace OpenALAudio;

public partial class VercidiumAudio : Node
{
    private vaudio.RaytracingContext context;

    [Export] public Node3D SceneRoot;
    [Export] public AudioStreamPlayer3D[] AudioSources;

    public ALFilter ambientFilter;

    public const string PRIMITIVE_META_KEY = "vercidium_audio_primitive";
    public const string MATERIAL_META_KEY = "vercidium_audio_material";
    public const string MATERIAL_RESOURCE_META_KEY = "vercidium_audio_material_resource";

    // Registry of custom materials loaded from child RaytracedAudioMaterial resources
    private Dictionary<int, VercidiumAudioMaterial> customMaterials = new();

    partial class VAudioPrimitiveRef : RefCounted
    {
        public vaudio.Primitive Primitive { get; set; }
    }

    public override void _Ready()
    {
        GD.Print("VercidiumAudio ready!");

        var settings = new vaudio.RaytracingContextSettings()
        {
            worldPosition = new(-100, 0, -100),
            worldSize = new(200, 100, 200),
            renderingEnabled = true,
            maxVoices = 8,
            reverbRayCount = 256,
            occlusionRayCount = 256,
            permeationRayCount = 256,
            trailBounceCount = 8,
            maximumGroupedEAXCount = 3,
            voiceReverbRayCount = 32,
            voiceReverbBounceCount = 8,
            logCallback = GD.Print,
        };

        // Register custom materials from child RaytracedAudioMaterial resources
        RegisterCustomMaterials(settings);

        // Create the raytracing context on startup
        context = new(settings);
        context.OnReverbUpdated = UpdateGodotReverb;

        // Apply runtime material updates for registered custom materials
        ApplyRuntimeMaterialUpdates();

        // Wait a frame for the scene to be fully loaded
        CallDeferred(nameof(InitializeScene));

        listenerReverbEffect = new();
        outsideReverbEffect = new();

        if (listenerReverbEffect.effectID == 0)
            GD.PrintErr("Failed to initialise reverb effect");
    }

    public override void _Process(double delta)
    {
        var camera = GetViewport().GetCamera3D();
        var pos = camera.GlobalPosition;

        var cameraPosition = new vaudio.Vector3F(pos.X, pos.Y, pos.Z);
        var cameraPitch = camera.GlobalRotation.X;
        var cameraYaw = camera.GlobalRotation.Y;
        var fieldOfView = 90 / 180.0f * MathF.PI;

        context.UpdateListener(cameraPosition, cameraPitch, cameraYaw);
        context.SetRenderView(cameraPosition, cameraPitch, cameraYaw, fieldOfView);

        context.Update();
    }

    public override void _PhysicsProcess(double delta)
    {
        RefreshRoot();

        if (SceneRoot == null)
            return;

        UpdatePrimitivesRecursive(SceneRoot);
    }

    // Minimal AttachVoice method exposed to GDScript
    public Voice AttachVoice(Node3D node, Action OnRaytracingComplete)
    {
        // Create new voice
        var voice = context.CreateVoice(new vaudio.FuncPositionF(() => ToVAudio(node.GlobalPosition)));
        voice.OnRaytracingComplete = OnRaytracingComplete;

        return voice;
    }

    public void DetachVoice(Node3D node, Voice voice)
    {
        Debug.Assert(voice != null);

        context.RemoveVoice(voice);
        GD.Print($"Detached voice from Node: {node.Name}");
    }

    public ALReverbEffect listenerReverbEffect;
    public ALReverbEffect outsideReverbEffect;
    public List<ALReverbEffectWithFilter> groupedReverbEffects = [];

    public ALReverbEffect GetReverbEffect(Voice voice)
    {
        if (voice.useOutsideEAX)
            return outsideReverbEffect;
        else if (voice.groupedEAXIndex >= 0)
            return groupedReverbEffects[voice.groupedEAXIndex].reverbEffect;
        else
            return listenerReverbEffect;
    }

    public ALReverbEffectWithFilter GetGroupedReverbEffect(Voice voice)
    {
        return groupedReverbEffects[voice.groupedEAXIndex];
    }

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= OnNodeAdded;
        GetTree().NodeRemoved -= OnNodeRemoved;

        // Clean up all primitives
        if (SceneRoot != null)
        {
            ForgetPrimitivesRecursive(SceneRoot);
        }

        // Dispose context
        context?.Dispose();
    }

    static void ForgetPrimitivesRecursive(Node node)
    {
        if (node is CsgBox3D csgBox && csgBox.HasMeta(PRIMITIVE_META_KEY))
        {
            csgBox.RemoveMeta(PRIMITIVE_META_KEY);
        }
        else if (node is CollisionShape3D collisionShape && collisionShape.HasMeta(PRIMITIVE_META_KEY))
        {
            collisionShape.RemoveMeta(PRIMITIVE_META_KEY);
        }
        else if (node is MeshInstance3D meshInstance && meshInstance.HasMeta(PRIMITIVE_META_KEY))
        {
            meshInstance.RemoveMeta(PRIMITIVE_META_KEY);
        }

        foreach (Node child in node.GetChildren())
        {
            ForgetPrimitivesRecursive(child);
        }
    }

    void InitializeScene()
    {
        foreach (Node child in SceneRoot.GetChildren())
        {
            CollectPrimitivesRecursive(child, vaudio.MaterialType.Air);
        }

        // Listen for scene tree changes
        GetTree().NodeAdded += OnNodeAdded;
        GetTree().NodeRemoved += OnNodeRemoved;
    }

    vaudio.MaterialType GetMaterial(Node node)
    {
        // Priority 1: Check for RaytracedAudioMaterial resource
        if (node.HasMeta(MATERIAL_RESOURCE_META_KEY))
        {
            var material = node.GetMeta(MATERIAL_RESOURCE_META_KEY).As<VercidiumAudioMaterial>();
            if (material != null)
            {
                return (vaudio.MaterialType)material.MaterialId;
            }
        }

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

    Dictionary<string, vaudio.MaterialType> DefaultMaterialDict = new()
    {
        { "air", vaudio.MaterialType.Air },
        { "brick", vaudio.MaterialType.Brick },
        { "cloth", vaudio.MaterialType.Cloth },
        { "concrete", vaudio.MaterialType.Concrete },
        { "concretepolished", vaudio.MaterialType.ConcretePolished },
        { "dirt", vaudio.MaterialType.Dirt },
        { "grass", vaudio.MaterialType.Grass },
        { "ice", vaudio.MaterialType.Ice },
        { "leaf", vaudio.MaterialType.Leaf },
        { "marble", vaudio.MaterialType.Marble },
        { "metal", vaudio.MaterialType.Metal },
        { "mud", vaudio.MaterialType.Mud },
        { "rock", vaudio.MaterialType.Rock },
        { "sand", vaudio.MaterialType.Sand },
        { "snow", vaudio.MaterialType.Snow },
        { "tree", vaudio.MaterialType.Tree },
        { "woodindoor", vaudio.MaterialType.WoodIndoor },
        { "woodoutdoor", vaudio.MaterialType.WoodOutdoor },
    };

    void CollectPrimitivesRecursive(Node node, vaudio.MaterialType material)
    {
        // Check for material override (supports both string and resource)
        if (node.HasMeta(MATERIAL_META_KEY))
            material = GetMaterial(node);

        // 'air' means don't create a primitive for this Node
        if (material == vaudio.MaterialType.Air)
            return;

        if (node is CsgBox3D csgBox)
        {
            CreateVAudioPrimitive(csgBox, material);
        }
        else if (node is CollisionShape3D collisionShape)
        {
            CreateVAudioPrimitive(collisionShape, material);
        }
        else if (node is MeshInstance3D meshInstance)
        {
            CreateVAudioPrimitive(meshInstance, material);
        }
        else
        {
            GD.PrintErr($"Cannot convert node to raytraced primitive: {node.Name}");
        }

        foreach (Node child in node.GetChildren())
        {
            CollectPrimitivesRecursive(child, material);
        }
    }

    void CreateVAudioPrimitive(CsgBox3D csgBox, vaudio.MaterialType material)
    {
        // Skip if already has primitive
        if (csgBox.HasMeta(PRIMITIVE_META_KEY))
            return;

        // 'air' means don't make a primitive for this Node
        if (material == vaudio.MaterialType.Air)
            return;

        var globalTransform = csgBox.GlobalTransform;
        var size = csgBox.Size;

        vaudio.PrismPrimitive prim = new()
        {
            size = ToVAudio(size),
            transform = ToVAudio(globalTransform),
            material = material
        };

        context.AddPrimitive(prim);

        var wrapper = new VAudioPrimitiveRef { Primitive = prim };
        csgBox.SetMeta(PRIMITIVE_META_KEY, wrapper);
    }

    void CreateVAudioPrimitive(CollisionShape3D collisionShape, vaudio.MaterialType material)
    {
        // Skip if already has primitive
        if (collisionShape.HasMeta(PRIMITIVE_META_KEY))
            return;

        if (collisionShape.HasMeta(MATERIAL_META_KEY))
            material = GetMaterial(collisionShape);

        // 'air' means don't make a primitive for this Node
        if (material == vaudio.MaterialType.Air)
            return;

        var shape = collisionShape.Shape;
        var globalTransform = collisionShape.GlobalTransform;
        var position = globalTransform.Origin;
        var scale = collisionShape.Scale;

        // Create primitive based on shape type
        if (shape is BoxShape3D box)
        {
            vaudio.PrismPrimitive prim = new()
            {
                size = ToVAudio(box.Size),
                transform = ToVAudio(globalTransform),
                material = material
            };

            context.AddPrimitive(prim);

            var wrapper = new VAudioPrimitiveRef { Primitive = prim };
            collisionShape.SetMeta(PRIMITIVE_META_KEY, wrapper);
        }
        else if (shape is SphereShape3D)
        {
            vaudio.SpherePrimitive prim = new()
            {
                center = new vaudio.Vector3F(position.X, position.Y, position.Z),
                radius = scale.X,
                material = material
            };
            context.AddPrimitive(prim);

            var wrapper = new VAudioPrimitiveRef { Primitive = prim };
            collisionShape.SetMeta(PRIMITIVE_META_KEY, wrapper);
        }
        else if (shape is CapsuleShape3D)
        {
            // The player - ignore
        }
        else if (shape is CylinderShape3D)
        {
            GD.Print("TODO - Shape is a cylinder");
        }
        else if (shape is ConcavePolygonShape3D polygon)
        {
            var triangles = ConvertConcavePolygonToVector3FList(polygon, out var min, out var max);
            var transform = ToVAudio(globalTransform);

            vaudio.MeshPrimitive prim = new(material, triangles, min, max, transform, true);
            context.AddPrimitive(prim);

            var wrapper = new VAudioPrimitiveRef { Primitive = prim };
            collisionShape.SetMeta(PRIMITIVE_META_KEY, wrapper);
        }
        else
        {
            GD.Print("shape is something different");
            GD.Print(shape);
        }
    }

    void CreateVAudioPrimitive(MeshInstance3D meshInstance, vaudio.MaterialType material)
    {
        // Skip if already has primitive
        if (meshInstance.HasMeta(PRIMITIVE_META_KEY))
            return;

        if (meshInstance.HasMeta(MATERIAL_META_KEY))
            material = GetMaterial(meshInstance);

        // 'air' means don't make a primitive for this Node
        if (material == vaudio.MaterialType.Air)
            return;

        var mesh = meshInstance.Mesh;
        if (mesh == null)
        {
            GD.PrintErr($"MeshInstance3D '{meshInstance.Name}' has no mesh assigned");
            return;
        }

        var globalTransform = meshInstance.GlobalTransform;

        // Convert mesh to triangle list
        var triangles = ConvertMeshToVector3FList(mesh, out var min, out var max);

        if (triangles.Count == 0)
        {
            GD.PrintErr($"MeshInstance3D '{meshInstance.Name}' has no triangles");
            return;
        }

        var transform = ToVAudio(globalTransform);

        vaudio.MeshPrimitive prim = new(material, triangles, min, max, transform, true);

        // TODO - make this a flag on an object
        prim.supportsPermeation = true;

        context.AddPrimitive(prim);

        var wrapper = new VAudioPrimitiveRef { Primitive = prim };
        meshInstance.SetMeta(PRIMITIVE_META_KEY, wrapper);

        GD.Print($"Created VAudio mesh primitive for '{meshInstance.Name}' with {triangles.Count / 3} triangles");
    }


    void OnNodeAdded(Node node)
    {
        var material = vaudio.MaterialType.Air;

        if (node.HasMeta(MATERIAL_META_KEY))
            material = GetMaterial(node);

        if (node is CsgBox3D csgBox)
        {
            CreateVAudioPrimitive(csgBox, material);
        }
        else if (node is CollisionShape3D collisionShape)
        {
            CreateVAudioPrimitive(collisionShape, material);
        }
        else if (node is MeshInstance3D meshInstance)
        {
            CreateVAudioPrimitive(meshInstance, material);
        }
    }

    void OnNodeRemoved(Node node)
    {
        if (node.HasMeta(PRIMITIVE_META_KEY))
        {
            var primitive = node.GetMeta(PRIMITIVE_META_KEY).As<VAudioPrimitiveRef>();
            context.RemovePrimitive(primitive.Primitive);
        }
    }

    void RefreshRoot()
    {
        var root = GetTree().Root;
        SceneRoot = root.GetChild(root.GetChildCount() - 1) as Node3D;
    }


    static void UpdatePrimitivesRecursive(Node node)
    {
        if (node.HasMeta(PRIMITIVE_META_KEY))
        {
            var wrapper = node.GetMeta(PRIMITIVE_META_KEY).As<VAudioPrimitiveRef>();
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


    #region CONVERSIONS
    public static vaudio.Matrix4F ToVAudio(Transform3D globalTransform)
    {
        Basis basis = globalTransform.Basis;
        Vector3 origin = globalTransform.Origin;

        // Both Godot's Basis and vaudio.Matrix4F are column-major
        // Godot Basis structure: basis.X, basis.Y, basis.Z are the columns

        return new vaudio.Matrix4F(
            // Column 0
            basis.X.X, basis.X.Y, basis.X.Z, 0f,
            // Column 1
            basis.Y.X, basis.Y.Y, basis.Y.Z, 0f,
            // Column 2
            basis.Z.X, basis.Z.Y, basis.Z.Z, 0f,
            // Column 3
            origin.X, origin.Y, origin.Z, 1f
        );
    }

    public static vaudio.Vector3F ToVAudio(Vector3 vec) => new(vec.X, vec.Y, vec.Z);

    public static List<vaudio.Vector3F> ConvertMeshToVector3FList(Mesh mesh, out vaudio.Vector3F min, out vaudio.Vector3F max)
    {
        List<vaudio.Vector3F> triangles = [];

        // Initialize min and max
        min = new vaudio.Vector3F(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new vaudio.Vector3F(float.MinValue, float.MinValue, float.MinValue);

        // Iterate through all surfaces in the mesh
        for (int surfaceIdx = 0; surfaceIdx < mesh.GetSurfaceCount(); surfaceIdx++)
        {
            var arrays = mesh.SurfaceGetArrays(surfaceIdx);
            if (arrays == null || arrays.Count == 0)
                continue;

            // Get vertex array (index 0 in the arrays)
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (vertices == null || vertices.Length == 0)
                continue;

            // Get normal array to check winding consistency
            Vector3[] normals = null;
            var normalsVariant = arrays[(int)Mesh.ArrayType.Normal];
            if (normalsVariant.VariantType != Variant.Type.Nil)
            {
                normals = normalsVariant.AsVector3Array();
            }

            // Get index array if it exists (index 8 in the arrays)
            var indicesVariant = arrays[(int)Mesh.ArrayType.Index];

            if (indicesVariant.VariantType == Variant.Type.Nil)
            {
                // No index array - vertices are in triangle order
                for (int i = 0; i < vertices.Length; i += 3)
                {
                    if (i + 2 >= vertices.Length)
                        break;

                    var v0 = vertices[i];
                    var v1 = vertices[i + 1];
                    var v2 = vertices[i + 2];

                    // Check winding order - calculate face normal
                    bool needsFlip = false;
                    if (normals != null && i < normals.Length)
                    {
                        var edge1 = v1 - v0;
                        var edge2 = v2 - v0;
                        var calculatedNormal = edge1.Cross(edge2).Normalized();
                        var meshNormal = normals[i];

                        // If normals point in opposite directions, flip winding
                        if (calculatedNormal.Dot(meshNormal) < 0)
                        {
                            needsFlip = true;
                        }
                        else
                        {

                        }
                    }

                    // Add vertices (flip order if needed)
                    if (needsFlip)
                    {
                        AddVertexAndUpdateBounds(v0, ref min, ref max, triangles);
                        AddVertexAndUpdateBounds(v2, ref min, ref max, triangles);
                        AddVertexAndUpdateBounds(v1, ref min, ref max, triangles);
                    }
                    else
                    {
                        AddVertexAndUpdateBounds(v0, ref min, ref max, triangles);
                        AddVertexAndUpdateBounds(v1, ref min, ref max, triangles);
                        AddVertexAndUpdateBounds(v2, ref min, ref max, triangles);
                    }
                }
            }
            else
            {
                // Has index array - use indices to build triangles
                var indices = indicesVariant.AsInt32Array();

                for (int i = 0; i < indices.Length; i += 3)
                {
                    if (i + 2 >= indices.Length)
                        break;

                    var idx0 = indices[i];
                    var idx1 = indices[i + 1];
                    var idx2 = indices[i + 2];

                    var v0 = vertices[idx0];
                    var v1 = vertices[idx1];
                    var v2 = vertices[idx2];

                    // Check winding order
                    bool needsFlip = false;
                    if (normals != null && idx0 < normals.Length)
                    {
                        var edge1 = v1 - v0;
                        var edge2 = v2 - v0;
                        var calculatedNormal = edge1.Cross(edge2).Normalized();
                        var meshNormal = normals[idx0];

                        // If normals point in opposite directions, flip winding
                        if (calculatedNormal.Dot(meshNormal) < 0)
                        {
                            needsFlip = true;
                        }
                        else
                        {

                        }
                    }

                    // Add vertices (flip order if needed)
                    if (needsFlip)
                    {
                        AddVertexAndUpdateBounds(v0, ref min, ref max, triangles);
                        AddVertexAndUpdateBounds(v2, ref min, ref max, triangles);
                        AddVertexAndUpdateBounds(v1, ref min, ref max, triangles);
                    }
                    else
                    {
                        AddVertexAndUpdateBounds(v0, ref min, ref max, triangles);
                        AddVertexAndUpdateBounds(v1, ref min, ref max, triangles);
                        AddVertexAndUpdateBounds(v2, ref min, ref max, triangles);
                    }
                }
            }
        }

        // If no vertices were found, reset to default bounds
        if (triangles.Count == 0)
        {
            min = new vaudio.Vector3F(0, 0, 0);
            max = new vaudio.Vector3F(0, 0, 0);
        }

        return triangles;
    }

    static void AddVertexAndUpdateBounds(Vector3 vertex, ref vaudio.Vector3F min, ref vaudio.Vector3F max, List<vaudio.Vector3F> triangles)
    {
        var vaudioVertex = new vaudio.Vector3F(vertex.X, vertex.Y, vertex.Z);
        triangles.Add(vaudioVertex);

        // Update bounds
        min.X = Math.Min(min.X, vaudioVertex.X);
        min.Y = Math.Min(min.Y, vaudioVertex.Y);
        min.Z = Math.Min(min.Z, vaudioVertex.Z);
        max.X = Math.Max(max.X, vaudioVertex.X);
        max.Y = Math.Max(max.Y, vaudioVertex.Y);
        max.Z = Math.Max(max.Z, vaudioVertex.Z);
    }

    public static List<vaudio.Vector3F> ConvertConcavePolygonToVector3FList(ConcavePolygonShape3D shape, out vaudio.Vector3F min, out vaudio.Vector3F max)
    {
        List<vaudio.Vector3F> triangles = [];

        // Get the faces array from the ConcavePolygonShape3D
        Vector3[] faces = shape.GetFaces();

        // Initialize min and max
        if (faces.Length > 0)
        {
            min = new vaudio.Vector3F(faces[0].X, faces[0].Y, faces[0].Z);
            max = new vaudio.Vector3F(faces[0].X, faces[0].Y, faces[0].Z);
        }
        else
        {
            min = new vaudio.Vector3F(0, 0, 0);
            max = new vaudio.Vector3F(0, 0, 0);
            return triangles;
        }

        // Each triangle consists of 3 consecutive Vector3 vertices
        for (int i = 0; i < faces.Length; i++)
        {
            vaudio.Vector3F vertex = new(
                faces[i].X,
                faces[i].Y,
                faces[i].Z
            );

            triangles.Add(vertex);

            // Update min and max
            min.X = Math.Min(min.X, vertex.X);
            min.Y = Math.Min(min.Y, vertex.Y);
            min.Z = Math.Min(min.Z, vertex.Z);

            max.X = Math.Max(max.X, vertex.X);
            max.Y = Math.Max(max.Y, vertex.Y);
            max.Z = Math.Max(max.Z, vertex.Z);
        }

        return triangles;
    }
    #endregion

    #region CUSTOM_MATERIALS
    /// <summary>
    /// Registers all RaytracedAudioMaterial child nodes with the context settings.
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

    /// <summary>
    /// Applies runtime material property updates after context creation.
    /// This allows materials to be modified at runtime if needed.
    /// </summary>
    void ApplyRuntimeMaterialUpdates()
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
    /// Helper method for developers to register materials at runtime via GDScript
    /// </summary>
    public void RegisterMaterial(VercidiumAudioMaterial material)
    {
        if (material == null)
        {
            GD.PrintErr("Cannot register null material");
            return;
        }

        if (material.MaterialId < 1000)
        {
            GD.PrintErr($"Custom material '{material.MaterialName}' has invalid ID {material.MaterialId}. Must be >= 1000.");
            return;
        }

        if (customMaterials.ContainsKey(material.MaterialId))
        {
            GD.PrintErr($"Material ID {material.MaterialId} is already registered");
            return;
        }

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

        GD.Print($"Runtime registered custom material: '{material.MaterialName}' (ID: {material.MaterialId})");
    }
    #endregion

    void UpdateGodotReverb()
    {
        // Play ambience and update reverb
        var ambientClarity = Lerp(0.0f, 1.0f, MathF.Min(1, context.ProcessedReverb.OutsidePercent / 0.4f));
        float gain = 0.3f + ambientClarity * 0.7f;
        float gainHF = MathF.Pow(gain, 1.5f);

        ambientFilter ??= new(gain, gainHF);
        ambientFilter.SetGain(gain, gainHF);


        float dt = (float)reverbStopwatch.ElapsedMilliseconds / 1000.0f;
        reverbStopwatch.Restart();
        float t = 1f - MathF.Exp(-ReverbSmoothingSpeed * dt);

        if (firstCopy)
            t = 1;

        CopyReverb(context.ListenerEAX, listenerReverbEffect, t);
        CopyReverb(context.OutsideEAX, outsideReverbEffect, t);

        for (int i = 0; i < context.GroupedEAX.Count; i++)
        {
            if (groupedReverbEffects.Count <= i)
                groupedReverbEffects.Add(new());

            CopyReverb(context.GroupedEAX[i], groupedReverbEffects[i].reverbEffect, t);

            groupedReverbEffects[i].Update();
        }

        firstCopy = false;
    }

    bool firstCopy = true;

    const float ReverbSmoothingSpeed = 8f; // Higher = faster response, lower = smoother. 8 = 125ms update
    static Stopwatch reverbStopwatch = Stopwatch.StartNew();

    void CopyReverb(vaudio.EAXReverbResults eax, ALReverbEffect effect, float t)
    {
        // Lerp all parameters toward target values
        effect.reflectionsDelay = Lerp(effect.reflectionsDelay, eax.ReflectionsDelay, t);
        effect.density = Lerp(effect.density, eax.Density, t);
        effect.diffusion = Lerp(effect.diffusion, eax.Diffusion, t);
        effect.gainLF = Lerp(effect.gainLF, eax.GainLF, t);
        effect.gainHF = Lerp(effect.gainHF, eax.GainHF, t);
        effect.gain = eax.Gain * 2;
        effect.decayTime = Lerp(effect.decayTime, eax.DecayTime, t);
        effect.decayLFRatio = Lerp(effect.decayLFRatio, eax.DecayLFRatio, t);
        effect.decayHFRatio = Lerp(effect.decayHFRatio, eax.DecayHFRatio, t);
        effect.reflectionsGain = Lerp(effect.reflectionsGain, eax.ReflectionsGain, t);
        effect.lateReverbGain = Lerp(effect.lateReverbGain, eax.LateReverbGain, t);
        effect.lateReverbDelay = Lerp(effect.lateReverbDelay, eax.LateReverbDelay, t);
        effect.echoTime = Lerp(effect.echoTime, eax.EchoTime, t);
        effect.echoDepth = Lerp(effect.echoDepth, eax.EchoDepth, t);
        effect.modulationTime = Lerp(effect.modulationTime, eax.ModulationTime, t);
        effect.modulationDepth = Lerp(effect.modulationDepth, eax.ModulationDepth, t);
        effect.airAbsorptionGainHF = Lerp(effect.airAbsorptionGainHF, eax.AirAbsorptionGainHF, t);
        effect.hfReference = Lerp(effect.hfReference, eax.HFReference, t);
        effect.lfReference = Lerp(effect.lfReference, eax.LFReference, t);
        effect.roomRolloffFactor = Lerp(effect.roomRolloffFactor, eax.RoomRolloffFactor, t);
        effect.decayHFLimit = eax.DecayHFLimit;

        if (eax.PanAL != Vector3F.Zero)
        {
            var camera = GetViewport().GetCamera3D();
            var pos = camera.GlobalPosition;

            var cameraPosition = new vaudio.Vector3F(pos.X, pos.Y, pos.Z);

            var diff = cameraPosition - eax.Center;
            var mag = diff.Magnitude;

            eax.PanAL = eax.PanAL.Normalized;
            var roomRadius = (eax.BoundsMax - eax.BoundsMin).Magnitude / 2.6f;
            var smoothDistance = 2.5f;

            if (mag < roomRadius)
            {
                var threshold = roomRadius - smoothDistance;
                var strength = Math.Max(0, mag - threshold) / smoothDistance;

                eax.PanAL *= strength;
            }
        }

        // TODO - separate pan for each
        effect.lateReverbPan[0] = Lerp(effect.lateReverbPan[0], eax.PanAL.X, t);
        effect.lateReverbPan[1] = Lerp(effect.lateReverbPan[1], eax.PanAL.Y, t);
        effect.lateReverbPan[2] = Lerp(effect.lateReverbPan[2], eax.PanAL.Z, t);

        effect.reflectionsPan[0] = Lerp(effect.reflectionsPan[0], eax.PanAL.X, t);
        effect.reflectionsPan[1] = Lerp(effect.reflectionsPan[1], eax.PanAL.Y, t);
        effect.reflectionsPan[2] = Lerp(effect.reflectionsPan[2], eax.PanAL.Z, t);

        effect.dirty = true;
        effect.Update();
    }

    static float Lerp(float current, float target, float t)
    {
        return current + (target - current) * t;
    }
}
