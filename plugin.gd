@tool
extends EditorPlugin

const VercidiumAudio = preload("res://addons/godot_raytraced_audio/main/VercidiumAudio.cs")
const VercidiumAudioVoice = preload("res://addons/godot_raytraced_audio/nodes/VercidiumAudioVoice.cs")
const VercidiumAudioMaterial = preload("res://addons/godot_raytraced_audio/nodes/VercidiumAudioMaterial.cs")

const VercidiumAudioSource = preload("res://addons/godot_raytraced_audio/nodes/VercidiumAudioSource.cs")
const VercidiumAudioSourceRelative = preload("res://addons/godot_raytraced_audio/nodes/VercidiumAudioSourceRelative.cs")
const VercidiumAudioSourceAmbient = preload("res://addons/godot_raytraced_audio/nodes/VercidiumAudioSourceAmbient.cs")

func _enter_tree():
	var icon = preload("res://addons/godot_raytraced_audio/icons/vercidium.svg")
	var iconAL = preload("res://addons/godot_raytraced_audio/icons/vercidium_al.svg")

	add_custom_type("VercidiumAudio", "Node", VercidiumAudio, icon)
	add_custom_type("VercidiumAudioVoice", "Node3D", VercidiumAudioVoice, icon)
	add_custom_type("VercidiumAudioMaterial", "Node3D", VercidiumAudioMaterial, icon)

	add_custom_type("VercidiumAudioSource", "Node3D", VercidiumAudioSource, iconAL)
	add_custom_type("VercidiumAudioSourceRelative", "Node", VercidiumAudioSourceRelative, iconAL)
	add_custom_type("VercidiumAudioSourceAmbient", "Node", VercidiumAudioSourceAmbient, iconAL)
	
	print("Vercidium Audio (vaudio) plugin enabled")
	print("Note: The *Source* nodes require the 'godot_openal' plugin")

func _exit_tree():
	remove_custom_type("VercidiumAudio")
	remove_custom_type("VercidiumAudioVoice")
	remove_custom_type("VercidiumAudioMaterial")

	remove_custom_type("VercidiumAudioSource")
	remove_custom_type("VercidiumAudioSourceRelative")
	remove_custom_type("VercidiumAudioSourceAmbient")
	
	print("Vercidium Audio (vaudio) plugin disabled")
