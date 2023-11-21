using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

public partial class Chunks : StaticBody3D
{

	public bool lazy = true;
	public int chunkSize = 16;

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
	public FastNoiseLite bigNoise;
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

	private Node Reserved;

	List<Vector3I> trees = new();
	List<Vector3I> rocks = new();
	public async void generateChunk(int xChunk, int yChunk, int zChunk, int chunkSize, bool lazy){
		Reserved = GetNode("/root/Reserved");
		Position = new Vector3(xChunk, yChunk, zChunk) * chunkSize;
		chunkPosition = new Vector3I(xChunk, yChunk, zChunk);
		cubeCoords = new(chunkSize*chunkSize*chunkSize);
		cubes = new(chunkSize*chunkSize*chunkSize);
		cubeIds = new(chunkSize*chunkSize*chunkSize);
		this.lazy = lazy;
		this.chunkSize = chunkSize;

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
				if (lazy){
					await ToSignal(GetTree(), "process_frame");
				}
				if (y == chunkSize){
					y = 0;
					z += 1;
					
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

			MeshInstance3D chunkMesh = (MeshInstance3D)GetNode("ChunkMesh");
			CollisionShape3D chunkCollision = (CollisionShape3D)GetNode("ChunkCollision");
			ArrayMesh arrMesh = new ArrayMesh();

			if (arrMesh != null){
				arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshData);
			}
			chunkMesh.Mesh = arrMesh;
			// chunkMesh.MaterialOverride = material;
			var collision = chunkMesh.Mesh.CreateTrimeshShape();
			chunkCollision.Shape = collision;
		}
		if (waterVertices.Count != 0){
			// water time

			waterMeshData[(int)Mesh.ArrayType.Vertex] = waterVertices.ToArray();
			waterMeshData[(int)Mesh.ArrayType.Normal] = waterNormals.ToArray();
			waterMeshData[(int)Mesh.ArrayType.TexUV] = waterUvs.ToArray();

			MeshInstance3D waterMesh = (MeshInstance3D)GetNode("WaterMesh");
			ArrayMesh waterArrMesh = new ArrayMesh();
			if (waterArrMesh != null) {
				waterArrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, waterMeshData);
			}
			waterMesh.Mesh = waterArrMesh;
		}

		TreePass();
	}
	
	
	/* int Generate(int x, int y, int z){
		float sample = smallNoise.GetNoise3D(x, y, z) * 2 + bigNoise.GetNoise2D(x, z) * 1;
		float waterSample = waterNoise.GetNoise2D(x, z);
		double modSample = Math.Min((sample+1)*16, (waterSample+0.2) * 100);
		if (modSample < y){
			if (y < 4){
				return 3;
			}	
			return -1;
		} else if (modSample < y + 1){
			if (biomeNoise.GetNoise2D(x, z) < 0){
				return 4;
			}
			return 0;
		} else if (modSample < y + 2){
			return 2;
		}
		return 1;
	} */

	// Get the block at some xyz position.
	int Generate(int x, int y, int z){
		
		float modY = (y / 5)*5 - 4;
		float modY2 = (y / 2)*2 - 4;
		float biomeSample1 = biomeNoise.GetNoise3D(x, modY, z);
		float biomeSample2 = biomeNoise2.GetNoise3D(x, modY2, z);
		float hilliness = biomeNoise2.GetNoise2D(x, z);
		double modSample = smallNoise.GetNoise2D(x, z) * 5;
		modSample += biomeSample1 * 30;
		modSample += biomeSample2 * 30;
		modSample -= modY;
		float caveSample = caveNoise.GetNoise3D(x, y, z);
		modSample += caveSample;
		if (modSample > 0){
			if (y + (hilliness * 10) < 10){
				if (y < 4){
					return 4; // stone
				}
				return 0; // grass
			}
			return 2;
		}
		if (y < 4){
			return 3;
		}
		return -1; // air
		/* float modY = (y / 4)*4;
		double modSample = smallNoise.GetNoise3D(x, y, z);
		bool empty = false;
		if (modSample < modY/32-1){
			empty = true;
			if (smallNoise.GetNoise3D(x, modY-1, z+1) > modY/32-1){
				empty = false;
			}
			else if (smallNoise.GetNoise3D(x, modY-1, z-1) > modY/32-1){
				empty = false;
			}
			else if (smallNoise.GetNoise3D(x+1, modY-1, z) > modY/32-1){
				empty = false;
			}
			else if (smallNoise.GetNoise3D(x-1, modY-1, z) > modY/32-1){
				empty = false;
			}
		}
		if (empty){
			if (y < 4){
				return 3;
			}	
			return -1;
		}
		return 0; */
	}
	

	void TreePass(){
		PackedScene treeScene = GD.Load<PackedScene>("res://tree.tscn");
		PackedScene rockScene = GD.Load<PackedScene>("res://rock.tscn");
		for (int i = 0; i < trees.Count; i++){
			Vector3I tree = trees[i];
			Vector3I realPos = tree + chunkPosition*chunkSize;
			float biomeSample1 = biomeNoise.GetNoise3D(realPos.X, realPos.Y, realPos.Z);
			// tree frequency is 1 in every 100 grass blocks
			// int treeThreshold = (int)(treeNoise.GetNoise2D(tree.X, tree.Z) * 200 + 30);
			int treeThreshold = 0;
			if (treeNoise.GetNoise2D(realPos.X, realPos.Z) > 0){
				treeThreshold = 50;
			}
			if (Math.Abs(biomeSample1.GetHashCode()) % 100 <= treeThreshold){
				// try to spawn tree at position
				// check if there is space to spawn a tree
				if (isSpaceClear(new Vector3I(-1, 0, -1)+tree, new Vector3I(1, 2, 1)+tree)){
					// spawn in tree
					StaticBody3D instance = (StaticBody3D)treeScene.Instantiate();
					AddChild(instance);
					instance.Position = new Vector3(tree.X, tree.Y, tree.Z);
				}
			}
		}
		for (int i = 0; i < rocks.Count; i++){
			Vector3I rock = rocks[i];
			Vector3I realPos = rock + chunkPosition*chunkSize;
			float biomeSample1 = biomeNoise.GetNoise3D(realPos.X, realPos.Y, realPos.Z);
			if (Math.Abs(biomeSample1.GetHashCode()) % 100 <= 2){
				if (isSpaceClear(new Vector3I(0, 0, 0)+rock, new Vector3I(0, 0, 0)+rock)){
					// spawn in rock
					StaticBody3D instance = (StaticBody3D)rockScene.Instantiate();
					AddChild(instance);
					instance.Position = new Vector3(rock.X, rock.Y, rock.Z);
				}
			}
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


	void createCube(int x, int y, int z, int id, bool cullInteriors=true){
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
			if (cullInteriors && isNeighbor(x, y, z, i)){
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
					if (id == 0){ // hacky way of defining tree candidate positions
						trees.Add(new Vector3I(x, y+1, z));
					} else if (id == 2){
						rocks.Add(new Vector3I(x, y+1, z));
					}
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

