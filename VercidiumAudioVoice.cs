using Godot;

namespace OpenALAudio;

[GlobalClass]
public partial class VercidiumAudioVoice : Node3D
{
    VercidiumAudio vercidiumAudio;
    vaudio.Voice voice;

    public ALReverbEffect effect;
    public ALFilter filter;

    public float GainLF => filter?.gain ?? 0;
    public float GainHF => filter?.gainHF ?? 0;
    public bool Raytraced => voice != null && !voice.initialising;

    public override void _EnterTree()
    {
        vercidiumAudio = this.GetVercidiumAudioParent();
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            return;

        voice = vercidiumAudio.AttachVoice(this, OnRaytracingComplete);
    }

    void OnRaytracingComplete()
    {
        Debug.Assert(filter == null);

        filter = new(1, 1);
        ApplyRaytracingResults();
    }

    public override void _Process(double delta)
    {
        if (Raytraced)
            ApplyRaytracingResults();
    }

    void ApplyRaytracingResults()
    {
        effect = vercidiumAudio.GetReverbEffect(voice);
        filter.SetGain(voice.filter.gainLF, voice.filter.gainHF);
    }

    public override void _ExitTree()
    {
        // Detach voice from VAudio before cleanup
        if (voice != null)
        {
            vercidiumAudio?.DetachVoice(this, voice);
            voice = null;
        }

        base._ExitTree();
    }
}
