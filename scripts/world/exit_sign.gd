class_name ExitSign
extends Node3D
## 지역 출구 표시: "▲ 속삭이는 숲" — 가까이 오면 서서히 나타나고 화살표가 출구 쪽으로 까딱인다 (진행 유도)
##  main 이 지역을 만든 뒤 Region.exits 마다 하나씩 세운다

var text := ""
var dir := Vector3(0, 0, -1)       # 출구 쪽 (세계)
var target: Node3D                 # 거리 재는 대상 (내 캐릭터)
var _label: Label3D
var _arrow: Label3D
var _t := 0.0


func _ready() -> void:
	_label = _mk(text, "small")
	_label.position.y = 1.15
	_arrow = _mk("▲" if dir.z < -0.5 else ("▼" if dir.z > 0.5 else ("◀" if dir.x < 0.0 else "▶")), "big")
	_arrow.position.y = 0.45
	_arrow.modulate = Color("#ffe08a")


func _mk(t: String, kind: String) -> Label3D:
	var l := Label3D.new()
	l.add_to_group("world_tag")             # 타이틀 메뉴 뒤에서는 숨긴다
	l.text = t
	l.font = PixelFont.get_font(kind)
	l.font_size = PixelFont.size_of(kind)
	l.pixel_size = 1.0 / 16.0
	l.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	l.no_depth_test = true
	l.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	l.modulate = Color("#fff4d6")
	l.outline_modulate = Color(0.1, 0.08, 0.12)
	l.outline_size = 3
	l.render_priority = 5
	l.outline_render_priority = 4
	add_child(l)
	return l


func _process(dt: float) -> void:
	_t += dt
	var a := 0.0
	if target and is_instance_valid(target):
		var d := Vector2(target.global_position.x - global_position.x, target.global_position.z - global_position.z).length()
		a = clampf(1.0 - (d - 4.0) / 5.0, 0.0, 1.0)
	_label.modulate.a = a
	_label.outline_modulate.a = a
	_arrow.modulate.a = a
	_arrow.outline_modulate.a = a
	# 1/16 m (= 1px) 단위로 까딱 — 도트가 미끄러지지 않게
	var bob := floorf((sin(_t * 4.0) * 0.5 + 0.5) * 3.0) / 16.0
	_arrow.position = Vector3(dir.x, 0.0, dir.z) * bob + Vector3(0, 0.45, 0)
