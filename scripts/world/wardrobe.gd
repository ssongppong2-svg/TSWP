class_name Wardrobe
extends Node3D
## 캠프 옷장: E 로 꾸미기 (머리·피부·옷·모자·이름)

var reach := 1.5
var _label: Label3D


func _ready() -> void:
	add_to_group("interact")
	add_child(BB.make("res://assets/sprites/loot.png", "wardrobe", {"unique": true, "up_normal": 0.7}))
	add_child(BB.blob(0.7, 0.3, 0.9))
	_label = Label3D.new()
	_label.add_to_group("world_tag")             # 타이틀 메뉴 뒤에서는 숨긴다
	_label.text = "[E] 꾸미기"
	_label.font = PixelFont.get_font("small")
	_label.font_size = PixelFont.size_of("small")
	_label.pixel_size = 1.0 / 16.0
	_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_label.no_depth_test = true
	_label.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	_label.outline_modulate = Color(0.1, 0.08, 0.12)
	_label.outline_size = 3
	_label.modulate = Color("#ffe9a8")
	_label.position.y = 2.4
	_label.visible = false
	add_child(_label)


func _process(_dt: float) -> void:
	var near := false
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player and (f as Node3D).global_position.distance_to(global_position) < 2.4:
			near = true
	_label.visible = near


func interact(p: Node) -> void:
	get_tree().call_group("game", "open_wardrobe", p)
