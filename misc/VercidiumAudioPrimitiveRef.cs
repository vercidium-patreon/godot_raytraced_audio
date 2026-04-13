namespace godot_raytraced_audio;

partial class VercidiumAudioPrimitiveRef : RefCounted
{
    public vaudio.Primitive Primitive { get; set; }
    public TransformWatcher Watcher { get; set; }
    public Callable? ShapeCallable { get; set; }
}
