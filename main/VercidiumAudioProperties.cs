namespace godot_raytraced_audio;

public partial class VercidiumAudio : Node
{
    public Node3D SceneRoot;

    [ExportGroup("World Bounds")]

    private Vector3 _worldPosition = new(-100, 0, -100);
    [Export] public Vector3 WorldPosition
    { 
        get => _worldPosition;
        set
        {
            _worldPosition = value;

            if (context != null)
                context.WorldPosition = ToVAudio(value);
        }
    }

    Vector3 _worldSize = new(200, 100, 200);
    [Export] public Vector3 WorldSize
    { 
        get => _worldSize;
        set
        {
            _worldSize = value;

            if (context != null)
                context.WorldSize = ToVAudio(value);
        }
    }

    [ExportGroup("Voice Settings")]

    int _maximumGroupedEAXCount = 3;
    [Export(PropertyHint.Range, "1,16,1")] public int MaximumGroupedEAXCount
    { 
        get => _maximumGroupedEAXCount;
        set
        {
            _maximumGroupedEAXCount = value;

            if (context != null)
                context.MaximumGroupedEAXCount = value;
        }
    }


    [ExportGroup("Rendering")]
    [Export] public bool RenderingEnabled = true;

}
