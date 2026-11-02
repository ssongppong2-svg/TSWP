class_name Emote
extends Node3D
## 머리 위 이모트 말풍선 (톡 튀어나와 2.4초 뒤 사라짐). 같은 사람이 다시 하면 바꿔 단다.

const IDS := ["smile", "lol", "cry", "thumb", "down", "skull", "heart", "what"]
const NAMES := {"smile": "좋아", "lol": "ㅋㅋㅋ", "cry": "흑흑", "thumb": "최고", "down": "별로", "skull": "죽었다", "heart": "고마워", "what": "뭐야?"}

var t := 0.0
var mesh: MeshInstance3D


static func pop_on(target: Node3D, id: String) -> void:
	if not IDS.has(id) or target == null or not target.is_inside_tree():
		return
	var old := target.get_node_or_null("Emote")
	if old:
		old.free()
	var e := Emote.new()
	e.name = "Emote"
	e.mesh = BB.make("res://assets/sprites/ui.png", "emote_" + id, {"unique": true, "receive_shade": false, "up_normal": 1.0, "emission": 0.2})
	e.add_child(e.mesh)
	target.add_child(e)
	e.position = Vector3(0.45, 1.75, 0)
	Audio.play("emote", target.global_position)


func _process(dt: float) -> void:
	t += dt
	# 톡 (1px 단위로 커졌다 제자리)
	var k := 1.0 + (0.35 if t < 0.06 else (0.15 if t < 0.12 else 0.0))
	mesh.scale = Vector3(k, k, 1)
	position.y = 1.75 + (1.0 / 16.0 if int(t * 3.0) % 2 == 0 else 0.0)
	if t > 2.4:
		queue_free()
