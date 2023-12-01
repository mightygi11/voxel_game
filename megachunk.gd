extends Node3D

@onready var megachunk_file = preload("res://megachunk.tscn")
@onready var chunk_file = preload("res://chunk.tscn")
var generated = false
var generating = false
var low_res = false
var chunk_size = 32
var child
# Global position
var chunk_pos: Vector3i
var megachunk_size = 16
var render_distance = 4

func _init():
	render_distance = Reserved.render_distance
	Reserved.connect("render_dist_changed", _on_render_distance_changed)

# Called when the node enters the scene tree for the first time.
func generate(x, y, z, lazy = true):
	# print("Generating megachunk (size %s) at %s" % [megachunk_size, chunk_pos])
	var player_vec = Vector3i(x, y, z)
	var player_dist = distance(player_vec)
	if player_dist < render_distance + (megachunk_size * 2):
		visible = true
		# if player_dist >= render_distance:
		if player_dist >= 0:
			if megachunk_size == 1:
				if !generating:
					generating = true
					if player_dist > render_distance / 2:
						low_res = true
					elif player_dist < render_distance / 2:
						low_res = false
					if !generated:
						generated = true
						child = chunk_file.instantiate()
						add_child(child)
						child.GenerateChunkLod(chunk_pos.x, chunk_pos.y, chunk_pos.z, chunk_size, lazy)
					elif !child.generating:
						if child.lowRes != low_res:
							if low_res:
								child.GenerateChunkLod(chunk_pos.x, chunk_pos.y, chunk_pos.z, chunk_size, lazy)
							else:
								child.GenerateChunk(chunk_pos.x, chunk_pos.y, chunk_pos.z, chunk_size, lazy)
					generating = false
			else:
				if !generated:
					generated = true
					for i in range(0,2):
						for j in range(0,2):
							for k in range(0,2):
								var new_child = megachunk_file.instantiate()
								var minichunk_size = megachunk_size/2
								var offset = minichunk_size
								new_child.megachunk_size = minichunk_size
								new_child.position = Vector3(i*(minichunk_size-offset), j*(minichunk_size-offset), k*(minichunk_size-offset))
								new_child.chunk_pos = chunk_pos + Vector3i(i*minichunk_size, j*minichunk_size, k*minichunk_size)
								add_child(new_child)
				for gen_child in get_children():
					await get_tree().process_frame
					await gen_child.generate(x, y, z, lazy)
	else:
		visible = false

func _on_render_distance_changed(dist: int):
	render_distance = dist
	

func distance(vec1:Vector3i):
	return Vector3(abs(vec1.x-chunk_pos.x), abs(vec1.y-chunk_pos.y), abs(vec1.z-chunk_pos.z)).length()
	#return abs(vec1.x-chunk_pos.x) + abs(vec1.y-chunk_pos.y) + abs(vec1.z-chunk_pos.z)
