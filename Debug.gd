extends Label

@onready var chunk_generator = Reserved.chunk_generator

func _process(delta):
	print("printing text")
	var shown_text = "DEBUG :3\n"
	shown_text += "FPS: %s\n" % Engine.get_frames_per_second()
	shown_text += "Chunks not yet generated: %s\n" % chunk_generator.GetChunksToGenerate()
	shown_text += "Chunks currently loaded: %s\n" % chunk_generator.GetChunksLoaded()
	print("got text!")
	text = shown_text
