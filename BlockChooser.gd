extends Node

@export var noise: Noise
@export var height_noise: Noise
@export var water_noise: Noise
# Called when the node enters the scene tree for the first time.
func generate(x, y, z) -> int:
	var sample := noise.get_noise_3d(x, y, z) + height_noise.get_noise_2d(x, z) * 2
	var water_sample := water_noise.get_noise_2d(x, z)
	var mod_sample = min((sample+0.5)*12, (water_sample+0.2) * 100)
	if mod_sample < y:
		if y < 4:
			return 3
		return -1
	elif mod_sample < y + 1:
		return 0
	elif mod_sample < y + 2:
		return 2
	else:
		return 1
	
