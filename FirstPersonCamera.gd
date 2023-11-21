extends Camera3D

@export var MOUSE_SENSITIVITY = 0.5

@export var current_mouse_sensitivity = MOUSE_SENSITIVITY

var yaw = 180
var pitch = 0
# yaw_diff and pitch_diff are altered when you want to turn the camera in code.
# Think gun recoil, wall jumping, looking at an NPC, etc.
var yaw_diff = 0
var pitch_diff = 0
var mouse_captured = false

# Called when the node enters the scene tree for the first time.
func _ready():
	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)

func _process(_delta):
	yaw_diff = lerp_angle(yaw_diff, 0, 0.005)
	pitch_diff = lerp_angle(pitch_diff, 0, 0.005)
	rotation.y = yaw + yaw_diff
	rotation.x = pitch + pitch_diff
	var input_vec = Input.get_vector("move_left", "move_right", "move_forward", "move_backward")
	var direction = (transform.basis * Vector3(input_vec.x, 0, input_vec.y)).normalized()
	position += direction * _delta * 9

func _input(event):
	#Camera movement
	if event is InputEventMouseMotion:
		yaw = fmod(yaw - event.relative.x * current_mouse_sensitivity/100, 360)
		pitch = max(min(pitch - event.relative.y * current_mouse_sensitivity/100, 1.3), -1.3)
		#PLAYER.rotation.y = yaw
		#rotation.x = pitch
	
	#Mouse lock toggle
	elif event.is_action_pressed("mouse_lock"):
		if mouse_captured:
			mouse_captured = false
			Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
		else:
			mouse_captured = true
			Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)

func turn_camera(new_yaw, new_pitch):
	yaw_diff = yaw - new_yaw
	yaw = new_yaw
	pitch_diff = pitch - new_pitch
	pitch = new_pitch

func _on_mouse_sensitivity_changed(new_val):
	MOUSE_SENSITIVITY = 0.0001 * new_val
	


