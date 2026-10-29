class_name BB
extends RefCounted
## 서 있는 도트 그림(빌보드) 만들기 도우미.
## 아틀라스 PNG + 같은 이름 .json ({이름: [x, y, w, h]}) — 발 위치 = 그림 아래 가운데.

const PPU := 16.0
static var _atlases := {}      # path → {tex, rects, size}
static var _shader: Shader
static var _mats := {}         # 공유 재질 캐시 (같은 그림·같은 설정)


static func atlas(path: String) -> Dictionary:
	if _atlases.has(path):
		return _atlases[path]
	var tex: Texture2D = load(path)
	var js := FileAccess.get_file_as_string(path.get_basename() + ".json")
	var rects: Dictionary = JSON.parse_string(js) if js != "" else {}
	var d := {"tex": tex, "rects": rects, "size": Vector2(tex.get_size())}
	_atlases[path] = d
	return d


static func shader() -> Shader:
	if _shader == null:
		_shader = load("res://shaders/billboard.gdshader")
	return _shader


static func region_uv(path: String, name: String) -> Vector4:
	var a := atlas(path)
	var r: Array = a["rects"].get(name, [0, 0, 16, 16])
	var s: Vector2 = a["size"]
	return Vector4(r[0] / s.x, r[1] / s.y, r[2] / s.x, r[3] / s.y)


static func px_size(path: String, name: String) -> Vector2:
	var r: Array = atlas(path)["rects"].get(name, [0, 0, 16, 16])
	return Vector2(r[2], r[3])


## 그림 하나 세우기. opts: sway, push, shadow, tint, up_normal, emission, receive_shade, unique(재질 따로)
static func make(path: String, name: String, opts := {}) -> MeshInstance3D:
	var sz := px_size(path, name)
	var q := QuadMesh.new()
	q.size = sz / PPU
	q.center_offset = Vector3.ZERO if opts.get("center", false) else Vector3(0, sz.y / PPU * 0.5, 0)
	var mi := MeshInstance3D.new()
	mi.mesh = q
	var key := "%s|%s|%s" % [path, name, str(opts)]
	var m: ShaderMaterial
	if not opts.get("unique", false) and _mats.has(key):
		m = _mats[key]
	else:
		m = ShaderMaterial.new()
		m.shader = shader()
		m.set_shader_parameter("tex", atlas(path)["tex"])
		m.set_shader_parameter("region", region_uv(path, name))
		m.set_shader_parameter("sway", float(opts.get("sway", 0.0)))
		m.set_shader_parameter("push_k", float(opts.get("push", 0.0)))
		m.set_shader_parameter("up_normal", float(opts.get("up_normal", 0.85)))
		m.set_shader_parameter("emission", float(opts.get("emission", 0.0)))
		m.set_shader_parameter("receive_shade", bool(opts.get("receive_shade", true)))
		m.set_shader_parameter("see_through", bool(opts.get("see_through", false)))
		if opts.has("tint"):
			m.set_shader_parameter("tint", opts["tint"])
		if not opts.get("unique", false):
			_mats[key] = m
	mi.material_override = m
	# 그림자는 기본으로 끈다: 빌보드가 해 쪽으로 돌아 드리우는 그림자는 길고 어색하다 → 땅에 구운 그늘(Terrain.blob)·발밑 그늘(BB.blob)로 대신
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if opts.get("shadow", false) else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	# 빌보드는 셰이더가 세로로 늘리므로 컬링 상자를 넉넉히
	mi.extra_cull_margin = 4.0
	return mi


## 움직이는 것(캐릭터·던진 물건) 발밑 그늘: 땅에 눕힌 타원. rx·rz 는 m.
static func blob(rx: float, rz: float, k := 1.0) -> MeshInstance3D:
	var q := QuadMesh.new()
	q.size = Vector2(rx * 2.0, rz * 2.0)
	q.orientation = PlaneMesh.FACE_Y
	var mi := MeshInstance3D.new()
	mi.mesh = q
	var m := ShaderMaterial.new()
	m.shader = load("res://shaders/blob.gdshader")
	m.set_shader_parameter("size_px", Vector2(rx, rz) * 2.0 * PPU)
	m.set_shader_parameter("strength", k)
	mi.material_override = m
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.position.y = 0.02
	return mi


## 같은 아틀라스의 여러 그림을 한 번에 (풀·꽃처럼 수천 개). items: [[이름, 위치, 뒤집기]]
static func multi(path: String, items: Array, opts := {}) -> MultiMeshInstance3D:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_custom_data = true
	mm.use_colors = true
	# 모든 그림을 같은 크기 사각형(가장 큰 것)으로 — 실제 크기는 인스턴스 스케일
	var q := QuadMesh.new()
	q.size = Vector2.ONE
	q.center_offset = Vector3(0, 0.5, 0)
	mm.mesh = q
	mm.instance_count = items.size()
	for i in items.size():
		var it: Array = items[i]
		var sz := px_size(path, it[0]) / PPU
		var tr := Transform3D(Basis.from_scale(Vector3(sz.x, sz.y, 1.0)), it[1])
		mm.set_instance_transform(i, tr)
		mm.set_instance_custom_data(i, Color(region_uv(path, it[0]).x, region_uv(path, it[0]).y, region_uv(path, it[0]).z, region_uv(path, it[0]).w))
		var c: Color = it[3] if it.size() > 3 else Color.WHITE
		c.a = 0.0 if (it.size() > 2 and it[2]) else 1.0
		mm.set_instance_color(i, c)
	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	var m := ShaderMaterial.new()
	m.shader = shader()
	m.set_shader_parameter("tex", atlas(path)["tex"])
	m.set_shader_parameter("use_instance", true)
	m.set_shader_parameter("sway", float(opts.get("sway", 0.0)))
	m.set_shader_parameter("push_k", float(opts.get("push", 0.0)))
	m.set_shader_parameter("up_normal", float(opts.get("up_normal", 0.9)))
	mmi.material_override = m
	mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if opts.get("shadow", false) else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mmi.extra_cull_margin = 4.0
	return mmi
