class_name PingMark
extends Node3D
## 핑 (휠 클릭 · 패드 R3): 찍은 자리에 5초 — 적 근처면 위험(빨강), 아이템·상자 근처면 아이템(노랑), 아니면 이동(하늘)
##  모든 사람 화면·미니맵에 보인다 (UI 시스템.md)

const KINDS := {"danger": ["위험!", Color("#ff5a5a")], "item": ["아이템", Color("#ffd860")], "move": ["이리로", Color("#7ad8ff")], "help": ["도와줘!", Color("#ff9ad0")]}

var kind := "move"
var by := ""
var col := Color.WHITE
var t := 0.0
var label: Label3D


static func place(parent: Node, at: Vector3, who: Player) -> PingMark:
	var k := "move"
	for f in parent.get_tree().get_nodes_in_group("enemies"):
		if not (f as Fighter).dead and (f as Node3D).global_position.distance_to(at) < 1.8:
			k = "danger"
	if k == "move":
		for n in parent.get_tree().get_nodes_in_group("interact"):
			if (n is ItemDrop or n is Chest or n is ShopStand) and (n as Node3D).global_position.distance_to(at) < 1.8:
				k = "item"
	if who and who.hp < who.max_hp() * 0.3 and at.distance_to(who.global_position) < 1.5:
		k = "help"
	var p := PingMark.new()
	p.kind = k
	p.by = who.nick if who else ""
	p.col = KINDS[k][1]
	p.position = at                         # 지역(부모)은 원점에 있다 — 들어가기 전에 자리를 정해야 고리도 제자리에
	parent.add_child(p)
	return p


func _ready() -> void:
	add_to_group("pings")
	var r := Fx.flat_at(self, global_position, 0.0, ["ring_hit0", "ring_hit1", "ring_hit2", "ring_hit3"], 8.0)
	r.scale = Vector3(0.8, 1, 0.8)
	label = Label3D.new()
	label.text = "▼ %s\n%s" % [KINDS[kind][0], by]
	label.font = PixelFont.get_font("small")
	label.font_size = PixelFont.size_of("small")
	label.pixel_size = 1.0 / 16.0
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.no_depth_test = true
	label.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	label.outline_modulate = Color(0.1, 0.08, 0.12)
	label.outline_size = 3
	label.modulate = col
	label.render_priority = 7
	label.outline_render_priority = 6
	label.position.y = 1.4
	add_child(label)
	Audio.play("ping")


func _process(dt: float) -> void:
	t += dt
	label.position.y = 1.4 + (1.0 / 16.0 if int(t * 4.0) % 2 == 0 else 0.0)
	label.modulate.a = clampf((5.0 - t) / 0.5, 0.0, 1.0)
	label.outline_modulate.a = label.modulate.a
	if t > 5.0:
		queue_free()
