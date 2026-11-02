class_name CrackedRock
extends StaticBody3D
## 금 간 바위 (비밀): 폭발로만 부서진다 (전투 시스템.md: 구조물은 폭발만) — 안에 숨은 보물 상자
## 폭탄마 · 자폭 도토리를 유인하기 · 메테오 · 폭죽 다발로도 부술 수 있다

var region: Region
var hp := 2


func _ready() -> void:
	add_to_group("breakable")
	collision_layer = 1
	var cs := CollisionShape3D.new()
	var b := CylinderShape3D.new()
	b.radius = 0.85
	b.height = 1.6
	cs.shape = b
	cs.position.y = 0.8
	add_child(cs)
	add_child(BB.make("res://assets/sprites/loot.png", "cracked_rock", {"unique": true, "up_normal": 0.7}))
	add_child(BB.blob(0.95, 0.4, 1.0))


## 폭발이 났다 (Bomb · 적 자폭 · 메테오 · 뜨거운 감자가 부른다)
func blast(at: Vector3, r: float) -> void:
	if Vector2(at.x - global_position.x, at.z - global_position.z).length() > r + 1.0:
		return
	hp -= 1
	Audio.play("wall_crumble", global_position, -4.0 if hp > 0 else 0.0)
	Particles.burst(get_parent(), global_position + Vector3(0, 0.6, 0), Color("#c8c0b0"), Color("#5a5048"), 18, 5.0)
	if hp > 0:
		return
	var ch := Chest.new()
	ch.region = region
	ch.luck = 2.0
	region.add_child(ch)
	ch.position = position
	Coin.burst(region, global_position + Vector3(0, 0.8, 0), 6, region.terrain)
	get_tree().call_group("hud", "notify", "비밀 발견! 바위 속에 보물 상자가", Color("#ffd860"))
	Achievements.add(get_tree(), "secrets")
	queue_free()
