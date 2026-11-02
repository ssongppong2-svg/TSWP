class_name ShopStand
extends Node3D
## 상점 진열대: 아이템 하나 + 값. E 로 산다 — 파티 금화(공용)에서 나간다 (누가 먼저 쓰느냐 = 트롤 거리)

const ATLAS := "res://assets/sprites/loot.png"

var id := ""
var reach := 1.3
var sold := false
var icon: MeshInstance3D
var price_l: Label3D
var name_l: Label3D
var _t := randf() * 3.0


func _ready() -> void:
	add_to_group("interact")
	add_to_group("shop")
	add_child(BB.make(ATLAS, "pedestal", {"unique": true, "up_normal": 0.8}))
	add_child(BB.blob(0.5, 0.25, 0.8))
	icon = BB.make(ItemDrop.ATLAS, id, {"unique": true, "center": true, "receive_shade": false, "emission": 0.2})
	icon.position.y = 1.25
	add_child(icon)
	price_l = _lbl("%d G" % Items.price(id), "small", Color("#ffd860"))
	price_l.position.y = 0.05
	price_l.position.z = 0.3
	name_l = _lbl("[E] %s\n%s" % [Items.get_item(id).get("name", id), Items.get_item(id).get("desc", "")], "small", Items.color(id))
	name_l.position.y = 2.15
	name_l.width = 160.0
	name_l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	name_l.visible = false


func _lbl(t: String, kind: String, c: Color) -> Label3D:
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
	l.render_priority = 6
	l.outline_render_priority = 5
	add_child(l)
	return l


func _process(dt: float) -> void:
	_t += dt
	if sold:
		return
	icon.position.y = 1.25 + floorf((sin(_t * 2.5) * 0.5 + 0.5) * 2.0) / 16.0
	# 이름·설명은 가장 가까운 진열대 하나만 (글자가 겹치지 않게)
	var near := false
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player:
			var d := (f as Node3D).global_position.distance_to(global_position)
			if d < 1.8:
				near = true
				for o in get_tree().get_nodes_in_group("shop"):
					if o != self and not (o as ShopStand).sold and (f as Node3D).global_position.distance_to((o as Node3D).global_position) < d:
						near = false
				break
	name_l.visible = near
	var party := get_tree().get_first_node_in_group("party")
	price_l.modulate = Color("#ffd860") if party and party.gold >= Items.price(id) else Color("#ff7a6a")


func interact(p: Node) -> void:
	if sold or not p is Player:
		return
	var party := get_tree().get_first_node_in_group("party")
	var cost := Items.price(id)
	if party == null or party.gold < cost:
		Audio.play("ui_denied")
		if (p as Player).local:
			get_tree().call_group("hud", "toast_text", "금화가 모자라다 (%d G 필요)" % cost, Color("#ff9a8a"))
		return
	party.add_gold(-cost)
	if (p as Player).local:
		Achievements.add(get_tree(), "spent", cost)
	Audio.play("coin", global_position)
	(p as Player).pick_up(id)
	sold = true
	remove_from_group("interact")
	remove_from_group("shop")
	icon.visible = false
	name_l.visible = false
	price_l.text = "팔림"
	price_l.modulate = Color(1, 1, 1, 0.5)
