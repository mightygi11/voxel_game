extends CharacterBody3D

const SPEED = 6
const JUMP_VELOCITY = 5.0
const MAX_AIR_SPEED = 6 # You can't accelerate past this speed BY YOURSELF in air

var gravity = 9.8

var state = 0
var hand_state = 0
var buffered_jump = false
var auto_jump = false
var spawn_point = Vector3(0, 2, 0)
var current_chunk = Vector3i(0, 0, 0)

@onready var camera_arm = $SpringArm3D
@onready var water_overlay = $SpringArm3D/Camera3D/Control/WaterOverlay


#state: 0 = default, 1 = airborne, 2 = crouch
#hand_state: 0 = default, 1 = mantle, 2 = walljump

signal player_death
signal moved_chunks

func _process(delta):

	common(delta)
	match state:
		0:
			grounded(delta)
		1:
			midair(delta)
		2:
			crouch(delta)
	
	default_hand(delta)
	
	move_and_slide()
	
func common(delta):
	# if Input.is_action_just_pressed("debug_bunnyhop"):
		# auto_jump = not auto_jump
	if auto_jump:
		Input.action_press("jump")
		
	if Input.is_action_just_pressed("jump"):
		buffered_jump = true
		buffer_jump()
		
	var chunk = Vector3i(floor(position.x/16.0), floor(position.y/16.0), floor(position.z/16.0))
	if chunk != current_chunk:
		emit_signal("moved_chunks", chunk.x, chunk.y, chunk.z)
		spawn_point = Vector3(position.x, 17, position.z)
		current_chunk = chunk
	if position.y < 3.6:
		water_overlay.visible = true
		gravity = 3
	else:
		water_overlay.visible = false
		gravity = 9.8
func grounded(delta):
	# state changes
	if buffered_jump == true:
		velocity.y = JUMP_VELOCITY
		buffered_jump = false
		state = 1
	if !is_on_floor():
		state = 1
	if Input.is_action_pressed("crouch"):
		state = 2
		if velocity.length() > 2:
			velocity += velocity.normalized() * 4
		return
	# get wasd input. determine direction
	var input_dir = Input.get_vector("move_left", "move_right", "move_forward", "move_backward")
	var direction = (transform.basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()
	var traction = 20
	velocity.x = lerp(velocity.x, direction.x * SPEED * 1, min(delta * traction, 1))
	velocity.z = lerp(velocity.z, direction.z * SPEED * 1, min(delta * traction, 1))
	velocity.y -= gravity * delta

func midair(delta):
	if is_on_floor():
		state = 0
	# get wasd input. determine direction
	var input_dir = Input.get_vector("move_left", "move_right", "move_forward", "move_backward")
	var direction = (transform.basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()
	var old_speed_sq = velocity.x * velocity.x + velocity.z * velocity.z
	velocity.x += direction.x * SPEED * 1 * 3 * delta
	velocity.z += direction.z * SPEED * 1 * 3 * delta
	var new_speed_sq = velocity.x * velocity.x + velocity.z * velocity.z
	if new_speed_sq > old_speed_sq and new_speed_sq > MAX_AIR_SPEED:
		# this is slow but whatever. 2 sqrts
		var velocity_y_temp = velocity.y
		velocity /= sqrt(new_speed_sq)
		velocity *= sqrt(old_speed_sq)
		velocity.y = velocity_y_temp
	velocity.y -= gravity * delta
#	if global_position.y < -10:
#		velocity = Vector3.ZERO
#		global_position = spawn_point
		
func crouch(delta):
	if !Input.is_action_pressed("crouch") and velocity.length() < 0.6:
		state = 0
	if !is_on_floor():
		state = 1
	# get wasd input. determine direction
	var input_dir = Input.get_vector("move_left", "move_right", "move_forward", "move_backward")
	var direction = (transform.basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()
	var traction = 2
	velocity.x = lerp(velocity.x, direction.x * SPEED * 0.2, min(delta * traction, 1))
	velocity.z = lerp(velocity.z, direction.z * SPEED * 0.2, min(delta * traction, 1))
	velocity.y -= gravity * delta

func default_hand(delta):
	if !buffered_jump and hand_state == 0 and Input.is_action_pressed("jump") and $MantleRay.is_colliding() and $MantleRay.get_collision_normal().dot(Vector3.UP) > 0.85:
		print("mantle")
		await mantle(delta)
	elif buffered_jump and is_on_wall_only() and (hand_state == 0 or hand_state == 2):
		print("walljump")
		await walljump(delta)

func mantle(delta):
	hand_state = 1
	camera_arm.turn_camera(camera_arm.yaw, camera_arm.pitch-0.2)
	var transform_basis = transform.basis.z
	velocity /= 2
	velocity.y = 3.5
	while $MantleRay.is_colliding():
		velocity.y = 3.5
		velocity += transform_basis * -60 * delta
		await get_tree().process_frame
	await get_tree().create_timer(0.4).timeout
	hand_state = 0

	
func walljump(delta):
	hand_state = 2
	# Wall jump
	buffered_jump = false
	velocity.y += JUMP_VELOCITY*0.5
	var collision_normal = get_slide_collision(0).get_normal()
	
	# Calculate direction to look in after walljump
	var cam_direction = -PI/2
	if collision_normal.z != 0:
		cam_direction = atan(collision_normal.x / collision_normal.z)
	if not (collision_normal.z < 0 or collision_normal.x == 1):
		cam_direction += PI
	# Don't rotate ALL the way there - lerp between current dir and new dir
	cam_direction = lerp_angle(cam_direction, camera_arm.yaw, 0.8)
	
	camera_arm.turn_camera(cam_direction, camera_arm.pitch)
	# push away from wall
	velocity += collision_normal * 4.5
	await get_tree().create_timer(0.2).timeout
	hand_state = 0
	

func buffer_jump():
	await get_tree().create_timer(0.08).timeout
	buffered_jump = false
