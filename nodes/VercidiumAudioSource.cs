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

        // Must create the voice after the parent VercidiumAudio node is initialised
        CallDeferred("CreateVoice");
    }

    public void CreateVoice()
    { 
        voice = vercidiumAudio.AttachVoice(this, OnRaytracingComplete);
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

    public override void _ExitTree()
    {
        if (voice != null)
            vercidiumAudio?.DetachVoice(this, voice);

        base._ExitTree();
    }
}
