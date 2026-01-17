using Godot;

namespace OpenALAudio;

public static class NodeExtensions
{
    public static VercidiumAudio GetVercidiumAudioParent(this Node node)
    {
        var sceneRoot = node.GetTree().CurrentScene;
        if (sceneRoot == null)
            return null;

        return sceneRoot.GetNodeOrNull<VercidiumAudio>("VercidiumAudio");
    }
}