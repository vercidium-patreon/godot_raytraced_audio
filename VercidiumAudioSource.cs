using Godot;

namespace OpenALAudio;

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
            if (!played)
                Play();

            ApplyRaytracingResults();
        }
    }

    void ApplyRaytracingResults()
    {
        effect = vercidiumAudio.GetReverbEffect(voice);

        // Update the equalizer applied to this effect
        if (voice.groupedEAXIndex >= 0)
        {
            var effectWithFilter = vercidiumAudio.GetGroupedReverbEffect(voice);
            effectWithFilter.SetFilterGain(voice.filter.gainLF, voice.filter.gainHF);

           // UpdateFilterNew((int)effectWithFilter.filterEffectSlotID, voice.filter.gainLF, voice.filter.gainHF);
            //return;
        }


        // Update the audio filter
        UpdateFilter(voice.filter.gainLF, voice.filter.gainHF);
    }

    public void UpdateFilterNew(int effectID, float gain, float gainHF)
    {
        if (filter == null)
            filter = new(gain, gainHF);
        else
            filter.SetGain(gain, gainHF);

        // Send full signal into the reverb effect, then use an AL_EFFECT_EQUALIZER to reduce it
        var silenceFilter = new ALFilter(0, 0);
        var fullFilter = new ALFilter(1, 1);

        foreach (var s in sources)
            s.SetFilter(effectID, silenceFilter, fullFilter);

        silenceFilter.Delete();
        fullFilter.Delete();
    }


    public override void _ExitTree()
    {
        if (voice != null)
            vercidiumAudio?.DetachVoice(this, voice);

        base._ExitTree();
    }
}
