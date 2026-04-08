namespace godot_raytraced_audio;

[Tool]
public partial class VercidiumAudio : Node
{
    public vaudio.Emitter CreateEmitter(VercidiumAudioEmitter node, Action OnRaytracingComplete)
    {
        var emitter = new vaudio.Emitter
        {
            Name = node.Name,
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
            AffectsGroupedEAX = node.AffectsGroupedEAX,
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
                LogWarning($"The {listener.Name} node has already been set as the IsMainListener node, but the {node.Name} node also has IsMainListener set to true. Only one node can be the main listener");
            }
        }
        else
        {
            if (listener == null)
            {
                LogWarning($"Emitters cannot be added before the main listener emitter is created. Ensure a VercidiumAudioEmitter node exists as a child node of VercidiumAudio, with IsMainListener set to true");
            }
            else
            {
                listener.AddTarget(emitter);
            }
        }

        return emitter;
    }

    public void RemoveEmitter(vaudio.Emitter voice)
    {
        Debug.Assert(voice != null);

        listener.RemoveTarget(voice);
        context.RemoveEmitter(voice);
    }

    // Log to both - in case we're launched from vs2026 or from the Godot Editor
    public static Action<string> Log = (message) =>
    {
        var prefixed = $"[godot_raytraced_audio] {message}";

        Console.WriteLine(prefixed);
        GD.Print(prefixed);
    };

    public static Action<string> LogWarning = (message) =>
    {
        var prefixed = $"[godot_raytraced_audio] {message}";

        Console.WriteLine(prefixed);
        GD.PushWarning(prefixed);
    };

    public static Action<string> LogError = (message) =>
    {
        var prefixed = $"[godot_raytraced_audio] {message}";

        Console.Error.WriteLine(prefixed);
        GD.PushError(prefixed);
    };

}
