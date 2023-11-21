extends Control


@onready var camera = get_parent().get_parent()
@onready var player = camera.get_parent()
@onready var world_gen = get_tree().root.get_node("main").get_child(0)


func _input(event):
	if event.is_action_pressed("pause"):
		$PauseMenu.visible = !$PauseMenu.visible

func _process(delta):
	var pos = player.position
	$PauseMenu/coords.text = "XYZ: (%s, %s, %s)" % [int(pos.x), int(pos.y), int(pos.z)]

func _on_h_slider_drag_ended(value_changed):
	camera.MOUSE_SENSITIVITY = $PauseMenu/SensitivitySlider.value


func _on_rd_slider_drag_ended(value_changed):
	world_gen.render_distance = int($PauseMenu/RDSlider.value)
	var chunk = Vector3i(player.position)/16
	player.emit_signal("moved_chunks", chunk.x, chunk.y, chunk.z)


func _on_fancy_slider_drag_ended(value_changed):
	var world_env:Environment = Reserved.world_env
	var value = $PauseMenu/FancySlider.value
	if value == 1:
		world_env.ssao_enabled = false
		world_env.ssil_enabled = false
		world_env.sdfgi_enabled = false
		world_env.fog_enabled = false
	elif value == 2:
		world_env.fog_enabled = true
		world_env.ssao_enabled = true
		world_env.ssil_enabled = false
		world_env.sdfgi_enabled = false
	elif value == 3:
		world_env.fog_enabled = true
		world_env.ssao_enabled = true
		world_env.ssil_enabled = true
		world_env.sdfgi_enabled = false
	elif value == 4:
		world_env.fog_enabled = true
		world_env.ssr_enabled = true
		world_env.ssao_enabled = true
		world_env.ssil_enabled = false
		world_env.sdfgi_enabled = true
		
