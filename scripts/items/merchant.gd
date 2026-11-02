class_name Merchant
extends Node3D
## 떠돌이 상인 말풍선: 가까이 오면 한마디 (멀리선 안 보여서 화면이 어지럽지 않게)

const LINES := ["골라 봐! 금화는 다 같이 쓰는 거지만~", "먼저 사는 사람이 임자야", "위험 없는 물건은 없어. 진짜로.", "친구 몫까지 써 버려도 난 몰라~"]

var label: Label3D
var _i := 0
var _was_near := false


func _ready() -> void:
	label = Label3D.new()
	label.font = PixelFont.get_font("small")
	label.font_size = PixelFont.size_of("small")
	label.pixel_size = 1.0 / 16.0
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.no_depth_test = true
	label.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	label.outline_modulate = Color(0.1, 0.08, 0.12)
	label.outline_size = 3
	label.modulate = Color("#e8f4d8")
	label.position.y = 2.4
	label.visible = false
	add_child(label)


func _process(_dt: float) -> void:
	# 상인 가까이 (진열대보다 상인이 더 가까울 때만) — 진열대 설명과 글자가 겹치지 않게
	var near := false
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player:
			var d := (f as Node3D).global_position.distance_to(global_position)
			if d < 3.5:
				near = true
				for st in get_tree().get_nodes_in_group("shop"):
					if (f as Node3D).global_position.distance_to((st as Node3D).global_position) < d:
						near = false
			break
	if near and not _was_near:
		label.text = LINES[_i % LINES.size()]
		_i += 1
	_was_near = near
	label.visible = near
