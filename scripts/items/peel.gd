class_name Peel
extends Node3D
## 바나나 껍질: 밟은 사람(적·아군·나)은 미끄러져 1.2초 넘어진다. 버린 사람은 0.8초 동안 안전.

const ATLAS := "res://assets/sprites/loot.png"

var owner_f: Fighter
var t := 0.0
var life := 40.0


static func drop(parent: Node, at: Vector3, owner_: Fighter, terrain: Terrain) -> Peel:
	var p := Peel.new()
	p.owner_f = owner_
	parent.add_child(p)
	at.y = terrain.height_at(at) if terrain else at.y
	p.global_position = at
	return p


func _ready() -> void:
	var m := BB.make(ATLAS, "peel", {"unique": true, "up_normal": 1.0, "receive_shade": false, "emission": 0.15})
	add_child(m)


func _physics_process(dt: float) -> void:
	t += dt
	if t > life:
		queue_free()
		return
	for f in Combat.in_radius(get_tree(), global_position, 0.25, 0.8):
		var fi := f as Fighter
		if fi == owner_f and t < 0.8:
			continue
		if fi.stun_t > 0.0:
			continue
		# 미끄러짐: 가던 방향으로 쭉 + 넘어짐
		var v := Vector3(fi.velocity.x, 0, fi.velocity.z)
		if v.length() < 0.5:
			v = Vector3(randf_range(-1, 1), 0, randf_range(-1, 1))
		fi.kv += v.normalized() * 7.0 + Vector3(0, 4.0, 0)
		fi.stun_t = 1.2
		fi.flash = 0.1
		Audio.play("troll", global_position)
		Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 14.0)
		Fx.pop(get_parent(), fi.center() + Vector3(0, 0.7, 0), ["spark0", "spark1", "spark2"], 10.0)
		if fi is Player and (fi as Player).local:
			Achievements.add(get_tree(), "slips")
			get_tree().call_group("hud", "toast_text", "미끄덩!", Color("#ffe060"))
		queue_free()
		return
