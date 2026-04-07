namespace godot_raytraced_audio;

[Tool]
public partial class VercidiumAudio : Node
{
    public vaudio.Emitter AttachEmitter(VercidiumAudioEmitter node, Action OnRaytracingComplete)
    {
        var emitter = new vaudio.Emitter
        {
            Position = new vaudio.FuncPosition(() => ToVAudio(node.GlobalPosition)),
            OnRaytracingComplete = OnRaytracingComplete,
            ReverbRayCount = node.ReverbRayCount,
            ReverbBounceCount = node.ReverbBounceCount,
            OcclusionRayCount = node.OcclusionRayCount,
            OcclusionBounceCount = node.OcclusionBounceCount,
            PermeationRayCount = node.PermeationRayCount,
            PermeationBounceCount = node.PermeationBounceCount,
            AmbientPermeationRayCount = node.AmbientPermeationRayCount,
            AmbientPermeationBounceCount = node.AmbientPermeationBounceCount,
            VisualisationRayCount = node.VisualisationRayCount,
            VisualisationBounceCount = node.VisualisationBounceCount,
        };

        context.AddEmitter(emitter);

        if (node.IsMainListener)
        {
            if (listener == null)
            {
                listener = node;
            }
            else
            {
                GD.PushWarning($"The {listener.Name} node has already been set as the IsMainListener node, but the {node.Name} node also has IsMainListener set to true. Only one node can be the main listener");
            }
        }
        else
        {
            if (listener == null)
            {
                GD.PushWarning($"Emitters cannot be added before the main listener emitteris created. Ensure a VercidiumAudioEmitter node exists as a child node of VercidiumAudio, with IsMainListener set to true.");
            }
            else
            {
                listener.AddTarget(emitter);
            }
        }

        return emitter;
    }

    public void DetachVoice(Node3D node, vaudio.Emitter voice)
    {
        Debug.Assert(voice != null);

        listener.RemoveTarget(voice);
        context.RemoveEmitter(voice);
    }

    // Helpers
    static bool IsNaNorInfinity(float v) => float.IsNaN(v) || float.IsInfinity(v);
    static bool IsNaNorInfinity(vaudio.Vector3F v) => IsNaNorInfinity(v.X) || IsNaNorInfinity(v.Y) || IsNaNorInfinity(v.Z);

    static float Lerp(float current, float target, float lerp) => current + (target - current) * lerp;
}
