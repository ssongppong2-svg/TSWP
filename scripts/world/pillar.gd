class_name Pillar
extends StaticBody3D
## 솟아오르는 돌기둥 + 남쪽 발밑 점프 발판 (보스 01 껍질 기믹: 높은 곳에서 때려야 약점이 깨진다)
##  발판을 밟으면 기둥 위로 튀어 오른다. 내려갈 땐 그냥 걸어 나가면 된다.

const H := 2.2
var sprite: MeshInstance3D
var pad: Fx
var up := false
var pad_pos := Vector3.ZERO
var _cool := {}


func _ready() -> void:
	collision_layer = 1
	var cs := CollisionShape3D.new()
	var bx := BoxShape3D.new()
	bx.size = Vector3(1.3, H, 0.9)
	cs.shape = bx
	cs.position.y = H * 0.5
	add_child(cs)
	sprite = BB.make(BossBits.ATLAS, "pillar", {"receive_shade": false, "see_through": true})
	sprite.position.z = 0.45
	add_child(sprite)
	position.y = -3.2
	visible = false


func set_up(on: bool) -> void:
	if on == up:
		return
	up = on
	visible = true
	var tw := create_tween()
	tw.tween_property(self, "position:y", 0.0 if on else -3.2, 0.6).set_trans(Tween.TRANS_BACK if on else Tween.TRANS_SINE)
	Audio.play("wall_crumble", global_position, -6.0)
	if on:
		pad_pos = global_position + Vector3(0, 0, 1.4)
		pad_pos.y = 0.0
		tw.tween_callback(func():
			pad = Fx.flat_at(get_parent(), pad_pos, 0.0, ["ring_heal0", "ring_heal1", "ring_heal2", "ring_heal3", "ring_heal2", "ring_heal1"] , 6.0)
			pad.fps = 6.0)
	else:
		tw.tween_callback(func(): visible = false)


func _physics_process(dt: float) -> void:
	if not up:
		return
	# 발판 고리가 계속 돌게
	if pad == null or not is_instance_valid(pad):
		if position.y > -0.1:
			pad = Fx.flat_at(get_parent(), pad_pos, 0.0, ["ring_heal0", "ring_heal1", "ring_heal2", "ring_heal3", "ring_heal2", "ring_heal1"], 6.0)
	for k in _cool.keys():
		_cool[k] = float(_cool[k]) - dt
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player:
			var p := f as Player
			var d := p.global_position - pad_pos
			d.y = 0.0
			if d.length() < 0.7 and p.is_on_floor() and float(_cool.get(p, 0.0)) <= 0.0 and p.global_position.y < 0.5:
				_cool[p] = 1.0
				p.launch(Vector3(0, 11.5, 0), (global_position - p.global_position) * Vector3(1, 0, 1) * 1.6)
				Audio.play("shroom", pad_pos)
