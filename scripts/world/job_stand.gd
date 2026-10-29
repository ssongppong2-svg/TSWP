class_name JobStand
extends Node3D
## 캠프의 직업 표지: 가까이 가서 E — 그 직업으로 바꾼다 (로비의 '직업 변경')
##  표지 위에 이름, 가까이 가면 한 줄 설명이 뜬다

var job := "warrior"
var reach := 1.4
var spr: MeshInstance3D
var name_l: Label3D
var desc_l: Label3D
var _t := 0.0


func _ready() -> void:
	add_to_group("interact")
	var J: Dictionary = DB.JOBS[job]
	spr = BB.make(CharSprite.ATLAS, "%s_idle_s0" % J["sprite"], {"unique": true, "up_normal": 0.6})
	add_child(spr)
	add_child(BB.blob(0.42, 0.2, 0.8))
	name_l = _label(String(J["name"]), "small", Color("#fff4d6"))
	name_l.position.y = 2.4
	desc_l = _label("[E] %s\n%s" % [J["name"], J["desc"]], "small", Color(1, 1, 1, 0.95))
	desc_l.position.y = 3.9
	desc_l.visible = false


func _label(t: String, kind: String, c: Color) -> Label3D:
	var l := Label3D.new()
	l.text = t
	l.font = PixelFont.get_font(kind)
	l.font_size = PixelFont.size_of(kind)
	l.pixel_size = 1.0 / 16.0
	l.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	l.no_depth_test = true
	l.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	l.modulate = c
	l.outline_modulate = Color(0.1, 0.08, 0.12)
	l.outline_size = 3
	add_child(l)
	return l


func _process(dt: float) -> void:
	_t += dt
	# 숨 쉬는 그림 (idle 2프레임)
	var J: Dictionary = DB.JOBS[job]
	(spr.material_override as ShaderMaterial).set_shader_parameter("region", BB.region_uv(CharSprite.ATLAS, "%s_idle_s%d" % [J["sprite"], int(_t * 2.5) % 2]))
	var near := false
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player and (f as Node3D).global_position.distance_to(global_position) < reach + 0.2:
			near = true
			name_l.modulate = Color("#9aff9a") if (f as Player).job == job else Color("#fff4d6")
	desc_l.visible = near
	name_l.visible = not near


func interact(p: Node) -> void:
	if p is Player and (p as Player).job != job:
		(p as Player).set_job(job)
		Audio.play("ui_confirm", global_position)
		Fx.pop(get_parent(), (p as Node3D).global_position, ["spark0", "spark1", "spark2"], 12.0)
