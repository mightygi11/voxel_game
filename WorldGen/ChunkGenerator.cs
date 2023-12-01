using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class ChunkGenerator : Node3D
{

	[Signal]
	public delegate void ChunkMovedEventHandler(Vector3I playerChunkPos);
	[Export]
	public Node player;
	[Export]
	public int renderDistance = 4;
	PriorityQueue<Vector3I, int> chunksToGenerate = new();
	Dictionary<Vector3I, Chunk> chunksLoaded = new();
	Queue<Vector3I> chunksToRemove = new();
	bool currentlyGenerating = false;
	Vector3I currentPlayerChunk = Vector3I.Zero;
	PackedScene chunkScene;
	public override void _Ready()
	{
		chunkScene = GD.Load<PackedScene>("res://chunk.tscn");
	}

	public override void _Process(double delta)
	{
		// GD.Print("ChunkQueue size: " + chunksToGenerate.Count);
		if ((Vector3I)player.Get("current_chunk") != currentPlayerChunk){
			currentPlayerChunk = (Vector3I)player.Get("current_chunk");
			PlayerChunkUpdate(currentPlayerChunk);
			// GD.Print("Updating player chunk...");
		}
		for (int i = 0; i < 2; i++){
			if (chunksToGenerate.Count != 0){
				Vector3I currChunk = chunksToGenerate.Dequeue();
				while (chunksLoaded.ContainsKey(currChunk) && chunksToGenerate.Count != 0){
					currChunk = chunksToGenerate.Dequeue();
				}
				if (!chunksLoaded.ContainsKey(currChunk)){
					currentlyGenerating = true;
					GenerateChunk(currChunk);
					currentlyGenerating = false;
				}
			}
		}
		if (chunksToRemove.Count != 0){
			RemoveChunk(chunksToRemove.Dequeue());
		}
	}

	void UpdateChunkLists(Vector3I playerChunkPos){
		for (int x = -renderDistance; x <= renderDistance; x++){
			// for (int y = -renderDistance; y <= renderDistance; y++){
			for (int y = -2; y <= 2; y++){
				for (int z = -renderDistance; z <= renderDistance; z++){
					Vector3I chunkPos = playerChunkPos + new Vector3I(x, y, z);
					int chunkDistance =
						Math.Abs(chunkPos.X - playerChunkPos.X) +
						// Math.Abs(chunkPos.Y - playerChunkPos.Y) +
						Math.Abs(chunkPos.Z - playerChunkPos.Z);
					if (chunkDistance <= renderDistance){
						if (!chunksLoaded.ContainsKey(chunkPos)){
							chunksToGenerate.Enqueue(chunkPos, chunkDistance);
						}
					}
				}
			}
		}
		foreach (Vector3I chunkPos in chunksLoaded.Keys){
			int chunkDistance =
				Math.Abs(chunkPos.X - playerChunkPos.X) +
				// Math.Abs(chunkPos.Y - playerChunkPos.Y) +
				Math.Abs(chunkPos.Z - playerChunkPos.Z);
			if (chunkDistance > renderDistance+4){
				chunksLoaded.TryGetValue(chunkPos, out Chunk chunk);
				chunk.QueueFree();
				chunksLoaded.Remove(chunkPos);
				// chunksToRemove.Enqueue(chunkPos);
			}
		}
	}

	async void GenerateChunk(Vector3I chunkPos){
		// check that the chunk being generated isn't already loaded in
		if (chunksLoaded.ContainsKey(chunkPos)){
			return;
		}
		// var chunkScene2 = GD.Load<PackedScene>("res://chunk.tscn");
		var chunk = chunkScene.Instantiate<Chunk>();
		
		// GD.Print(chunkPos);
		chunksLoaded.Add(chunkPos, chunk);
		// chunk.Generate(chunkPos); // non-threaded
		await Task.Run(() => chunk.Generate(chunkPos)); // threaded
		if (IsInstanceValid(chunk)){
			AddChild(chunk);
		}
	}

	void RemoveChunk(Vector3I chunkPos){
		if (!chunksLoaded.ContainsKey(chunkPos)){
			return;
		}
		chunksLoaded.TryGetValue(chunkPos, out Chunk chunk);
		chunk.QueueFree();
		chunksLoaded.Remove(chunkPos);
	}

	public void PlayerChunkUpdate(Vector3I playerChunkPos)
	{
		GD.Print("Updating chunks..");
		UpdateChunkLists(playerChunkPos);
	}

	public int GetChunksToGenerate(){
		return chunksToGenerate.Count;
	}
	public int GetChunksLoaded(){
		return chunksLoaded.Count;
	}
}






