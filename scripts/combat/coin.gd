class_name Coin
extends Node3D
## 금화: 적이 쓰러지면 톡톡 튀어나와 잠깐 뒤 가까운 플레이어에게 빨려 들어간다 (주스: 보상이 눈에 보이게)

var vel := Vector3.ZERO
var t := 0.0
var mesh: MeshInstance3D
var terrain: Terrain
var target: Node3D


static func burst(parent: Node, at: Vector3, n: int, terrain_: Terrain) -> void:
	for i in n:
		var c := Coin.new()
		c.terrain = terrain_
		c.vel = Vector3(randf_range(-2.5, 2.5), randf_range(5.0, 7.5), randf_range(-2.0, 2.0))
		parent.add_child(c)
		c.global_position = at + Vector3(0, 0.4, 0)


func _ready() -> void:
	mesh = BB.make(Fx.ATLAS, "coin0", {"unique": true, "center": true, "emission": 0.3, "receive_shade": false, "up_normal": 0.9})
	add_child(mesh)
	add_child(BB.blob(0.15, 0.08, 0.7))


func _physics_process(dt: float) -> void:
	t += dt
	(mesh.material_override as ShaderMaterial).set_shader_parameter("region", BB.region_uv(Fx.ATLAS, "coin%d" % (int(t * 12.0) % 4)))
	if t < 0.5:
		vel.y -= 22.0 * dt
		var p := global_position + vel * dt
		var g := terrain.height_at(p) + 0.15 if terrain else 0.15
		if p.y < g:
			p.y = g
			vel.y = absf(vel.y) * 0.45
			vel.x *= 0.6
			vel.z *= 0.6
		global_position = p
		return
	if target == null:
		var bd := 1e9
		for f in get_tree().get_nodes_in_group("fighters"):
			if f is Player and not (f as Player).dead:
				var d := (f as Node3D).global_position.distance_to(global_position)
				if d < bd:
					bd = d
					target = f
		if target == null:
			return
	# 빨려 들어가기 (점점 빨라짐)
	var to := target.global_position + Vector3(0, 0.8, 0) - global_position
	var spd := 6.0 + t * 14.0
	if to.length() < 0.35:
		Audio.play("coin", global_position)
		var party := get_tree().get_first_node_in_group("party")
		if party:
			party.add_gold(1)
		queue_free()
		return
	global_position += to.normalized() * minf(spd * dt, to.length())
