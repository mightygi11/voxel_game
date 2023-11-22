extends StaticBody3D

var possible_items = [
	"Sword",
	"Bow",
	"Shield",
	"Potion",
	"Magic Sword"
]


var opening = false
	
func interact(player):
	if opening:
		return
	opening = true
	var curr_chunk = get_parent().name
	if Reserved.modified_items.has(curr_chunk):
		Reserved.modified_items[curr_chunk].append(Vector3i(position))
	else:
		Reserved.modified_items[curr_chunk] = [Vector3i(position)]
	print("Got interacted with by %s" % player)
	var item = possible_items.pick_random()
	print("Giving the player a %s" % item)
	$AnimationPlayer.play("open")
