using vaudio;

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
            context?.UpdateWorldBounds(ToVAudio(value), ToVAudio(_worldSize));
        }
    }

    Vector3 _worldSize = new(200, 100, 200);
    [Export] public Vector3 WorldSize
    { 
        get => _worldSize;
        set
        {
            _worldSize = value;
            context?.UpdateWorldBounds(ToVAudio(_worldPosition), ToVAudio(value));
        }
    }

    [ExportGroup("Voice Settings")]

    int _maxVoices = 8;
    [Export(PropertyHint.Range, "1,32,1")] public int MaxVoices
    { 
        get => _maxVoices;
        set
        {
            _maxVoices = value;
            context?.UpdateMaxVoices(value);
        }
    }

    int _maximumGroupedEAXCount = 3;
    [Export(PropertyHint.Range, "1,8,1")] public int MaximumGroupedEAXCount
    { 
        get => _maximumGroupedEAXCount;
        set
        {
            _maximumGroupedEAXCount = value;
            context?.UpdateMaximumGroupedEAXAmount(value);
        }
    }

    [ExportGroup("Raytracing Quality")]

    int _reverbRayCount = 256;
    [Export(PropertyHint.Range, "32,1024,32")] public int ReverbRayCount
    { 
        get => _reverbRayCount;
        set
        {
            _reverbRayCount = value;
            context?.UpdateReverbRayCount(value);
        }
    }

    int _occlusionRayCount = 512;
    [Export(PropertyHint.Range, "32,1024,32")] public int OcclusionRayCount
    { 
        get => _occlusionRayCount;
        set
        {
            _occlusionRayCount = value;
            context?.UpdateOcclusionRayCount(value);
        }
    }

    int _permeationRayCount = 128;
    [Export(PropertyHint.Range, "32,1024,32")] public int PermeationRayCount
    { 
        get => _permeationRayCount;
        set
        {
            _permeationRayCount = value;
            context?.UpdatePermeationRayCount(value);
        }
    }

    int _trailBounceCount = 8;
    [Export(PropertyHint.Range, "1,16,1")] public int TrailBounceCount
    { 
        get => _trailBounceCount;
        set
        {
            _trailBounceCount = value;
            context?.UpdateTrailBounceCount(value);
        }
    }

    int _voiceReverbRayCount = 32;
    [Export(PropertyHint.Range, "8,128,8")] public int VoiceReverbRayCount
    { 
        get => _voiceReverbRayCount;
        set
        {
            _voiceReverbRayCount = value;
            context?.UpdateVoiceReverbRayCount(value);
        }
    }

    int _voiceReverbBounceCount = 8;
    [Export(PropertyHint.Range, "1,16,1")] public int VoiceReverbBounceCount
    { 
        get => _voiceReverbBounceCount;
        set
        {
            _voiceReverbBounceCount = value;
            context?.UpdateVoiceReverbBounceCount(value);
        }
    }

    [ExportGroup("Rendering")]
    [Export] public bool RenderingEnabled = true;

}
