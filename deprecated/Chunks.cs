using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

public partial class Chunks : StaticBody3D
{

	public bool generating = false;
	public bool lazy = true;
	public int chunkSize = 32;

	public bool lowRes = false;

	public bool lowResGenerated = false;
	public bool hiResGenerated = false;

	List<Vector3> vertices = new List<Vector3>();
	List<Vector3> normals = new List<Vector3>();
	List<Vector2> uvs = new List<Vector2>();

	List<Vector3> waterVertices = new List<Vector3>();
	List<Vector3> waterNormals = new List<Vector3>();
	List<Vector2> waterUvs = new List<Vector2>();

	List<Vector3I> cubes = new();
	List<int> cubeIds = new();
	List<bool> cubeCoords;

	Vector3I chunkPosition = new(0, 0, 0);

	[Export]
	public FastNoiseLite smallNoise;
	[Export]
	public FastNoiseLite waterNoise;
	[Export]
	public FastNoiseLite biomeNoise;
	[Export]
	public FastNoiseLite biomeNoise2;
	[Export]
	public FastNoiseLite caveNoise;
	[Export]
	public FastNoiseLite treeNoise;
	[Export]
	public FastNoiseLite temperatureNoise;

	private Node Reserved;

	Node3D ObjectsNode;

	System.Collections.Generic.Dictionary<Vector3I, int> objects = new();

	public async void GenerateChunkLod(int xChunk, int yChunk, int zChunk, int chunkSize, bool lazy){
		ObjectsNode = (Node3D)GetNode("ChunkMesh");
		MeshInstance3D chunkMesh = (MeshInstance3D)GetNode("ChunkMesh");
		MeshInstance3D waterMesh = (MeshInstance3D)GetNode("WaterMesh");
		MeshInstance3D lodChunkMesh = (MeshInstance3D)GetNode("LodChunkMesh");
		SetCollisionLayerValue(1, false);
		lowRes = true;
		if (lowResGenerated == true){
			chunkMesh.Visible = false;
			waterMesh.Visible = false;
			lodChunkMesh.Visible = true;
			Scale = new Vector3(2,2,2);
			return;
		}
		generating = true;
		int lodChunkSize = chunkSize/2;

		Reserved = GetNode("/root/Reserved");
		Position = new Vector3(xChunk, yChunk, zChunk) * chunkSize;
		
		chunkPosition = new Vector3I(xChunk, yChunk, zChunk);
		cubeCoords = new(lodChunkSize*lodChunkSize*lodChunkSize);
		cubes = new(lodChunkSize*lodChunkSize*lodChunkSize);
		cubeIds = new(lodChunkSize*lodChunkSize*lodChunkSize);
		this.lazy = lazy;
		this.chunkSize = chunkSize;

		var meshData = new Godot.Collections.Array();
		meshData.Resize((int)Mesh.ArrayType.Max);
		var waterMeshData = new Godot.Collections.Array();
		waterMeshData.Resize((int)Mesh.ArrayType.Max);

		int x = 0;
		int y = 0;
		int z = 0;

		

		for (int b = 0; b < lodChunkSize*lodChunkSize*lodChunkSize; b++){
			if (x == lodChunkSize){
				x = 0;
				y += 1;
				if (lazy){
					await ToSignal(GetTree(), "process_frame");
				}
				if (y == lodChunkSize){
					y = 0;
					z += 1;
				}
			}
			int block = Generate((x*2)+(xChunk*chunkSize), (y*2)+(yChunk*chunkSize), (z*2)+zChunk*chunkSize);
			if (block != -1){
				cubeIds.Add(block);
			} else {
				cubeIds.Add(-1);
			}
			x++;	
		}
		x = 0;
		y = 0;
		z = 0;
		
		// actual mesh creation
		for (int i = 0; i < cubeIds.Count; i++){
			if (x == lodChunkSize){
				x = 0;
				y += 1;
				if (lazy){
					await ToSignal(GetTree(), "process_frame");
				}
				if (y == lodChunkSize){
					y = 0;
					z += 1;
					
				}
			}
			if (cubeIds[i] == -1){
				x += 1;
				continue;
			}
			// Vector3I cube = cubes[i];
			int id = cubeIds[i];
			createCube(x, y, z, id);
			x += 1;
		}

		// set up the new mesh
		if (vertices.Count != 0){
			meshData[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
			meshData[(int)Mesh.ArrayType.Normal] = normals.ToArray();
			meshData[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();

			ArrayMesh arrMesh = new ArrayMesh();

			arrMesh?.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshData);
			lodChunkMesh.Mesh = arrMesh;
			lodChunkMesh.Visible = true;
			chunkMesh.Visible = false;
			waterMesh.Visible = false;
		}
		Scale = new Vector3(2, 2, 2);
		lowResGenerated = true;
		generating = false;

	}
	public async void GenerateChunk(int xChunk, int yChunk, int zChunk, int chunkSize, bool lazy){
		ObjectsNode = (Node3D)GetNode("ChunkMesh");
		MeshInstance3D chunkMesh = (MeshInstance3D)GetNode("ChunkMesh");
		MeshInstance3D lodChunkMesh = (MeshInstance3D)GetNode("LodChunkMesh");
		MeshInstance3D waterMesh = (MeshInstance3D)GetNode("WaterMesh");
		lowRes = false;
		SetCollisionLayerValue(1, true);
		if (hiResGenerated == true){
			chunkMesh.Visible = true;
			waterMesh.Visible = true;
			lodChunkMesh.Visible = false;
			Scale = new Vector3(1,1,1);
			return;
		}
		generating = true;
		Reserved = GetNode("/root/Reserved");
		Position = new Vector3(xChunk, yChunk, zChunk) * chunkSize;
		chunkPosition = new Vector3I(xChunk, yChunk, zChunk);
		cubeCoords = new(chunkSize*chunkSize*chunkSize);
		cubes = new(chunkSize*chunkSize*chunkSize);
		cubeIds = new(chunkSize*chunkSize*chunkSize);
		this.lazy = lazy;
		this.chunkSize = chunkSize;

		vertices.Clear();
		normals.Clear();
		uvs.Clear();

		var meshData = new Godot.Collections.Array();
		meshData.Resize((int)Mesh.ArrayType.Max);
		var waterMeshData = new Godot.Collections.Array();
		waterMeshData.Resize((int)Mesh.ArrayType.Max);

		int x = 0;
		int y = 0;
		int z = 0;

		for (int b = 0; b < chunkSize*chunkSize*chunkSize; b++){
			if (x == chunkSize){
				x = 0;
				y += 1;
				if (y == chunkSize){
					y = 0;
					z += 1;
					if (lazy){
						await ToSignal(GetTree(), "process_frame");
					}
				}
			}
			// int block = (int)generator.Call("generate", x+(xChunk*chunkSize), y+(yChunk*chunkSize), z+zChunk*chunkSize);
			int block = Generate(x+(xChunk*chunkSize), y+(yChunk*chunkSize), z+zChunk*chunkSize);
			
			if (block != -1){
				// cubes.Add(new Vector3I(x, y, z));
				cubeIds.Add(block);
				cubeCoords.Add(block != 3); // for solidness purposes, water is not counted as a block
			} else {
				cubeIds.Add(-1); // remove?
				cubeCoords.Add(false);
			}
			x += 1;
		}

		x = 0;
		y = 0;
		z = 0;
		
		// actual mesh creation
		// water is made separately from rest of blocks
		for (int i = 0; i < cubeIds.Count; i++){
			if (x == chunkSize){
				x = 0;
				y += 1;
				if (lazy){
					await ToSignal(GetTree(), "process_frame");
				}
				if (y == chunkSize){
					y = 0;
					z += 1;
					
				}
			}
			if (cubeIds[i] == -1){
				x += 1;
				continue;
			}
			// Vector3I cube = cubes[i];
			int id = cubeIds[i];
			createCube(x, y, z, id);
			x += 1;
		}
		
		

		if (vertices.Count != 0){
			meshData[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
			meshData[(int)Mesh.ArrayType.Normal] = normals.ToArray();
			meshData[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();

			
			CollisionShape3D chunkCollision = (CollisionShape3D)GetNode("ChunkCollision");
			ArrayMesh arrMesh = new ArrayMesh();

			if (arrMesh != null){
				arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshData);
			}
			chunkMesh.Mesh = arrMesh;
			// chunkMesh.MaterialOverride = material;
			var collision = chunkMesh.Mesh.CreateTrimeshShape();
			chunkMesh.Visible = true;
			chunkCollision.Shape = collision;
		}
		if (waterVertices.Count != 0){
			// water time

			waterMeshData[(int)Mesh.ArrayType.Vertex] = waterVertices.ToArray();
			waterMeshData[(int)Mesh.ArrayType.Normal] = waterNormals.ToArray();
			waterMeshData[(int)Mesh.ArrayType.TexUV] = waterUvs.ToArray();

			ArrayMesh waterArrMesh = new ArrayMesh();
			if (waterArrMesh != null) {
				waterArrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, waterMeshData);
			}
			waterMesh.Visible = true;
			waterMesh.Mesh = waterArrMesh;
		}
		lodChunkMesh.Visible = false;
		Scale = new Vector3(1,1,1);
		
		ObjectPass();
		hiResGenerated = true;
		generating = false;
	}
	
	
	// Get the block at some xyz position.
	int Generate(int x, int y, int z){
		float temperatureSample = temperatureNoise.GetNoise2D(x, z) * 25;
		float hilliness = waterNoise.GetNoise2D(x, z);
		float modY = (y / 5)*5 - 4;
		float modY2 = (y / 2)*2 - 4;
		float biomeSample1 = biomeNoise.GetNoise3D(x, modY, z);
		float biomeSample2 = biomeNoise2.GetNoise3D(x, modY2, z);
		double modSample = smallNoise.GetNoise2D(x, z) * 5;
		modSample += biomeSample1 * 60 + 10;
		modSample += biomeSample2 * 60;
		temperatureSample -= y + (hilliness * 2);
		modSample -= modY;
		float caveSample = caveNoise.GetNoise3D(x, y, z);
		modSample += caveSample * 10;
		if (modSample > 0){
			if (y < 4){
				if (temperatureSample > -9){
					return 4; // sand
				}
				return 2; // stone
			}
			else if (temperatureSample - y > -6){
				return 4; // sand
			} else if (temperatureSample > -33) {
				if (y + hilliness * 3 < 14){
					if (temperatureSample < -10 || temperatureSample > -2){
						return 1;
					}
					return 0;
				}
				return 2; // stone
			} else {
				return 5; // snow
			}
			
		}
		if (y < 4){
			if (temperatureSample < -10){
				return 6; // ice
			}
			return 3; // water
		}
		return -1; // air
	}
	int Generate2(int x, int y, int z){
		float modY = (y / 5)*5 - 4;
		float modY2 = (y / 2)*2 - 4;
		float biomeSample1 = biomeNoise.GetNoise3D(x, modY, z);
		float biomeSample2 = biomeNoise2.GetNoise3D(x, modY2, z);
		float hilliness = biomeNoise2.GetNoise2D(x, z);
		double realBiomeSample = caveNoise.GetNoise2D(x, z);
		realBiomeSample = Math.Clamp((realBiomeSample) * 10 + 0.5, 0, 1);
		double modSample = smallNoise.GetNoise2D(x, z) * realBiomeSample * 5;
		modSample += biomeSample1 * 30 * 1;
		modSample += biomeSample2 * 30 * realBiomeSample;
		modSample -= modY;
		modSample += caveNoise.GetNoise3D(x, y, z);
		if (modSample > 0){
			if (y + (hilliness * 10) < 10){
				if (y < 4){
					return 4; // sand
				}
				if (realBiomeSample < 0.5){
					return 4; // sand
				}
				return 0; // grass
			}
			return 2;
		}
		if (y < 4){
			return 3;
		}
		return -1; // air
	}

	// Only runs after all blocks in chunk have been generated.
	void ObjectPass(){
		PackedScene safetyScene = GD.Load<PackedScene>("res://safety.tscn");
		PackedScene rockScene = GD.Load<PackedScene>("res://rock.tscn");
		PackedScene chestScene = GD.Load<PackedScene>("res://chest.tscn");
		PackedScene caveScene = GD.Load<PackedScene>("res://cave.tscn");

		Dictionary modItems = (Dictionary)Reserved.Get("modified_items");
		String chunkName = Name;

		List<Vector3I> successfulTrees = new();

		bool generatedSafety = false;
		foreach (KeyValuePair<Vector3I, int> entry in objects){
			// safety generation
			Vector3I realPos = entry.Key + chunkPosition*chunkSize;
			float biomeSample1 = biomeNoise.GetNoise3D(realPos.X, realPos.Y, realPos.Z);
			if (chunkPosition.Y > 0 && !generatedSafety && chunkPosition.X % 5 == 0 && chunkPosition.Z % 5 == 0){
				Vector3I safety = entry.Key;
				if (Math.Abs(biomeSample1.GetHashCode()) % 32 == 0){
					// check if there is space to spawn a safety zone
					if (isSpaceClear(new Vector3I(0, 0, 0)+safety, new Vector3I(1, 4, 1)+safety)){
						bool generating = true;
						for (int j = 0; j < 4; j++){
							if (isSpaceClear(new Vector3I((j % 2), -1, j / 2)+safety, new Vector3I((j % 2), -1, j / 2)+safety)){
								generating = false;
								break;
							}
						}
						if (generating){
							GD.Print("genning safety");
							// spawn in safety zone
							StaticBody3D instance = (StaticBody3D)safetyScene.Instantiate();
							ObjectsNode.AddChild(instance);
							instance.Position = new Vector3(safety.X, safety.Y, safety.Z);
							generatedSafety = true;
						}
					}
				}
			// chests
			} else if (Math.Abs(biomeSample1.GetHashCode()) % 51 == 0){
				Vector3I chest = entry.Key;
				if (isSpaceClear(new Vector3I(-1, 0, -1)+chest, new Vector3I(1, 1, 1)+chest)){
					// don't spawn the chest if it's already been opened
					if (modItems.ContainsKey(chunkName)){
						Array<Vector3I> chestCoords = (Array<Vector3I>)modItems[chunkName];
						if (chestCoords.Contains(new Vector3I(chest.X, chest.Y, chest.Z))){
							continue;
						}
					}
					int direction = Math.Abs(biomeSample1.GetHashCode()) % 4;
					// is there a low ceiling
					if (!isSpaceClear(new Vector3I(0, 2, 0)+chest, new Vector3I(0, 3, 0)+chest)){
						// spawn in chest
						StaticBody3D instance = (StaticBody3D)chestScene.Instantiate();
						ObjectsNode.AddChild(instance);
						instance.Position = new Vector3(chest.X, chest.Y, chest.Z);
						instance.GetNode<Node3D>("mesh").Rotation = new Vector3(0, direction * (float)Math.PI / 2.0f, 0);
					} else {
						// check if space is sufficiently occluded (by 3+ nearby walls)
						int occludedDirections = 0;
						
						if (!isSpaceClear(new Vector3I(-5, 1, 0)+chest, new Vector3I(-2, 1, 0)+chest)){
							occludedDirections++;
						} else {
							direction = 2;
						}
						if (!isSpaceClear(new Vector3I(2, 1, 0)+chest, new Vector3I(5, 1, 0)+chest)){
							occludedDirections++;
						} else {
							direction = 0;
						}
						if (!isSpaceClear(new Vector3I(0, 1, -5)+chest, new Vector3I(0, 1, -2)+chest)){
							occludedDirections++;
						} else {
							direction = 1;
						}
						if (!isSpaceClear(new Vector3I(0, 1, 2)+chest, new Vector3I(0, 1, 5)+chest)){
							occludedDirections++;
						} else {
							direction = 3;
						}
						if (occludedDirections >= 3){
							StaticBody3D instance = (StaticBody3D)chestScene.Instantiate();
							ObjectsNode.AddChild(instance);
							instance.Position = new Vector3(chest.X, chest.Y, chest.Z);
							instance.GetNode<Node3D>("mesh").Rotation = new Vector3(0, direction * (float)Math.PI / 2.0f, 0);
						}
					}
				}
			// trees
			} else if (entry.Value == 0){
				int treeThreshold = 1;
				if (treeNoise.GetNoise2D(realPos.X, realPos.Z) > 0.1){
					treeThreshold = 50;
				}
				
				if (Math.Abs(biomeSample1.GetHashCode()) % 100 <= treeThreshold){
					Vector3I tree = entry.Key;
					// try to spawn tree at position
					// check if there is space to spawn a tree
					if (isSpaceClear(new Vector3I(-1, 0, -1)+tree, new Vector3I(1, 2, 1)+tree)){
						// add tree to multimesh
						successfulTrees.Add(tree);
						/* StaticBody3D instance = (StaticBody3D)treeScene.Instantiate();
						AddChild(instance);
						instance.Position = new Vector3(tree.X, tree.Y, tree.Z); */
					}
				}
				
			// rocks	
			} else if (entry.Value == 2 && Math.Abs(biomeSample1.GetHashCode()) % 100 <= 2){
				Vector3I rock = entry.Key;
				if (isSpaceClear(new Vector3I(0, 0, 0)+rock, new Vector3I(0, 0, 0)+rock)){
					// spawn in rock
					StaticBody3D instance = (StaticBody3D)rockScene.Instantiate();
					ObjectsNode.AddChild(instance);
					instance.Position = new Vector3(rock.X, rock.Y, rock.Z);

				}
			} else if (Math.Abs(biomeSample1.GetHashCode()) % 29 == 0){
				Vector3I cave = entry.Key;
				if (isSpaceClear(new Vector3I(-1, 0, -1)+cave, new Vector3I(0, 1, 0)+cave)){
					if (!isSpaceFilled(new Vector3I(-1, -1, -1)+cave, new Vector3I(0, -1, 0)+cave)){
						continue;
					}
					int direction = -1;
					if (isSpaceFilled(new Vector3I(-2, 0, -2)+cave, new Vector3I(1, 2, -2)+cave)){
						direction = 0;
					} else if (isSpaceFilled(new Vector3I(-2, 0, -2)+cave, new Vector3I(-2, 2, 1)+cave)){
						direction = 1;
					} else if (isSpaceFilled(new Vector3I(-2, 0, 1)+cave, new Vector3I(1, 2, 1)+cave)){
						direction = 2;
					} else if (isSpaceFilled(new Vector3I(1, 0, -2)+cave, new Vector3I(1, 2, 1)+cave)){
						direction = 3;
					}
					if (direction != -1){
						StaticBody3D instance = (StaticBody3D)caveScene.Instantiate();
						ObjectsNode.AddChild(instance);
						instance.Position = new Vector3(cave.X, cave.Y, cave.Z);
						instance.GetNode<Node3D>("Mesh").Rotation = new Vector3(0, direction * (float)Math.PI / 2.0f, 0);
						if (direction == 0 || direction == 2){
							if (objects.ContainsKey(new Vector3I(-1, 0, 0)+cave)){
								objects.Remove(new Vector3I(-1, 0, 0)+cave);
							}
							if (objects.ContainsKey(new Vector3I(1, 0, 0)+cave)){
								objects.Remove(new Vector3I(1, 0, 0)+cave);
							}
						}
						if (direction == 1 || direction == 3){
							if (objects.ContainsKey(new Vector3I(0, 0, -1)+cave)){
								objects.Remove(new Vector3I(0, 0, -1)+cave);
							}
							if (objects.ContainsKey(new Vector3I(0, 0, 1)+cave)){
								objects.Remove(new Vector3I(0, 0, 1)+cave);
							}
						}
					}
				}
			}
		}
		// multimesh finalizing
		if (successfulTrees.Count > 0){
			PackedScene multiTreeScene = GD.Load<PackedScene>("res://multi_tree.tscn");
			Mesh treeMesh = GD.Load<Mesh>("res://tree_mesh.tres");
			PackedScene treeCollision = GD.Load<PackedScene>("res://tree_collision.tscn");
			StaticBody3D multiInstance = (StaticBody3D)multiTreeScene.Instantiate();
			ObjectsNode.AddChild(multiInstance);
			// MultiMesh multiMesh = multiInstance.GetNode<MultiMeshInstance3D>("MultiMesh").Multimesh;
			MultiMesh multiMesh = new()
			{
				Mesh = treeMesh,
				TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
				InstanceCount = successfulTrees.Count,
				VisibleInstanceCount = successfulTrees.Count
			};
			for (int i = 0; i < successfulTrees.Count; i++){
				multiMesh.SetInstanceTransform(i, new Transform3D(Basis.FromScale(new Vector3(0.4f,0.4f,0.4f)), successfulTrees[i]));
				CollisionShape3D instance = (CollisionShape3D)treeCollision.Instantiate();
				instance.Position = successfulTrees[i] + new Vector3(0.5f, 1.5f, 0.5f);
				multiInstance.AddChild(instance);
			}
			multiInstance.GetNode<MultiMeshInstance3D>("MultiMesh").Multimesh = multiMesh;
		}
	}

	// Only runs after all blocks in chunk have been generated.
	bool isSpaceClear(Vector3I bound1, Vector3I bound2){
		for (int x = bound1.X; x <= bound2.X; x++){
			for (int y = bound1.Y; y <= bound2.Y; y++){
				for (int z = bound1.Z; z <= bound2.Z; z++){
					if (0 <= x && x <= 15 && 0 <= y && y <= 15 && 0 <= z && z <= 15){
						// case that the block is inside this chunk
						// check previously gen'd blocks if they're air
						int index = x + (y * chunkSize) + (z * chunkSize * chunkSize);
						if (cubeIds[index] != -1){
							// GD.Print("Found previously generated blocks in the way.");
							return false;
						}
					} else {
						// case that the block is outside the chunk
						// generate new blocks outside chunk and check if they're air
						// lots of throwing away data here if the checked area is big and mostly empty but whatever
						int result = Generate(x+(chunkPosition.X*chunkSize), y+(chunkPosition.Y*chunkSize), z+chunkPosition.Z*chunkSize);
						if (result != -1){
							// GD.Print("Found NOT generated blocks in the way.");
							return false;
						}
					}
				}
			}
		}
		return true;
	}

	bool isSpaceFilled(Vector3I bound1, Vector3I bound2){
		for (int x = bound1.X; x <= bound2.X; x++){
			for (int y = bound1.Y; y <= bound2.Y; y++){
				for (int z = bound1.Z; z <= bound2.Z; z++){
					if (0 <= x && x <= 15 && 0 <= y && y <= 15 && 0 <= z && z <= 15){
						// case that the block is inside this chunk
						// check previously gen'd blocks if they're air
						int index = x + (y * chunkSize) + (z * chunkSize * chunkSize);
						if (cubeIds[index] == -1){
							// GD.Print("Found previously generated blocks in the way.");
							return false;
						}
					} else {
						// case that the block is outside the chunk
						// generate new blocks outside chunk and check if they're air
						// lots of throwing away data here if the checked area is big and mostly empty but whatever
						int result = Generate(x+(chunkPosition.X*chunkSize), y+(chunkPosition.Y*chunkSize), z+chunkPosition.Z*chunkSize);
						if (result == -1){
							// GD.Print("Found NOT generated blocks in the way.");
							return false;
						}
					}
				}
			}
		}
		return true;
	}


	void createCube(int x, int y, int z, int id, bool cullInteriors=true){
		if (lowRes){
			for (int i = 0; i < 6; i++){
				if (isNeighborLod(x, y, z, i)){
					continue;
				}
				createSquare(x, y, z, id, i);
			}
			return;
		}
		if (id == 3){ // water
			for (int i = 0; i < 6; i++){
				if (cullInteriors && isWaterNeighbor(x, y, z, i)){
					continue;
				}
				createWaterSquare(x, y, z, id, i);
			}
			return;
		}
		for (int i = 0; i < 6; i++){
			if (cullInteriors && isNeighborOptimized(x, y, z, i)){
				continue;
			}
			createSquare(x, y, z, id, i);
		}
	}

	void createSquare(int x, int y, int z, int id, int orientation){
		Vector3 normalVec;
		switch (orientation){
			case 0: // up
				{
					if (!lowRes && !objects.ContainsKey(new Vector3I(x, y+1, z))){
						objects.Add(new Vector3I(x, y+1, z), id);
					}
						
					/* chests.Add(new Vector3I(x, y+1, z));
					if (chunkPosition.Y > 0){
						safetys.Add(new Vector3I(x, y+1, z));
					}
					if (id == 0){ // hacky way of defining object candidate positions
						trees.Add(new Vector3I(x, y+1, z));
					} else if (id == 2){
						rocks.Add(new Vector3I(x, y+1, z));
					} */
					normalVec = Vector3.Up;
					vertices.Add(new Vector3(x, y+1, z));
					vertices.Add(new Vector3(x+1, y+1, z));
					vertices.Add(new Vector3(x, y+1, z+1));
					vertices.Add(new Vector3(x+1, y+1, z));
					vertices.Add(new Vector3(x+1, y+1, z+1));
					vertices.Add(new Vector3(x, y+1, z+1));
					break;
				}
			case 1: // down
				{
					normalVec = Vector3.Down;
					vertices.Add(new Vector3(x, y, z));
					vertices.Add(new Vector3(x, y, z+1));
					vertices.Add(new Vector3(x+1, y, z));
					vertices.Add(new Vector3(x, y, z+1));
					vertices.Add(new Vector3(x+1, y, z+1));
					vertices.Add(new Vector3(x+1, y, z));
					break;
				}
			case 2: // x+
				{
					normalVec = Vector3.Right;
					vertices.Add(new Vector3(x+1, y+1, z+1));
					vertices.Add(new Vector3(x+1, y+1, z));
					vertices.Add(new Vector3(x+1, y, z+1));
					vertices.Add(new Vector3(x+1, y+1, z));
					vertices.Add(new Vector3(x+1, y, z));
					vertices.Add(new Vector3(x+1, y, z+1));
					break;
				}
			case 3: // x-
				{
					normalVec = Vector3.Left;
					vertices.Add(new Vector3(x, y+1, z));
					vertices.Add(new Vector3(x, y+1, z+1));
					vertices.Add(new Vector3(x, y, z));
					vertices.Add(new Vector3(x, y+1, z+1));
					vertices.Add(new Vector3(x, y, z+1));
					vertices.Add(new Vector3(x, y, z));
					break;
				}
			case 4: // z+
				{
					normalVec = Vector3.Back;
					vertices.Add(new Vector3(x, y+1, z+1));
					vertices.Add(new Vector3(x+1, y+1, z+1));
					vertices.Add(new Vector3(x, y, z+1));
					vertices.Add(new Vector3(x+1, y+1, z+1));
					vertices.Add(new Vector3(x+1, y, z+1));
					vertices.Add(new Vector3(x, y, z+1));
					break;
				}
			case 5: // z-
			default:
				{
					normalVec = Vector3.Forward;
					vertices.Add(new Vector3(x+1, y+1, z));
					vertices.Add(new Vector3(x, y+1, z));
					vertices.Add(new Vector3(x+1, y, z));
					vertices.Add(new Vector3(x, y+1, z));
					vertices.Add(new Vector3(x, y, z));
					vertices.Add(new Vector3(x+1, y, z));
					break;
				}
		}
		for (int i = 0; i < 6; i++){
			normals.Add(normalVec);
		}
		addTexture(id);
	}

	void addTexture(int id = 0){
		int texmapSize = 4;
		float offsetY = (id/texmapSize)/(float)texmapSize;
		float offsetX = (id%texmapSize)/(float)texmapSize;
		float texScale = 1/(float)texmapSize;
		uvs.Add(new Vector2(offsetX, offsetY));
		uvs.Add(new Vector2(offsetX+texScale, offsetY));
		uvs.Add(new Vector2(offsetX, offsetY+texScale));
		uvs.Add(new Vector2(offsetX+texScale, offsetY));
		uvs.Add(new Vector2(offsetX+texScale, offsetY+texScale));
		uvs.Add(new Vector2(offsetX, offsetY+texScale));
	}

	bool isNeighbor(int x, int y, int z, int orientation = 0){
		switch (orientation) {
			case 0:
				{
					if (y == chunkSize-1) return false;
					return cubeCoords[x + ((y+1)*chunkSize) + (z*chunkSize*chunkSize)];
				}
			case 1:
				{
					if (y == 0) return false;
					return cubeCoords[x + ((y-1)*chunkSize) + (z*chunkSize*chunkSize)];
				}
			case 2:
				{
					if (x == chunkSize-1) return false;
					return cubeCoords[x+1 + (y*chunkSize) + (z*chunkSize*chunkSize)];
				}
			case 3:
				{
					if (x == 0) return false;
					return cubeCoords[x-1 + (y*chunkSize) + (z*chunkSize*chunkSize)];
				}
			case 4:
				{
					if (z == chunkSize-1) return false;
					return cubeCoords[x + (y*chunkSize) + ((z+1)*chunkSize*chunkSize)];
				}
			case 5:
				{
					if (z == 0) return false;
					return cubeCoords[x + (y*chunkSize) + ((z-1)*chunkSize*chunkSize)];
				}
			default:
				return false;
			
		}
	}
	// Generates extra blocks outside the chunk borders to cut down on the amount of squares drawn.
	// Results in longer generation times but less load on the GPU after generation.
	bool isNeighborLod(int x, int y, int z, int orientation = 0){
		int lodChunkSize = chunkSize/2;
		switch (orientation) {
			case 0:
				{
					if (y == lodChunkSize-1){
						/* Vector3I realPos = new Vector3I(x, y, z) * 2 + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y + 2, realPos.Z);
						return neighbor != -1; */
						return false;
					}
					return cubeIds[x + ((y+1)*lodChunkSize) + (z*lodChunkSize*lodChunkSize)] != -1;
				}
			case 1:
				{
					if (y == 0){
						/* Vector3I realPos = new Vector3I(x, y, z) * 2 + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y - 2, realPos.Z);
						return neighbor != -1; */
						return false;
					}
					return cubeIds[x + ((y-1)*lodChunkSize) + (z*lodChunkSize*lodChunkSize)] != -1;
				}
			case 2:
				{
					if (x == lodChunkSize-1){
						/* Vector3I realPos = new Vector3I(x, y, z) * 2 + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X + 2, realPos.Y, realPos.Z);
						return neighbor != -1; */
						return false;
					}
					return cubeIds[x+1 + (y*lodChunkSize) + (z*lodChunkSize*lodChunkSize)] != -1;
				}
			case 3:
				{
					if (x == 0){
						/* Vector3I realPos = new Vector3I(x, y, z) * 2 + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X - 2, realPos.Y, realPos.Z);
						return neighbor != -1; */
						return false;
					}
					return cubeIds[x-1 + (y*lodChunkSize) + (z*lodChunkSize*lodChunkSize)] != -1;
				}
			case 4:
				{
					if (z == lodChunkSize-1){
						/* Vector3I realPos = new Vector3I(x, y, z) * 2 + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y, realPos.Z + 2);
						return neighbor != -1; */
						return false;
					}
					return cubeIds[x + (y*lodChunkSize) + ((z+1)*lodChunkSize*lodChunkSize)] != -1;
				}
			case 5:
				{
					if (z == 0){
						/* Vector3I realPos = new Vector3I(x, y, z) * 2 + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y, realPos.Z - 2);
						return neighbor != -1; */
						return false;
					}
					return cubeIds[x + (y*lodChunkSize) + ((z-1)*lodChunkSize*lodChunkSize)] != -1;
				}
			default:
				return false;
			
		}
	}
	bool isNeighborOptimized(int x, int y, int z, int orientation = 0){
		switch (orientation) {
			case 0:
				{
					if (y == chunkSize-1){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y + 1, realPos.Z);
						return neighbor != -1 && neighbor != 3;
					}
					return cubeCoords[x + ((y+1)*chunkSize) + (z*chunkSize*chunkSize)];
				}
			case 1:
				{
					if (y == 0){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y - 1, realPos.Z);
						return neighbor != -1 && neighbor != 3;
					}
					return cubeCoords[x + ((y-1)*chunkSize) + (z*chunkSize*chunkSize)];
				}
			case 2:
				{
					if (x == chunkSize-1){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X + 1, realPos.Y, realPos.Z);
						return neighbor != -1 && neighbor != 3;
					}
					return cubeCoords[x+1 + (y*chunkSize) + (z*chunkSize*chunkSize)];
				}
			case 3:
				{
					if (x == 0){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X - 1, realPos.Y, realPos.Z);
						return neighbor != -1 && neighbor != 3;
					}
					return cubeCoords[x-1 + (y*chunkSize) + (z*chunkSize*chunkSize)];
				}
			case 4:
				{
					if (z == chunkSize-1){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y, realPos.Z + 1);
						return neighbor != -1 && neighbor != 3;
					}
					return cubeCoords[x + (y*chunkSize) + ((z+1)*chunkSize*chunkSize)];
				}
			case 5:
				{
					if (z == 0){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y, realPos.Z - 1);
						return neighbor != -1 && neighbor != 3;
					}
					return cubeCoords[x + (y*chunkSize) + ((z-1)*chunkSize*chunkSize)];
				}
			default:
				return false;
			
		}
	}

	bool isWaterNeighbor(int x, int y, int z, int orientation = 0){
		// Culls faces on edges of chunks with more expensive calculations.
		// Only detects water.
		switch (orientation) {
			case 0:
				{
					if (y == chunkSize-1){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y + 1, realPos.Z);
						return neighbor != -1;
					}
					return cubeIds[x + ((y+1)*chunkSize) + (z*chunkSize*chunkSize)] != -1;
				}
			case 1:
				{
					if (y == 0){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y - 1, realPos.Z);
						return neighbor != -1;
					}
					return cubeIds[x + ((y-1)*chunkSize) + (z*chunkSize*chunkSize)] != -1;
				}
			case 2:
				{
					if (x == chunkSize-1){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X + 1, realPos.Y, realPos.Z);
						return neighbor != -1;
					}
					return cubeIds[x+1 + (y*chunkSize) + (z*chunkSize*chunkSize)] != -1;
				}
			case 3:
				{
					if (x == 0){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X - 1, realPos.Y, realPos.Z);
						return neighbor != -1;
					}
					return cubeIds[x-1 + (y*chunkSize) + (z*chunkSize*chunkSize)] != -1;
				}
			case 4:
				{
					if (z == chunkSize-1){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y, realPos.Z + 1);
						return neighbor != -1;
					}
					return cubeIds[x + (y*chunkSize) + ((z+1)*chunkSize*chunkSize)] != -1;
				}
			case 5:
				{
					if (z == 0){
						Vector3I realPos = new Vector3I(x, y, z) + chunkPosition * chunkSize;
						int neighbor = Generate(realPos.X, realPos.Y, realPos.Z - 1);
						return neighbor != -1;
					}
					return cubeIds[x + (y*chunkSize) + ((z-1)*chunkSize*chunkSize)] != -1;
				}
			default:
				return false;
			
		}
	}
	void createWaterSquare(int x, int y, int z, int id, int orientation){
		Vector3 normalVec;
		switch (orientation){
			case 0: // up
				{
					normalVec = Vector3.Up;
					waterVertices.Add(new Vector3(x, y+1, z));
					waterVertices.Add(new Vector3(x+1, y+1, z));
					waterVertices.Add(new Vector3(x, y+1, z+1));
					waterVertices.Add(new Vector3(x+1, y+1, z));
					waterVertices.Add(new Vector3(x+1, y+1, z+1));
					waterVertices.Add(new Vector3(x, y+1, z+1));
					break;
				}
			case 1: // down
				{
					normalVec = Vector3.Down;
					waterVertices.Add(new Vector3(x, y, z));
					waterVertices.Add(new Vector3(x, y, z+1));
					waterVertices.Add(new Vector3(x+1, y, z));
					waterVertices.Add(new Vector3(x, y, z+1));
					waterVertices.Add(new Vector3(x+1, y, z+1));
					waterVertices.Add(new Vector3(x+1, y, z));
					break;
				}
			case 2: // x+
				{
					normalVec = Vector3.Right;
					waterVertices.Add(new Vector3(x+1, y+1, z+1));
					waterVertices.Add(new Vector3(x+1, y+1, z));
					waterVertices.Add(new Vector3(x+1, y, z+1));
					waterVertices.Add(new Vector3(x+1, y+1, z));
					waterVertices.Add(new Vector3(x+1, y, z));
					waterVertices.Add(new Vector3(x+1, y, z+1));
					break;
				}
			case 3: // x-
				{
					normalVec = Vector3.Left;
					waterVertices.Add(new Vector3(x, y+1, z));
					waterVertices.Add(new Vector3(x, y+1, z+1));
					waterVertices.Add(new Vector3(x, y, z));
					waterVertices.Add(new Vector3(x, y+1, z+1));
					waterVertices.Add(new Vector3(x, y, z+1));
					waterVertices.Add(new Vector3(x, y, z));
					break;
				}
			case 4: // z+
				{
					normalVec = Vector3.Back;
					waterVertices.Add(new Vector3(x, y+1, z+1));
					waterVertices.Add(new Vector3(x+1, y+1, z+1));
					waterVertices.Add(new Vector3(x, y, z+1));
					waterVertices.Add(new Vector3(x+1, y+1, z+1));
					waterVertices.Add(new Vector3(x+1, y, z+1));
					waterVertices.Add(new Vector3(x, y, z+1));
					break;
				}
			case 5: // z-
			default:
				{
					normalVec = Vector3.Forward;
					waterVertices.Add(new Vector3(x+1, y+1, z));
					waterVertices.Add(new Vector3(x, y+1, z));
					waterVertices.Add(new Vector3(x+1, y, z));
					waterVertices.Add(new Vector3(x, y+1, z));
					waterVertices.Add(new Vector3(x, y, z));
					waterVertices.Add(new Vector3(x+1, y, z));
					break;
				}
		}
		for (int i = 0; i < 6; i++){
			waterNormals.Add(normalVec);
		}
		int texmapSize = 4;
		float offsetY = (id/texmapSize)/(float)texmapSize;
		float offsetX = (id%texmapSize)/(float)texmapSize;
		float texScale = 1/(float)texmapSize;
		waterUvs.Add(new Vector2(offsetX, offsetY));
		waterUvs.Add(new Vector2(offsetX+texScale, offsetY));
		waterUvs.Add(new Vector2(offsetX, offsetY+texScale));
		waterUvs.Add(new Vector2(offsetX+texScale, offsetY));
		waterUvs.Add(new Vector2(offsetX+texScale, offsetY+texScale));
		waterUvs.Add(new Vector2(offsetX, offsetY+texScale));
	}
}

