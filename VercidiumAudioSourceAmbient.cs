using Godot;
using vaudio;

namespace OpenALAudio;

[Tool]
[GlobalClass]
public partial class VercidiumAudioSourceAmbient : ALSource3D
{
    private VercidiumAudio vercidiumAudio;
    private bool _played = false;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
            return;

        vercidiumAudio = this.GetVercidiumAudioParent();
    }

    public override void _Ready()
    {
    }

    public override bool Play()
    {
        // Don't play until we've raytraced once
        if (vercidiumAudio?.ambientFilter == null)
            return false;

        _played = base.Play();
        return _played;
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        // Ensure VercidiumAudio is available
        if (vercidiumAudio?.ambientFilter == null)
            return;

        UpdateFilter(vercidiumAudio.ambientFilter.gain, vercidiumAudio.ambientFilter.gainHF);

        if (!_played)
        {
            _played = Play();
        }
    }
}
