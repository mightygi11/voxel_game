extends Node3D

var chunk_size = 16
var chunk_file: PackedScene = load("res://chunk.tscn")

var chunk_queue = []

var render_distance = 8
var generating = false

var chunk_thread: Thread
var current_chunk: Vector3i = Vector3i.ZERO

@onready var player = $"../Player"

# Called when the node enters the scene tree for the first time.
func _ready():
	current_chunk.y = (player.position.y/chunk_size)
	chunk_thread = Thread.new()
	await generate_chunk(0,0,0, false)
	await generate_chunk(-1,0,0, false)
	await generate_chunk(0,0,-1, false)
	await generate_chunk(-1,0,-1, false)
	gen_chunks()
	
func _exit_tree():
	chunk_thread.wait_to_finish()
	
func _process(delta):
	if !chunk_queue.is_empty() and generating == false:
		var chunk: Vector3i = chunk_queue.pop_front()
		# print("Generating chunks (queued: %s)" % chunk_queue.size())
		var i = 0
		while get_node_or_null("%s-%s-%s" % [chunk.x, chunk.y, chunk.z]):
			i += 1
			if chunk_queue.is_empty() or i > 10:
				generating = false
				return
			chunk = chunk_queue.pop_front()
		generate_chunk(chunk.x, chunk.y, chunk.z)

	
func gen_chunks():
	for x in range(-2, 2):
		for y in range(0, 3):
			for z in range(-2, 2):
				await generate_chunk(x,y,z,false)

func generate_chunk(x, y, z, lazy=true): # x y z corresponds to chunk xyzs
	# print("Generating chunk (queued: %s)" % chunk_queue.size())
	generating = true
	var x_diff = (player.position.x/chunk_size) - x
	var y_diff = (player.position.y/chunk_size) - y
	var z_diff = (player.position.z/chunk_size) - z
	var chunk_node = get_node_or_null("%s-%s-%s" % [x, y, z])
	if chunk_node:
		if chunk_node.generating:
			chunk_queue.push_back(Vector3i(x, y, z))
			generating = false
			return
		if abs(x_diff) < render_distance-2 and abs(z_diff) < render_distance - 2:
			if chunk_node.lowRes:
				await chunk_node.GenerateChunk(x,y,z,chunk_size,lazy)
		else:
			if !chunk_node.lowRes:
				await chunk_node.GenerateChunkLod(x,y,z,chunk_size,lazy)
		generating = false
		return
	
	if abs(x_diff) > render_distance or abs(y_diff) > 3 or abs(z_diff) > render_distance:
		generating = false
		return
	var chunk = chunk_file.instantiate()
	
	chunk.name = "%s-%s-%s" % [x, y, z]
	add_child(chunk)
	if abs(x_diff) < render_distance/2 and abs(z_diff) < render_distance/2:
		await chunk.GenerateChunk(x,y,z,chunk_size,true)
	else:
		await chunk.GenerateChunkLod(x,y,z,chunk_size,true)
	generating = false

func _on_player_moved_chunks(x, y, z):
	print("Chunk totals: %s, %s" % [get_child_count(), chunk_queue.size()])
	# remove chunks
	var lazy_counter = 0
	var chunks_to_free = []
	for chunk in get_children():
		if lazy_counter > 100:
			lazy_counter = 0
			await get_tree().process_frame
		lazy_counter += 1
		if (!chunk):
			continue
		var x_diff = (chunk.position.x/chunk_size) - x
		var y_diff = (chunk.position.y/chunk_size) - y
		var z_diff = (chunk.position.z/chunk_size) - z
		if abs(x_diff) < render_distance/2 and abs(z_diff) < render_distance/2:
			var chunk_x = (chunk.position.x/chunk_size)
			var chunk_y = (chunk.position.y/chunk_size)
			var chunk_z = (chunk.position.z/chunk_size)
			await generate_chunk(chunk_x, chunk_y, chunk_z, true)
		if abs(x_diff) > render_distance or abs(y_diff) > 3 or abs(z_diff) > render_distance:
			chunk.queue_free()
#			chunk.visible = false
#			chunk.process_mode = Node.PROCESS_MODE_DISABLED
#			if abs(x_diff) > render_distance + 2 or abs(z_diff) > render_distance + 2 or abs(y_diff) > 3:
#				chunk.queue_free()
#		else:
#			chunk.process_mode = Node.PROCESS_MODE_INHERIT
#			chunk.visible = true
	if true:
		if y >= 0:
			for i in range(x-render_distance+1, x+render_distance):
				for j in range(-1, y+2):
					for k in range(z-render_distance+1, z+render_distance):
						if (!chunk_queue.has(Vector3i(i,j,k))):
							chunk_queue.push_back(Vector3i(i,j,k))
		else:
			for i in range(x-2, x+3):
				for j in range(y-2, y+2):
					for k in range(z-2, z+3):
						if (!chunk_queue.has(Vector3i(i,j,k))):
							chunk_queue.push_back(Vector3i(i,j,k))
		
