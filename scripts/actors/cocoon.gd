class_name Cocoon
extends Fighter
## 고치: hatch_t 초 뒤 거미가 나온다. 그 전에 부수면 아무것도 안 나온다 (딜과 정리 사이의 판단 — BossCocoon)

var hatch_t := 4.0
var mesh: MeshInstance3D
var _t := 0.0


func _ready() -> void:
	team = 1
	base_max_hp = 26.0
	hp = base_max_hp
	body_r = 0.4
	body_h = 1.3
	collision_layer = 0
	mesh = BB.make(BossBits.ATLAS, "cocoon0", {"unique": true, "up_normal": 0.8})
	spr_mat = mesh.material_override
	add_child(mesh)
	add_child(BB.blob(0.45, 0.22, 0.8))
	HpBar.attach(self, 12)
	mesh.scale = Vector3(1, 0.1, 1)
	create_tween().tween_property(mesh, "scale", Vector3.ONE, 0.3).set_trans(Tween.TRANS_BACK)
	Audio.play("q_cocoon", global_position)


func _physics_process(dt: float) -> void:
	tick_status(dt)
	if dead:
		return
	_t += dt
	var k := _t / hatch_t
	var fr := int(_t * (3.0 + k * 9.0)) % 4
	spr_mat.set_shader_parameter("region", BB.region_uv(BossBits.ATLAS, "cocoon%d" % fr))
	if _t >= hatch_t:
		Audio.play("q_hatch", global_position)
		var e := Enemy.spawn(get_parent(), "spider", global_position + Vector3(0, 0.2, 0), terrain, rig)
		get_tree().call_group("party", "on_spawn", e)
		Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 12.0)
		queue_free()


func _die() -> void:
	super._die()
	Audio.play("crate_break", global_position)
	Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 12.0)
	queue_free()
