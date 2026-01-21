using godot_openal;

namespace godot_raytraced_audio;

[Tool]
[GlobalClass]
public partial class VercidiumAudioSource : ALSource3D
{
    private VercidiumAudio vercidiumAudio;
    vaudio.Voice voice;

    public bool Raytraced => voice != null && !voice.initialising;

    private bool _playWhenRaytracingCompletes = false;
    private bool _wasPlayingBeforeDeviceDestroyed = false;

    [Export]
    public bool PlayWhenRaytracingCompletes
    {
        get => _playWhenRaytracingCompletes;
        set => _playWhenRaytracingCompletes = value;
    }

    public override void _EnterTree()
    {
        vercidiumAudio = this.GetVercidiumAudioParent();
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            return;

        // Register for device recreated callback to re-play sounds
        ALManager.instance.RegisterDeviceRecreatedCallback(OnDeviceRecreated);

        // Must create the voice after the parent VercidiumAudio node is initialised
        CallDeferred("CreateVoice");
    }

    public void CreateVoice()
    {
        voice = vercidiumAudio.AttachVoice(this, OnRaytracingComplete);
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

    void OnRaytracingComplete()
    {
        ApplyRaytracingResults();

        if (PlayWhenRaytracingCompletes)
            Play();
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

            ApplyRaytracingResults();
        }
    }

    void ApplyRaytracingResults()
    {
        effect = vercidiumAudio.GetReverbEffect(voice);

        // Update the audio filter
        UpdateFilter(voice.filter.gainLF, voice.filter.gainHF);
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

        if (voice != null)
            vercidiumAudio?.DetachVoice(this, voice);

        base._ExitTree();
    }
}
