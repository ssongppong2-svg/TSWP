class_name WebField
extends Node3D
## 바닥 거미줄: 위에 있으면 느려지고 조금씩 아프다. 6초 뒤 사라진다

var r := 2.8
var life := 6.0
var owner_f: Fighter
var tick := 0.0
var mesh: MeshInstance3D


func _ready() -> void:
	var q := QuadMesh.new()
	q.size = Vector2(r * 2, r * 2)
	q.orientation = PlaneMesh.FACE_Y
	mesh = MeshInstance3D.new()
	mesh.mesh = q
	var m := ShaderMaterial.new()
	m.shader = load("res://shaders/fx_flat.gdshader")
	m.set_shader_parameter("tex", BB.atlas(BossBits.ATLAS)["tex"])
	m.set_shader_parameter("region", BB.region_uv(BossBits.ATLAS, "web_field"))
	mesh.material_override = m
	mesh.position.y = 0.03
	add_child(mesh)
	mesh.scale = Vector3(0.2, 1, 0.2)
	create_tween().tween_property(mesh, "scale", Vector3.ONE, 0.25).set_trans(Tween.TRANS_BACK)
	Audio.play("web_hit", global_position)


func _physics_process(dt: float) -> void:
	life -= dt
	tick -= dt
	if life <= 0.0:
		queue_free()
		return
	if tick <= 0.0:
		tick = 0.5
		for f in Combat.in_radius(get_tree(), global_position, r, 1.0):
			var fi := f as Fighter
			if fi.team == 0:
				var h := Hit.make(2.0, owner_f, Vector3.ZERO, "web")
				h.slow = 0.55
				fi.take(h)
