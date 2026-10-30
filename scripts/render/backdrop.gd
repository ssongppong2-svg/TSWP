class_name Backdrop
extends Node3D
## 원경: 지도 북쪽 뒤에 하늘 + 능선 3겹. 직교 카메라라 원근 시차가 없어서, 카메라를 따라 층마다 다른 비율로 밀어 패럴랙스를 만든다.
##  측면 시점에서 크게 보이고, 탑다운에선 지도 북쪽 끝에 가면 지평선처럼 보인다.

const LAYERS := [
	# z(뒤로), 높이(m), 패럴랙스(0 = 카메라에 붙음 … 1 = 땅과 같이), base, amp, freq, bumps, 색(위·아래·윗선)
	[-90.0, 70.0, 0.08, 0.0, 0.0, 0.0, 0.0, "sky"],
	[-70.0, 40.0, 0.18, 0.28, 0.35, 0.0025, 0.0, ["#9ab8c0", "#8aa8b4", "#c8dce0"]],
	[-45.0, 34.0, 0.35, 0.22, 0.25, 0.004, 0.6, ["#6a9090", "#5a8080", "#9cc0b8"]],
	[-22.0, 28.0, 0.6, 0.16, 0.18, 0.007, 1.0, ["#3e6a5c", "#2e5248", "#6a9a7a"]],
]

var layers: Array = []
var rig: CamRig
var origin_x := 0.0


func build(width: float, preset: Dictionary) -> void:
	origin_x = width * 0.5
	for L in LAYERS:
		var q := QuadMesh.new()
		var w := 260.0
		q.size = Vector2(w, float(L[1]))
		q.center_offset = Vector3(0, float(L[1]) * 0.5, 0)
		var mi := MeshInstance3D.new()
		mi.mesh = q
		var m := ShaderMaterial.new()
		m.shader = load("res://shaders/backdrop.gdshader")
		m.set_shader_parameter("size_px", Vector2(w, float(L[1])) * 16.0)
		m.set_shader_parameter("seed", float(layers.size()) * 3.1)
		if L[7] is String:
			var sky: Color = preset.get("sky", Color("#8ab8b0"))
			m.set_shader_parameter("kind", 0)
			m.set_shader_parameter("col_bot", sky.lightened(0.25))
			m.set_shader_parameter("col_top", sky.darkened(0.1))
			m.set_shader_parameter("col_hi", Color(1, 1, 1).lerp(sky, 0.25))
		else:
			m.set_shader_parameter("kind", 1)
			m.set_shader_parameter("base", float(L[3]))
			m.set_shader_parameter("amp", float(L[4]))
			m.set_shader_parameter("freq", float(L[5]))
			m.set_shader_parameter("bumps", float(L[6]))
			var cols: Array = L[7]
			var tint: Color = preset.get("sky", Color("#8ab8b0"))
			m.set_shader_parameter("col_top", Color(cols[0]).lerp(tint, 0.25))
			m.set_shader_parameter("col_bot", Color(cols[1]).lerp(tint, 0.2))
			m.set_shader_parameter("col_hi", Color(cols[2]).lerp(tint, 0.15))
		mi.material_override = m
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		mi.position = Vector3(origin_x, -6.0, float(L[0]))
		add_child(mi)
		layers.append([mi, m, float(L[2])])


func _process(_dt: float) -> void:
	if rig == null:
		return
	# 층마다 카메라 이동의 (1 - 패럴랙스) 만큼 따라간다 → 먼 층일수록 느리게 지나간다
	for l in layers:
		var mi: MeshInstance3D = l[0]
		var k: float = l[2]
		var fx := rig.focus.x
		mi.position.x = origin_x + (fx - origin_x) * (1.0 - k)
		mi.position.y = -6.0 + (rig.focus.y) * (1.0 - k) * 0.5
