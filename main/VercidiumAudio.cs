namespace godot_raytraced_audio;

[Tool]
public partial class VercidiumAudio : Node
{
    public vaudio.Voice AttachVoice(Node3D node, Action OnRaytracingComplete)
    {
        var voice = context.CreateVoice(new vaudio.FuncPositionF(() =>ToVAudio(node.GlobalPosition)));
        voice.OnRaytracingComplete = OnRaytracingComplete;

        return voice;
    }

    public void DetachVoice(Node3D node, vaudio.Voice voice)
    {
        Debug.Assert(voice != null);

        context.RemoveVoice(voice);
    }

    // Helpers
    static bool IsNaNorInfinity(float v) => float.IsNaN(v) || float.IsInfinity(v);
    static bool IsNaNorInfinity(vaudio.Vector3F v) => IsNaNorInfinity(v.X) || IsNaNorInfinity(v.Y) || IsNaNorInfinity(v.Z);

    static float Lerp(float current, float target, float lerp) => current + (target - current) * lerp;
}
