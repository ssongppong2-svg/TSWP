class_name Party
extends Node
## 파티 공유 상태: 부활 횟수(인원 × 3) · 금화 · 기록(처치·피해·회복·사망 — 결과 화면용)
## 근거: 게임 시작과 선택, 직업, 플레이.md / 방 시스템.md

signal changed
signal game_over

var lives := 3
var gold := 0
var stats := {}                    # 플레이어 → {"kills", "deaths", "dmg", "heal"}
var over := false


func _enter_tree() -> void:
	add_to_group("party")


func setup(player_count: int) -> void:
	lives = player_count * 3
	changed.emit()


## 부활 1회 사용. 남은 게 없으면 false (그 플레이어는 관전)
func use_life(p: Node) -> bool:
	_stat(p, "deaths", 1)
	if lives <= 0:
		_check_wipe()
		return false
	lives -= 1
	changed.emit()
	return true


func _check_wipe() -> void:
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player and not (f as Player).dead:
			return
	if not over:
		over = true
		game_over.emit()


func add_gold(n: int) -> void:
	gold += n
	changed.emit()


func _stat(p: Node, key: String, n: float) -> void:
	if p == null:
		return
	if not stats.has(p):
		stats[p] = {"kills": 0, "deaths": 0, "dmg": 0.0, "heal": 0.0}
	stats[p][key] += n


func on_enemy_killed(e: Enemy, by: Node) -> void:
	add_gold(int(e.E.get("gold", 1)))
	if by is Player:
		_stat(by, "kills", 1)


## 무리 밖에서 생긴 적(고치에서 부화 등)도 처치 보상 연결
func on_spawn(e: Enemy) -> void:
	if not e.killed.is_connected(on_enemy_killed):
		e.killed.connect(on_enemy_killed)
