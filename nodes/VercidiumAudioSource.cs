using godot_openal;
using Silk.NET.SDL;

namespace godot_raytraced_audio;

[Tool]
[GlobalClass]
public partial class VercidiumAudioSource : ALSource3D
{
    private VercidiumAudio vercidiumAudio;
    VercidiumAudioEmitter emitter;

    public bool Raytraced => emitter != null && emitter.Raytraced;

    private bool _PlayWhenRaytracingCompletes = true;
    private bool _RaytraceOnce = true;
    private bool _wasPlayingBeforeDeviceDestroyed = false;

    [Export]
    public bool PlayWhenRaytracingCompletes
    {
        get => _PlayWhenRaytracingCompletes;
        set => _PlayWhenRaytracingCompletes = value;
    }

    [Export]
    public bool RaytraceOnce
    {
        get => _RaytraceOnce;
        set => _RaytraceOnce = value;
    }

    [ExportGroup("Raytracing Quality")]

    int _ReverbRayCount = 32;
    [Export(PropertyHint.Range, "16,1024,16")]
    public int ReverbRayCount
    {
        get => _ReverbRayCount;
        set
        {
            _ReverbRayCount = value;

            if (emitter != null)
                emitter.ReverbRayCount = value;
        }
    }

    int _ReverbBounceCount = 64;
    [Export(PropertyHint.Range, "1,128,1")]
    public int ReverbBounceCount
    {
        get => _ReverbBounceCount;
        set
        {
            _ReverbBounceCount = value;

            if (emitter != null)
                emitter.ReverbBounceCount = value;
        }
    }

    int _VisualisationRayCount = 0;
    [Export(PropertyHint.Range, "0,128,4")]
    public int VisualisationRayCount
    {
        get => _VisualisationRayCount;
        set
        {
            _VisualisationRayCount = value;

            if (emitter != null)
                emitter.VisualisationRayCount = value;
        }
    }

    int _VisualisationBounceCount = 0;
    [Export(PropertyHint.Range, "0,32,1")]
    public int VisualisationBounceCount
    {
        get => _VisualisationBounceCount;
        set
        {
            _VisualisationBounceCount = value;

            if (emitter != null)
                emitter.VisualisationBounceCount = value;
        }
    }

    int _VisualisationUpdateFrequency = 500;
    [Export(PropertyHint.Range, "50,1000,50")]
    public int VisualisationUpdateFrequency
    {
        get => _VisualisationUpdateFrequency;
        set
        {
            _VisualisationUpdateFrequency = value;

            if (emitter != null)
                emitter.VisualisationUpdateFrequency = value;
        }
    }

    public override void _EnterTree()
    {
        vercidiumAudio = this.GetVercidiumAudioParent();

        // Register for device recreated callback to re-play sounds
        ALManager.instance.RegisterDeviceRecreatedCallback(OnDeviceRecreated);

        // Must create the emitter after the parent VercidiumAudio node is initialised
        CreateEmitter();
    }

    public void CreateEmitter()
    {
        emitter = new VercidiumAudioEmitter()
        {
            Name = $"{Name}-Emitter",
            OnRaytracedByAnotherEmitterCallback = OnRaytracedByAnotherEmitter,

            // Disable all but reverb
            OcclusionRayCount = 0,
            PermeationRayCount = 0,
            AmbientPermeationRayCount = 0,

            // Less rays for individual sources
            ReverbRayCount = ReverbRayCount,
            ReverbBounceCount = ReverbBounceCount,

            VisualisationRayCount = VisualisationRayCount,
            VisualisationBounceCount = VisualisationBounceCount,
            VisualisationUpdateFrequency = VisualisationUpdateFrequency,            
        };

        AddChild(emitter);
    }

    void OnDeviceRecreated()
    {
        // Re-play if we were playing before the device was destroyed
        if (_wasPlayingBeforeDeviceDestroyed)
        {
            _wasPlayingBeforeDeviceDestroyed = false;
            Play();
        }
    }

    void OnRaytracedByAnotherEmitter(vaudio.Emitter other)
    {
        ApplyRaytracingResults(other);

        if (PlayWhenRaytracingCompletes)
            Play();

        // Remove our emitter after we've been raytraced (this is a short sound that doesn't need continuous raytracing)
        if (RaytraceOnce)
        {
            Debug.Assert(emitter != null);

            RemoveChild(emitter);
            emitter = null;
        }
    }

    bool played = false;

    public override bool Play()
    {
        if (!Raytraced)
        {
            PlayWhenRaytracingCompletes = true;
            return false;
        }

        return played = base.Play();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Raytraced)
        {
            if (!played && PlayWhenRaytracingCompletes)
                Play();

            ApplyRaytracingResults(vercidiumAudio.listener.emitter);
        }
    }

    void ApplyRaytracingResults(vaudio.Emitter other)
    {
        effect = vercidiumAudio.GetReverbEffect(emitter);

        if (other.HasRaytracedTarget(emitter.emitter))
        {
            var vaudioFilter = other.GetTargetFilter(emitter.emitter);
            UpdateFilter(vaudioFilter.gainLF, vaudioFilter.gainHF, true);
        }
    }

    public override void OnDeviceDestroyed()
    {
        // Track if we were playing so we can re-play after device recreation
        _wasPlayingBeforeDeviceDestroyed = played && Looping;

        // Reset played state since sources are being destroyed
        played = false;

        base.OnDeviceDestroyed();
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
        {
            base._ExitTree();
            return;
        }

        // Unregister the device recreated callback
        ALManager.instance.UnregisterDeviceRecreatedCallback(OnDeviceRecreated);

        base._ExitTree();
    }
}
