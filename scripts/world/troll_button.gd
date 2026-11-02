class_name TrollButton
extends Node3D
## '누르지 마시오' 빨간 버튼 (트롤 퍼즐 시스템): 누르면 70% 몬스터 · 30% 금화 비. 20초에 한 번.

var reach := 1.3
var region: Region
var _cool := 0.0
var _label: Label3D


func _ready() -> void:
	add_to_group("interact")
	add_child(BB.make("res://assets/sprites/loot.png", "troll_button", {"unique": true, "up_normal": 0.7}))
	add_child(BB.blob(0.4, 0.2, 0.9))
	_label = Label3D.new()
	_label.text = "누르지 마시오"
	_label.font = PixelFont.get_font("small")
	_label.font_size = PixelFont.size_of("small")
	_label.pixel_size = 1.0 / 16.0
	_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_label.no_depth_test = true
	_label.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	_label.outline_modulate = Color(0.1, 0.08, 0.12)
	_label.outline_size = 3
	_label.modulate = Color("#ff7a7a")
	_label.position.y = 1.9
	add_child(_label)


func _process(dt: float) -> void:
	_cool -= dt
	_label.text = "누르지 마시오" if _cool <= 0.0 else "…%d" % ceili(_cool)


func interact(p: Node) -> void:
	if _cool > 0.0:
		return
	_cool = 20.0
	Audio.play("troll", global_position)
	var nick := (p as Player).nick if p is Player else "누군가"
	if randf() < 0.7:
		get_tree().call_group("hud", "notify", "%s 눌렀다… 몬스터다!" % Josa.i_ga(nick), Color("#ff7a7a"))
		get_tree().call_group("hud", "banner", "아ㅋㅋㅋ 또 망했다!", 2.0)
		for k in 3:
			var a := k * TAU / 3.0
			var at := global_position + Vector3(cos(a), 0, sin(a)) * 2.6
			at.y = region.terrain.height_at(at) + 0.2
			var e := Enemy.spawn(region, ["acorn", "spider", "boomer"][k], at, region.terrain, region.rig)
			region.enemy_spawned.emit(e)
	else:
		get_tree().call_group("hud", "notify", "%s 눌렀다… 금화다?!" % Josa.i_ga(nick), Color("#ffd860"))
		Coin.burst(region, global_position + Vector3(0, 1.2, 0), 14, region.terrain)
