class_name DiffBoard
extends Node3D
## 캠프 난이도 표지판: E 로 바꾼다 (겁쟁이 → 인간 → 신 → 밈). 숲에 들어갈 때 정해진다.

var reach := 1.5
var label: Label3D


func _ready() -> void:
	add_to_group("interact")
	add_child(BB.make(Region.CAMP, "sign", {"unique": true, "up_normal": 0.7}))
	label = Label3D.new()
	label.add_to_group("world_tag")             # 타이틀 메뉴 뒤에서는 숨긴다
	label.font = PixelFont.get_font("small")
	label.font_size = PixelFont.size_of("small")
	label.pixel_size = 1.0 / 16.0
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.no_depth_test = true
	label.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	label.outline_modulate = Color(0.1, 0.08, 0.12)
	label.outline_size = 3
	label.width = 150.0
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.position.y = 2.6
	add_child(label)
	_refresh(false)


func _refresh(near: bool) -> void:
	var lv := Diff.level()
	label.text = ("난이도: %s" % Diff.NAMES[lv]) + (("\n[E] 바꾸기 · " + Diff.DESC[lv]) if near else "")
	label.modulate = Diff.COLS[lv]


func _process(_dt: float) -> void:
	var near := false
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player and (f as Node3D).global_position.distance_to(global_position) < 2.4:
			near = true
	_refresh(near)


func interact(_p: Node) -> void:
	Prefs.difficulty = (Prefs.difficulty + 1) % 4
	Prefs.save_prefs()
	Audio.play("ui_toggle", global_position)
	get_tree().call_group("hud", "notify", "난이도: %s" % Diff.NAMES[Diff.level()], Diff.COLS[Diff.level()])
