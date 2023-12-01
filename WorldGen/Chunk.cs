using Godot;
using System;
using System.Collections.Generic;

public partial class Chunk : StaticBody3D
{
	static int BLOCKS_DIMENSIONS = 16;
	static int texmapSize = 4;
	[Export]
	public FastNoiseLite biomeNoise;
	[Export]
	public FastNoiseLite biomeNoise2;
	[Export]
	public FastNoiseLite smallNoise;
	[Export]
	public FastNoiseLite waterNoise;
	[Export]
	public FastNoiseLite temperatureNoise;
	[Export]
	public FastNoiseLite caveNoise;
	bool lazy = false;

	bool generated = false;
	int blockGenLevel = 0;

	int chunkSize;
	int chunkRes;
	public Vector3I chunkPos;

	byte[,,] blocks;
	
	List<Vector3> vertices = new List<Vector3>();
	List<Vector3> normals = new List<Vector3>();
	List<Vector2> uvs = new List<Vector2>();

	/*
	chunkX/Y/Z: the chunk coordinates of a chunk. Like block coordinates but scaled up x16.
	chunkRes: the "resolution" of a chunk. 1 is 8x more detailed than 2. For LODs.
	chunkSize: the height, width, & length of a single chunk in block coordinates.
	*/

	public void ThreadGeneratedChunk(object state){
		Generate(chunkPos);
	}
	public void GenerateChunkLod(int xChunk, int yChunk, int zChunk, int chunkSize, bool lazy){
		if (chunkRes != 2){
			generated = true;
			Generate(new Vector3I(xChunk, yChunk, zChunk), 2, chunkSize);
			generated = false;
		}
		
	}
	public void GenerateChunk(int xChunk, int yChunk, int zChunk, int chunkSize, bool lazy){
		if (chunkRes != 1){
			generated = true;
			Generate(new Vector3I(xChunk, yChunk, zChunk), 1, chunkSize);
			generated = false;
		}
	}

	/// <summary>
	/// Calculate blocks and construct the mesh for the given chunk.
	/// </summary>
	/// <param name="chunkPos"> The position of the chunk, in chunk space.</param>
	/// <param name="chunkRes"> For LoDs, how many blocks to draw per individual block.</param>
	/// <param name="chunkSize"> The height, width, and length of the chunk in cubic meters/blocks. </param>
	public void Generate(Vector3I chunkPos, int chunkRes=1, int chunkSize=16){
		// initialize arrays, lists, etc
		blocks = new byte[BLOCKS_DIMENSIONS,BLOCKS_DIMENSIONS,BLOCKS_DIMENSIONS];
		vertices.Clear();
		normals.Clear();
		uvs.Clear();
		
		this.chunkSize = chunkSize;
		this.chunkRes = chunkRes;
		this.chunkPos = chunkPos;

		var meshData = new Godot.Collections.Array();
		meshData.Resize((int)Mesh.ArrayType.Max);

		MeshInstance3D chunkMesh = (MeshInstance3D)GetNode("ChunkMesh");
		CollisionShape3D chunkCollision = (CollisionShape3D)GetNode("ChunkCollision");
		
		Position = chunkPos * chunkSize;
		
		// first, determine what all the blocks in the chunk are gonna be
		// if previously determined don't bother
		if (blockGenLevel == 0 || blockGenLevel > chunkRes){
			CalculateBlocks(chunkPos.X, chunkPos.Y, chunkPos.Z, chunkRes, chunkSize);
			blockGenLevel = chunkRes;
		}
		

		// next, actually calculate the mesh stuff for the chunk
		CalculateMesh(chunkRes);

		// build the mesh
		if (vertices.Count != 0){
			meshData[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
			meshData[(int)Mesh.ArrayType.Normal] = normals.ToArray();
			meshData[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
			
			
			ArrayMesh arrMesh = new ArrayMesh();

			if (arrMesh != null){
				arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshData);
			}
			chunkMesh.Mesh = arrMesh;
			// chunkMesh.MaterialOverride = material;
			var collision = chunkMesh.Mesh.CreateTrimeshShape();
			// chunkMesh.Visible = true;
			chunkCollision.Shape = collision;
		}
	}

	/// <summary>
	/// Calculate the blocks to be used in the chunk.
	/// </summary>
	/// <param name="chunkX"> X position of chunk in chunk space. </param>
	/// <param name="chunkY"> Y position of chunk in chunk space. </param>
	/// <param name="chunkZ"> Z position of chunk in chunk space. </param>
	/// <param name="chunkRes"> The number of blocks to calculate per cubic meter. </param>
	/// <param name="chunkSize"> The size of the chunk in blocks/cubic meters. </param>
	void CalculateBlocks(int chunkX, int chunkY, int chunkZ, int chunkRes=1, int chunkSize=16){
		for (int x = 0; x < chunkSize; x += chunkRes){
			for (int y = 0; y < chunkSize; y += chunkRes){
				for (int z = 0; z < chunkSize; z += chunkRes){
					int realX = chunkX*chunkSize + x;
					int realY = chunkY*chunkSize + y;
					int realZ = chunkZ*chunkSize + z;
					float temperatureSample = temperatureNoise.GetNoise2D(realX, realZ) * 25;
					float hilliness = waterNoise.GetNoise2D(realX, realZ);
					float modY = (realY / 5)*5 - 4;
					float modY2 = (realY / 2)*2 - 4;
					float biomeSample1 = biomeNoise.GetNoise3D(realX, modY, realZ);
					float biomeSample2 = biomeNoise2.GetNoise3D(realX, modY2, realZ);
					double modSample = smallNoise.GetNoise2D(realX, realZ) * 5;
					modSample += biomeSample1 * 60 + 10;
					modSample += biomeSample2 * 60;
					temperatureSample -= realY + (hilliness * 2);
					modSample -= modY;
					float caveSample = caveNoise.GetNoise3D(realX, realY, realZ);
					modSample += caveSample * 10;
					if (modSample > 0){
						if (realY < 4){
							if (temperatureSample > -9){
								blocks[x,y,z] = 5; // sand
							} else {
								blocks[x,y,z] = 3; // stone
							}
						}
						else if (temperatureSample - realY > -6){
							blocks[x,y,z] = 5; // sand
						} else if (temperatureSample > -33) {
							if (realY + hilliness * 3 < 14){
								if (temperatureSample < -10 || temperatureSample > -2){
									blocks[x,y,z] = 1;
								} else {
									blocks[x,y,z] = 2;
								}
							} else {
								blocks[x,y,z] = 3; // stone
							}
						} else {
							blocks[x,y,z] = 6; // snow
						}
						
					} else {
						if (realY < 4){
							if (temperatureSample < -10){
								blocks[x,y,z] = 7; // ice
							} else {
								blocks[x,y,z] = 4; // water
							}
						} else {
							blocks[x,y,z] = 0; // air
						}
					}
					
				}
			}
		}
	}
	
	void CalculateBlocksTest(int chunkX, int chunkY, int chunkZ, int chunkRes=1, int chunkSize=16){
		for (int x = 0; x < chunkSize; x += chunkRes){
			for (int y = 0; y < chunkSize; y += chunkRes){
				for (int z = 0; z < chunkSize; z += chunkRes){
					int realX = chunkX*chunkSize + x;
					int realY = chunkY*chunkSize + y;
					int realZ = chunkZ*chunkSize + z;
					float sample = smallNoise.GetNoise2D(realX, realZ);
					sample *= 5;
					sample -= realY;
					if (sample > 0){ // is there a block here or not
						blocks[x,y,z] = 1;
					} else {
						blocks[x,y,z] = 0;
					}
				}
			}
		}
	}

	/// <summary>
	/// Calculate all the vertices, normals, and UVs for the mesh, and
	/// store them in their respective class variables.
	/// </summary>
	/// <param name="chunkRes"> The number of cubes to draw per cubic meter. </param>
	void CalculateMesh(int chunkRes){
		for (int x = 0; x < chunkSize; x += chunkRes){
			for (int y = 0; y < chunkSize; y += chunkRes){
				for (int z = 0; z < chunkSize; z += chunkRes){
					if (blocks[x,y,z] == 0){
						continue;
					}
					// Vector3I cube = cubes[i];
					int id = blocks[x,y,z];
					createCube(x, y, z, id);
				}
			}
		}
	}

	/// <summary>
	/// Creates up to 6 square meshes in a cube formation.
	/// </summary>
	/// <param name="x"> The local x position of a block. </param>
	/// <param name="y"> The local y position of a block. </param>
	/// <param name="z"> The local z position of a block. </param>
	/// <param name="id"> The block type - 0 is air, 1 is grass, etc.</param>
	void createCube(int x, int y, int z, int id){
		for (int i = 0; i < 6; i++){
			if (!isNeighbor(x, y, z, i)){
				createSquare(x, y, z, id, i);
			}
		}
	}

	/// <summary>
	/// Checks if any given block has a neighboring solid block in any of 6 directions.
	/// Returns false on the edges of chunks.
	/// </summary>
	/// <param name="x"> The local x position of a block. </param>
	/// <param name="y"> The local y position of a block. </param>
	/// <param name="z"> The local z position of a block. </param>
	/// <param name="orientation"> The direction to check for a neighbor in.
	/// 0 = up, 1 = down, 2 = +x, 3 = -x, 4 = +z, 5 = -z </param>
	/// <returns></returns>
	bool isNeighbor(int x, int y, int z, int orientation = 0){
		switch (orientation) {
			case 0:
				{
					if (y == chunkSize-1) return false;
					return blocks[x,y+1,z] != 0;
				}
			case 1:
				{
					if (y == 0) return false;
					return blocks[x,y-1,z] != 0;
				}
			case 2:
				{
					if (x == chunkSize-1) return false;
					return blocks[x+1,y,z] != 0;
				}
			case 3:
				{
					if (x == 0) return false;
					return blocks[x-1,y,z] != 0;
				}
			case 4:
				{
					if (z == chunkSize-1) return false;
					return blocks[x,y,z+1] != 0;
				}
			case 5:
				{
					if (z == 0) return false;
					return blocks[x,y,z-1] != 0;
				}
			default:
				return false;
			
		}
	}

	/// <summary>
	/// Draws a square with 1 of 6 orientations at some local position.
	/// </summary>
	/// <param name="x"> The local x position of a block. </param>
	/// <param name="y"> The local y position of a block. </param>
	/// <param name="z"> The local z position of a block. </param>
	/// <param name="id"> The block type. 1 is grass, 2 is dirt, etc. </param>
	/// <param name="orientation"> The direction to draw the square facing. </param>
	void createSquare(int x, int y, int z, int id, int orientation){
		Vector3 normalVec;
		if (id == 0) return; // failsafe. don't draw air squares
		switch (orientation){
			case 0: // up
				{
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
		id -= 1;
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

}
