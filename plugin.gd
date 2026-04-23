@tool
extends EditorPlugin

const VercidiumAudio = preload("res://addons/godot_raytraced_audio/main/VercidiumAudio.cs")
const VercidiumAudioEmitter = preload("res://addons/godot_raytraced_audio/nodes/VercidiumAudioEmitter.cs")
const VercidiumAudioMaterial = preload("res://addons/godot_raytraced_audio/nodes/VercidiumAudioMaterial.cs")

const VercidiumAudioSource = preload("res://addons/godot_raytraced_audio/nodes/VercidiumAudioSource.cs")
const VercidiumAudioSourceRelative = preload("res://addons/godot_raytraced_audio/nodes/VercidiumAudioSourceRelative.cs")
const VercidiumAudioSourceAmbient = preload("res://addons/godot_raytraced_audio/nodes/VercidiumAudioSourceAmbient.cs")

const CSPROJ_INSERT = """	<PropertyGroup>
    	<!-- Replace this with the path to your vaudio SDK -->
		<VAudioDir>path/to/your/vaudio/folder</VAudioDir>
    </PropertyGroup>

    <!-- Add vaudio.dll to your project -->
    <ItemGroup>
    	<Reference Include="vaudio">
    		<HintPath>$(VAudioDir)\\vaudio.dll</HintPath>
    	</Reference>
    </ItemGroup>

    <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    	<!-- Copy the license file to the build directory -->
    	<Copy SourceFiles="$(VAudioDir)\\vaudio.license" DestinationFolder="$(OutDir)" />

    	<!-- Copy dependencies to the build directory -->
    	<Copy SourceFiles="$(VAudioDir)\\glfw3.dll" DestinationFolder="$(OutDir)" />

    	<!-- Copy native dependencies to the project directory -->
    	<Copy SourceFiles="$(VAudioDir)\\libSkiaSharp.dll" DestinationFolder="$(ProjectDir)" />
    	<Copy SourceFiles="$(VAudioDir)\\libHarfBuzzSharp.dll" DestinationFolder="$(ProjectDir)" />

    	<!-- Copy the resource folder to the build directory -->
    	<ItemGroup>
    		<ResourceFiles Include="$(VAudioDir)\\resource\\**\\*.*" />
    	</ItemGroup>

    	<Copy SourceFiles="@(ResourceFiles)" DestinationFolder="$(OutDir)\\resource\\%(RecursiveDir)" />

    	<!-- GODOT ONLY: copy the resource folder to project directory -->
    	<Copy SourceFiles="@(ResourceFiles)" DestinationFolder="$(ProjectDir)\\resource\\%(RecursiveDir)" />
    </Target>"""

func _enter_tree():
	var icon = preload("res://addons/godot_raytraced_audio/icons/vercidium.svg")
	var iconAL = preload("res://addons/godot_raytraced_audio/icons/vercidium_al.svg")

	add_custom_type("VercidiumAudio", "Node", VercidiumAudio, icon)
	add_custom_type("VercidiumAudioEmitter", "Node3D", VercidiumAudioEmitter, icon)
	add_custom_type("VercidiumAudioMaterial", "Node3D", VercidiumAudioMaterial, icon)

	add_custom_type("VercidiumAudioSource", "Node3D", VercidiumAudioSource, iconAL)
	add_custom_type("VercidiumAudioSourceRelative", "Node", VercidiumAudioSourceRelative, iconAL)
	add_custom_type("VercidiumAudioSourceAmbient", "Node", VercidiumAudioSourceAmbient, iconAL)

	_setup_project()

	if not ProjectSettings.settings_changed.is_connected(_on_settings_changed):
		ProjectSettings.settings_changed.connect(_on_settings_changed)

	print("[godot_raytraced_audio] Vercidium Audio (vaudio) plugin enabled")
	print("[godot_raytraced_audio] Note: the *Source* nodes require the 'godot_openal' plugin")

func _exit_tree():
	remove_custom_type("VercidiumAudio")
	remove_custom_type("VercidiumAudioEmitter")
	remove_custom_type("VercidiumAudioMaterial")

	remove_custom_type("VercidiumAudioSource")
	remove_custom_type("VercidiumAudioSourceRelative")
	remove_custom_type("VercidiumAudioSourceAmbient")

	if ProjectSettings.settings_changed.is_connected(_on_settings_changed):
		ProjectSettings.settings_changed.disconnect(_on_settings_changed)

	print("Vercidium Audio (vaudio) plugin disabled")

var _setup_done := false

func _on_settings_changed():
	if not _setup_done:
		_setup_project()

func _setup_project():
	var project_name = ProjectSettings.get_setting("application/config/name")
	var csproj_path = "res://%s.csproj" % project_name

	if not FileAccess.file_exists(csproj_path):
		push_error("[godot_raytraced_audio] No C# solution found. Please create a C# solution (Project → Tools → C# → Create C# Solution) and then re-enable this plugin")
		return

	var file = FileAccess.open(csproj_path, FileAccess.READ)
	if not file:
		return

	var content = file.get_as_text()
	file.close()

	_setup_done = true

	if "VAudioDir" in content:
		if "path/to/your/vaudio/folder" in content:
			push_error("[godot_raytraced_audio] csproj is invalid (%s) - please replace 'path/to/your/vaudio/folder' with your actual vaudio path, then disable and enable the VercidiumAudio plugin" % ProjectSettings.globalize_path(csproj_path))
		else:
			print("[godot_raytraced_audio] csproj configured correctly")
		return

	var insert_pos = content.rfind("</Project>")
	if insert_pos == -1:
		push_error("[godot_raytraced_audio] Could not find a </Project> tag in the .csproj file")
		return

	var new_content = content.substr(0, insert_pos) + "\n" + CSPROJ_INSERT + "\n" + content.substr(insert_pos)

	file = FileAccess.open(csproj_path, FileAccess.WRITE)
	if file:
		file.store_string(new_content)
		file.close()
		push_warning("[godot_raytraced_audio] Added vaudio references to ", ProjectSettings.globalize_path(csproj_path), " - please replace 'path/to/your/vaudio/folder' with your actual vaudio path, then disable and enable the VercidiumAudio plugin")
