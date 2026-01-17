# Example VAudio Setup

This document shows how to set up VAudio in your Godot project.

### 1. Enable Plugins
In Project > Project Settings > Plugins:
1. Enable "OpenAL Audio"
2. Enable "Vercidium Audio (VAudio)"

### 2. Add Core Nodes
```gdscript
# Add to your main scene:
var vaudio = VercidiumAudio.new()
vaudio.name = "VercidiumAudio"
add_child(vaudio)

var al_manager = ALManager.new()
al_manager.name = "ALManager"
add_child(al_manager)
```

### 3. Create Audio Source
```gdscript
# Use RaytracedAudioSource instead of ALSource3D
var audio_source = RaytracedAudioSource.new()
audio_source.name = "MySound"
audio_source.position = Vector3(0, 1, 0)
add_child(audio_source)

# Load and play sound
audio_source.Stream = preload("res://sounds/mysound.ogg")
audio_source.Play()
```

### 4. Configure Materials (Optional)

Select a node in the scene tree, then in the Inspector:
1. Scroll to "Metadata"
2. Add new metadata entry:
   - Name: `vaudio_material`
   - Type: String
   - Value: One of these:
     - `concrete` - Hard surfaces, lots of reflection
     - `metal` - Metallic surfaces
     - `woodindoor` - Indoor wood surfaces
     - `grass` - Outdoor grass
     - `snow` - Snow-covered surfaces
     - `air` - Exclude from raytracing (useful for triggers)

Example in code:
```gdscript
# Make a wall out of concrete
wall.set_meta("vaudio_material", "concrete")

# Exclude a trigger volume
trigger_area.set_meta("vaudio_material", "air")
```

## Common Patterns

### Door with Material
```gdscript
var door = StaticBody3D.new()
door.set_meta("vaudio_material", "woodindoor")

var collision = CollisionShape3D.new()
var shape = BoxShape3D.new()
shape.size = Vector3(2, 3, 0.1)
collision.shape = shape
door.add_child(collision)
```

### Room with Different Materials
```gdscript
# Room root uses default material
var room = Node3D.new()

# Floor is concrete
var floor = StaticBody3D.new()
floor.set_meta("vaudio_material", "concrete")
room.add_child(floor)

# Walls are also concrete (inherit from parent if not set)
var wall = StaticBody3D.new()
room.add_child(wall)
```

## GDScript Example - Complete Scene

```gdscript
extends Node3D

func _ready():
    # Setup VAudio
    var vaudio = VercidiumAudio.new()
    vaudio.name = "VercidiumAudio"
    add_child(vaudio)
    
    var al_manager = ALManager.new()
    al_manager.name = "ALManager"
    add_child(al_manager)
    
    # Create a room
    create_room()
    
    # Create audio source
    create_audio_source()

func create_room():
    # Floor
    var floor = StaticBody3D.new()
    floor.set_meta("vaudio_material", "concrete")
    var floor_collision = CollisionShape3D.new()
    var floor_shape = BoxShape3D.new()
    floor_shape.size = Vector3(20, 0.5, 20)
    floor_collision.shape = floor_shape
    floor.add_child(floor_collision)
    add_child(floor)
    
    # Walls could be added similarly...

func create_audio_source():
    var audio = vaudio.RaytracedAudioSource.new()
    audio.name = "BackgroundMusic"
    audio.position = Vector3(0, 2, 0)
    audio.Stream = preload("res://audio/music.ogg")
    audio.Looping = true
    add_child(audio)
```

## Performance Tips

1. **Use simpler collision shapes where possible**
   - BoxShape3D is faster than ConcavePolygonShape3D
   - Combine small objects into larger primitives

2. **Adjust ray counts in VercidiumAudio.cs**
   ```csharp
   reverbRayCount = 128,     // Lower for better performance
   occlusionRayCount = 128,
   ```

3. **Use 'air' material to exclude decorative objects**
   - Particles, effects, UI elements don't need raytracing

## Troubleshooting

**Audio has no occlusion/reverb:**
- Ensure OpenAL Audio plugin is enabled first
- Check that VercidiumAudio node exists in scene
- Verify you're using RaytracedAudioSource, not ALSource3D

**Console shows "VercidiumAudio not found":**
- Make sure VercidiumAudio node is named "VercidiumAudio"
- Or modify RaytracedAudioSource to search by type instead of name

**Performance issues:**
- Reduce ray counts in VercidiumAudio._Ready()
- Use simpler collision shapes
- Mark decorative objects as 'air' material
