class_name Telegraph
extends MeshInstance3D
## 공격 예고 장판: t 초 동안 안쪽이 차오르다 끝나면 사라진다 (피할 시간을 보여 준다 — 보스 시스템.md "공정하지만 치명적")

var t := 0.0
var dur := 1.0
var mat: ShaderMaterial


static func circle(parent: Node, at: Vector3, r: float, dur_: float, col := Color(1, 0.25, 0.2)) -> Telegraph:
	return _make(parent, at, Vector2(r * 2, r * 2), 0, 0.0, dur_, col)


static func rect(parent: Node, at: Vector3, length: float, width: float, yaw: float, dur_: float, col := Color(1, 0.25, 0.2)) -> Telegraph:
	return _make(parent, at, Vector2(length, width), 1, yaw, dur_, col)


static func _make(parent: Node, at: Vector3, sz: Vector2, shape: int, yaw: float, dur_: float, col: Color) -> Telegraph:
	var w := Telegraph.new()
	var q := QuadMesh.new()
	q.size = sz
	q.orientation = PlaneMesh.FACE_Y
	w.mesh = q
	w.mat = ShaderMaterial.new()
	w.mat.shader = load("res://shaders/warn.gdshader")
	w.mat.set_shader_parameter("size_px", sz * 16.0)
	w.mat.set_shader_parameter("shape", shape)
	w.mat.set_shader_parameter("col", col)
	w.material_override = w.mat
	w.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	w.dur = dur_
	parent.add_child(w)
	w.global_position = at + Vector3(0, 0.04, 0)
	w.rotation.y = -yaw
	return w


func _process(dt: float) -> void:
	t += dt
	mat.set_shader_parameter("fill", clampf(t / dur, 0.0, 1.0))
	if t >= dur:
		queue_free()
