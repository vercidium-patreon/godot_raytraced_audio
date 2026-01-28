using System.Linq;
using godot_openal;

namespace godot_raytraced_audio;

public partial class VercidiumAudio : Node
{
    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        // Cache the scene root since we access it often
        SceneRoot = GetTree().CurrentScene as Node3D;
    }

    // Log to both - in case we're launched from vs2026 or from the Godot Editor
    static Action<string> Log = (message) =>
    {
        var prefixed = $"[godot_raytraced_audio] {message}";

        Console.WriteLine(prefixed);
        GD.Print(prefixed);
    };

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            return;

        // Log to both - in case we're launched from vs2026 or from the Godot Editor
        Action<string> logCallback = (s) =>
        {
            Console.WriteLine(s);
            GD.Print(s);
        };

        var settings = new vaudio.RaytracingContextSettings()
        {
            worldPosition = new(WorldPosition.X, WorldPosition.Y, WorldPosition.Z),
            worldSize = new(WorldSize.X, WorldSize.Y, WorldSize.Z),
            renderingEnabled = RenderingEnabled,
            maxVoices = MaxVoices,
            reverbRayCount = ReverbRayCount,
            occlusionRayCount = OcclusionRayCount,
            permeationRayCount = PermeationRayCount,
            trailBounceCount = TrailBounceCount,
            maximumGroupedEAXCount = MaximumGroupedEAXCount,
            voiceReverbRayCount = VoiceReverbRayCount,
            voiceReverbBounceCount = VoiceReverbBounceCount,
            logCallback = Log,
            onReverbUpdated = UpdateGodotReverb
        };

        // Register custom materials from child RaytracedAudioMaterial resources
        RegisterCustomMaterials(settings);

        context = new(settings);

        // Create reverb effects
        OnDeviceRecreated();

        // Register for device destroyed/recreated callbacks to clean up and recreate reverb effects
        ALManager.instance.RegisterDeviceDestroyedCallback(OnDeviceDestroyed);
        ALManager.instance.RegisterDeviceRecreatedCallback(OnDeviceRecreated);

        // Wait a frame for the scene to be fully loaded
        CallDeferred(nameof(InitializeScene));

        Log("Ready");
    }

    void OnDeviceDestroyed()
    {
        // Delete all reverb effects - they contain OpenAL resources that are now invalid
        ambientFilter?.Delete();
        ambientFilter = null;

        listenerReverbEffect?.Dispose();
        listenerReverbEffect = null;

        foreach (var effect in groupedReverbEffects)
            effect.Dispose();

        groupedReverbEffects.Clear();
    }

    void OnDeviceRecreated()
    {
        // Recreate the reverb effects after the device is recreated
        listenerReverbEffect = new();

        // Don't create ambientFilter here, as we need raytracing to complete first
    }

    void InitializeScene()
    {
        foreach (Node child in SceneRoot.GetChildren())
            AddPrimitive(child, vaudio.MaterialType.Air, true);

        // Listen for scene tree changes
        GetTree().NodeAdded += OnNodeAdded;
        GetTree().NodeRemoved += OnNodeRemoved;
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
            return;

        // Unregister the device destroyed/recreated callbacks
        ALManager.instance.UnregisterDeviceDestroyedCallback(OnDeviceDestroyed);
        ALManager.instance.UnregisterDeviceRecreatedCallback(OnDeviceRecreated);

        GetTree().NodeAdded -= OnNodeAdded;
        GetTree().NodeRemoved -= OnNodeRemoved;

        // Remove vercidium_audio_* metadata fields from all nodes in the scene
        RemovePrimitive(SceneRoot, true);

        context?.Dispose();
    }

    // This fires for the new parent node AND each of its child nodes separately
    //  Parent node is invoked first
    void OnNodeAdded(Node node)
    {
        AddPrimitive(node, vaudio.MaterialType.Air, false); 
    }

    // This fires for the new parent node AND each of its child nodes separately
    //  Child nodes are invoked first
    void OnNodeRemoved(Node node)
    {
        RemovePrimitive(node, false);
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        // TODO - allow custom listener positions (e.g. no Camera3D)
        var camera = GetViewport().GetCamera3D();
        var pos = camera.GlobalPosition;

        var cameraPosition = new vaudio.Vector3F(pos.X, pos.Y, pos.Z);
        var cameraPitch = camera.GlobalRotation.X;
        var cameraYaw = camera.GlobalRotation.Y;
        var fieldOfView = 90 / 180.0f * MathF.PI;

        // Sync the listener + debug window to the Godot camera
        context.UpdateListener(cameraPosition, cameraPitch, cameraYaw);
        context.SetRenderView(cameraPosition, cameraPitch, cameraYaw, fieldOfView);

        ApplyMaterialUpdates();

        context.Update();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        UpdatePrimitivesRecursive(SceneRoot);
    }
}
