class_name PondSim
extends Node
## 연못 물결 시뮬레이션 (GPU): 두 장의 작은 화면(96x64, 실수 정밀도)을 번갈아 그리며 파동 방정식을 푼다.
##  물방울을 떨어뜨리면(splash) 동그란 물결이 퍼지고 벽에 부딪혀 되돌아온다. 결과는 물 셰이더가 읽는다.

const W := 96
const H := 64

var vps: Array[SubViewport] = []
var mats: Array[ShaderMaterial] = []
var cur := 0
var pending: Array = []            # [uv, 반지름, 세기]
var water_mat: ShaderMaterial
var rect := Rect2()                # 연못이 덮는 세계 xz 범위
var _rain := 0.0


func setup(wm: ShaderMaterial, world_rect: Rect2) -> void:
	add_to_group("pond")
	water_mat = wm
	rect = world_rect
	for i in 2:
		var vp := SubViewport.new()
		vp.size = Vector2i(W, H)
		vp.use_hdr_2d = true
		vp.transparent_bg = false
		vp.disable_3d = true
		vp.render_target_update_mode = SubViewport.UPDATE_DISABLED
		vp.render_target_clear_mode = SubViewport.CLEAR_MODE_NEVER
		var cr := ColorRect.new()
		cr.size = Vector2(W, H)
		var m := ShaderMaterial.new()
		m.shader = load("res://shaders/ripple_step.gdshader")
		m.set_shader_parameter("texel", Vector2(1.0 / W, 1.0 / H))
		cr.material = m
		vp.add_child(cr)
		add_child(vp)
		vps.append(vp)
		mats.append(m)
	# 처음엔 서로를 읽게 (빈 텍스처 = 0.5 아님 → 첫 몇 걸음 무시해도 됨)
	mats[0].set_shader_parameter("prev", vps[1].get_texture())
	mats[1].set_shader_parameter("prev", vps[0].get_texture())


## 세계 위치에 물방울 (세기 0.1 작은 발걸음 … 0.6 폭탄)
func splash(p: Vector3, strength := 0.25, radius := 2.5) -> void:
	if not rect.has_point(Vector2(p.x, p.z)):
		return
	var uv := Vector2((p.x - rect.position.x) / rect.size.x, (p.z - rect.position.y) / rect.size.y)
	pending.append([uv, radius, strength])


func _process(dt: float) -> void:
	if vps.is_empty():
		return
	# 가끔 물고기·벌레가 톡
	_rain -= dt
	if _rain <= 0.0:
		_rain = randf_range(0.6, 2.2)
		pending.append([Vector2(randf_range(0.2, 0.8), randf_range(0.25, 0.75)), 1.6, randf_range(0.08, 0.16)])
	var nxt := 1 - cur
	var arr: Array[Vector4] = [Vector4.ZERO, Vector4.ZERO, Vector4.ZERO, Vector4.ZERO]
	for i in mini(4, pending.size()):
		var d: Array = pending[i]
		arr[i] = Vector4((d[0] as Vector2).x, (d[0] as Vector2).y, float(d[1]), float(d[2]))
	pending = pending.slice(mini(4, pending.size()))
	mats[nxt].set_shader_parameter("drops", arr)
	vps[nxt].render_target_update_mode = SubViewport.UPDATE_ONCE
	cur = nxt
	if water_mat:
		water_mat.set_shader_parameter("ripple", vps[cur].get_texture())
