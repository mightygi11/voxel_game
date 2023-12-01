extends Node

var reservations: Array[ReservationArea]
var world_env
var modified_items := {}
var render_distance = 4
@onready var chunk_generator = get_tree().root.get_node("main/Chunks")

signal render_dist_changed

class ReservationArea:
	var blockID: int
	var chunk: Vector3i
	var bound1: Vector3i
	var bound2: Vector3i

func update_render_distance(dist: int, player_chunk_pos: Vector3i):
	render_distance = dist
	chunk_generator.renderDistance = dist
	chunk_generator.PlayerChunkUpdate(player_chunk_pos)

# Called when the node enters the scene tree for the first time.
func is_reserved(chunk, x, y, z) -> bool:
	for r in reservations:
		if x > r.bound1.x and y > y.bound1.y and z > r.bound1.z:
			if x < r.bound2.x and y < y.bound2.y and z < r.bound2.z:
				return true
	return false
		

func reserve(chunk, x1, y1, z1, x2, y2, z2, id):
	pass
