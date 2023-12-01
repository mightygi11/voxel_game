extends StaticBody3D
class_name Chunk

var generator
var lazy = true

var vertices = PackedVector3Array()
var normals = PackedVector3Array()
var uvs = PackedVector2Array()

var chunk_size: int

var cubes: Array[Vector3i]
var cube_ids: Array[int]
var cubes_x := []

@export var material: StandardMaterial3D

func generate_chunk(x, y, z):
	generator = $BlockChooser
	var mesh_data = []
	mesh_data.resize(ArrayMesh.ARRAY_MAX)
	
	# terrain generation formula stuff
	for x_cube in range(chunk_size):
		var cubes_y = []
		for y_cube in range(chunk_size):
			var cubes_z = []
			for z_cube in range(chunk_size):
				var block = generator.generate(x_cube+x*16, y_cube+y*16, z_cube+z*16)
				if block != -1:
					cubes.append(Vector3i(x_cube, y_cube, z_cube))
					cube_ids.append(block)
					cubes_z.append(true)
				else:
					cubes_z.append(false)
			cubes_y.append(cubes_z)
		cubes_x.append(cubes_y)
	
	# actual mesh creation
	for i in range(len(cubes)):
		if lazy and i % 100 == 0:
			await get_tree().process_frame
		var cube = cubes[i]
		var id = cube_ids[i]
		create_cube(cube.x, cube.y, cube.z, id)
	
	mesh_data[ArrayMesh.ARRAY_VERTEX] = vertices
	mesh_data[ArrayMesh.ARRAY_NORMAL] = normals
	mesh_data[ArrayMesh.ARRAY_TEX_UV] = uvs
	var arr_mesh = ArrayMesh.new()
	arr_mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, mesh_data)
	$ChunkMesh.mesh = arr_mesh
	$ChunkMesh.material_override = material
	position = Vector3(x, y, z) * chunk_size
	var collision = $ChunkMesh.mesh.create_trimesh_shape()
	$ChunkCollision.shape = collision

func create_square(x, y, z, orientation=0, id = 0):
	match orientation:
		0: # up
			vertices.push_back(Vector3(x, y+1, z))
			vertices.push_back(Vector3(x+1, y+1, z))
			vertices.push_back(Vector3(x, y+1, z+1))
			vertices.push_back(Vector3(x+1, y+1, z))
			vertices.push_back(Vector3(x+1, y+1, z+1))
			vertices.push_back(Vector3(x, y+1, z+1))
		1: # down
			vertices.push_back(Vector3(x, y, z))
			vertices.push_back(Vector3(x, y, z+1))
			vertices.push_back(Vector3(x+1, y, z))
			vertices.push_back(Vector3(x, y, z+1))
			vertices.push_back(Vector3(x+1, y, z+1))
			vertices.push_back(Vector3(x+1, y, z))
		2: # x+
			vertices.push_back(Vector3(x+1, y+1, z+1))
			vertices.push_back(Vector3(x+1, y+1, z))
			vertices.push_back(Vector3(x+1, y, z+1))
			vertices.push_back(Vector3(x+1, y+1, z))
			vertices.push_back(Vector3(x+1, y, z))
			vertices.push_back(Vector3(x+1, y, z+1))
		3: # x-
			vertices.push_back(Vector3(x, y+1, z))
			vertices.push_back(Vector3(x, y+1, z+1))
			vertices.push_back(Vector3(x, y, z))
			vertices.push_back(Vector3(x, y+1, z+1))
			vertices.push_back(Vector3(x, y, z+1))
			vertices.push_back(Vector3(x, y, z))
		4: # z+
			vertices.push_back(Vector3(x, y+1, z+1))
			vertices.push_back(Vector3(x+1, y+1, z+1))
			vertices.push_back(Vector3(x, y, z+1))
			vertices.push_back(Vector3(x+1, y+1, z+1))
			vertices.push_back(Vector3(x+1, y, z+1))
			vertices.push_back(Vector3(x, y, z+1))
		5: # z-
			vertices.push_back(Vector3(x+1, y+1, z))
			vertices.push_back(Vector3(x, y+1, z))
			vertices.push_back(Vector3(x+1, y, z))
			vertices.push_back(Vector3(x, y+1, z))
			vertices.push_back(Vector3(x, y, z))
			vertices.push_back(Vector3(x+1, y, z))
	var normal_vec: Vector3
	match orientation:
		0:
			normal_vec = Vector3.UP
		1:
			normal_vec = Vector3.DOWN
		2:
			normal_vec = Vector3.RIGHT
		3:
			normal_vec = Vector3.LEFT
		4:
			normal_vec = Vector3.BACK
		5:
			normal_vec = Vector3.FORWARD
	for i in range(6):
		normals.push_back(normal_vec)
	add_texture(id)
	
func create_cube(x, y, z, id = 0, cull_interiors=true):
	for i in range(6):
		if cull_interiors and is_neighbor(x, y, z, i):
			continue
		create_square(x, y, z, i, id)

func add_texture(id:int=3):
	var texmap_size = 2
	var offset_x = id/(texmap_size)/float(texmap_size)
	var offset_y = (id%texmap_size)/float(texmap_size)
	var tex_scale = 1/float(texmap_size)
	uvs.push_back(Vector2(offset_x,offset_y))
	uvs.push_back(Vector2(offset_x+tex_scale,offset_y))
	uvs.push_back(Vector2(offset_x,offset_y+tex_scale))
	uvs.push_back(Vector2(offset_x+tex_scale,offset_y))
	uvs.push_back(Vector2(offset_x+tex_scale,offset_y+tex_scale))
	uvs.push_back(Vector2(offset_x,offset_y+tex_scale))

func is_neighbor(x, y, z, orientation=0):
	match orientation:
		0:
			if y == chunk_size-1: return false
			if cubes_x[x][y+1][z] == true:
				return true
			else:
				return false
		1:
			if y == 0: return false
			if cubes_x[x][y-1][z] == true:
				return true
			else:
				return false
		2:
			if x == chunk_size-1: return false
			if cubes_x[x+1][y][z] == true:
				return true
			else:
				return false
		3:
			if x == 0: return false
			if cubes_x[x-1][y][z] == true:
				return true
			else:
				return false
		4:
			if z == chunk_size-1: return false
			if cubes_x[x][y][z+1] == true:
				return true
			else:
				return false
		5:
			if z == 0: return false
			if cubes_x[x][y][z-1] == true:
				return true
			else:
				return false
	
