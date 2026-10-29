class_name Fighter
extends CharacterBody3D
## 싸우는 것 공통 (플레이어 · 적 · 보스): 체력 · 팀 · 맞기 · 넉백 · 번쩍 · 독/느려짐 · 능력치 덧셈표
##  능력치는 직접 바꾸지 않고 mods[출처] = {이름: 비율} 로 쌓는다 (패시브·아이템이 서로 덮어쓰지 않게)

signal died(f: Fighter)
signal damaged(f: Fighter, hit: Hit, amount: float)

const GRAV := 26.0

var team := 0                      # 0 플레이어 · 1 적
var hp := 100.0
var base_max_hp := 100.0
var dead := false
var invuln := 0.0
var kv := Vector3.ZERO             # 넉백 속도 (따로 줄어든다)
var flash := 0.0
var poison_t := 0.0
var poison_dps := 0.0
var poison_src: Node = null
var slow_t := 0.0
var slow_k := 0.0
var mods := {}                     # 출처 → {"speed": +0.8, "attack": -0.2, "max_hp": -0.25}
var rig: CamRig
var terrain: Terrain
var spr_mat: ShaderMaterial        # 맞으면 하얗게
var body_r := 0.3
var body_h := 1.0
var last_hit_by: Node = null


func _enter_tree() -> void:
	add_to_group("fighters")


func stat(name_: String) -> float:
	var k := 0.0
	for src in mods:
		k += float((mods[src] as Dictionary).get(name_, 0.0))
	return maxf(0.05, 1.0 + k)


func max_hp() -> float:
	return base_max_hp * stat("max_hp")


func set_mod(src: String, d: Dictionary) -> void:
	var before := max_hp()
	if d.is_empty():
		mods.erase(src)
	else:
		mods[src] = d
	# 최대 체력이 바뀌면 비율 유지
	var after := max_hp()
	if absf(after - before) > 0.01 and before > 0.0:
		hp = clampf(hp * after / before, 0.0, after)


func ratio() -> float:
	return clampf(hp / maxf(1.0, max_hp()), 0.0, 1.0)


## 맞기 (모든 피해·회복이 여기로)
func take(h: Hit) -> void:
	if dead:
		return
	if h.heal:
		var before := hp
		hp = minf(max_hp(), hp + h.amount)
		if hp > before + 0.5:
			DmgNum.pop(self, hp - before, "heal")
		return
	if invuln > 0.0:
		return
	var amt := h.amount
	var friendly := h.team == team and h.source != null
	if h.source == self:
		amt *= h.self_ratio
	elif friendly:
		amt = hp * h.ff_cur if h.ff_cur >= 0.0 else amt * 0.5
	if amt <= 0.0 and h.knock == Vector3.ZERO:
		return
	hp -= amt
	last_hit_by = h.source
	flash = 0.12
	kv += h.knock * (0.6 if friendly else 1.0)
	if h.poison > 0.0:
		poison_t = 3.0
		poison_dps = h.poison
		poison_src = h.source
	if h.slow > 0.0:
		slow_t = 2.0
		slow_k = h.slow
	if amt > 0.0:
		DmgNum.pop(self, amt, "ally" if friendly else ("player" if team == 0 else "enemy"))
	damaged.emit(self, h, amt)
	_on_hurt(h, amt)
	if hp <= 0.0:
		hp = 0.0
		_die()


func _on_hurt(_h: Hit, _amt: float) -> void:
	pass


func _die() -> void:
	dead = true
	died.emit(self)


## 넉백·상태 이상 진행 (자식이 _physics_process 안에서 부른다)
func tick_status(dt: float) -> void:
	invuln = maxf(0.0, invuln - dt)
	if flash > 0.0:
		flash = maxf(0.0, flash - dt)
	if spr_mat:
		spr_mat.set_shader_parameter("flash", 1.0 if flash > 0.06 else (0.5 if flash > 0.0 else 0.0))
	if poison_t > 0.0 and not dead:
		poison_t -= dt
		var d := poison_dps * dt
		hp -= d
		if spr_mat:
			spr_mat.set_shader_parameter("tint", Color(0.75, 1.0, 0.7))
		if hp <= 0.0:
			hp = 0.0
			last_hit_by = poison_src
			_die()
		elif poison_t <= 0.0 and spr_mat:
			spr_mat.set_shader_parameter("tint", Color.WHITE)
	if slow_t > 0.0:
		slow_t -= dt
	kv = kv.move_toward(Vector3.ZERO, (14.0 if is_on_floor() else 4.0) * dt)


func move_mul() -> float:
	return stat("speed") * (1.0 - slow_k if slow_t > 0.0 else 1.0)


## 중심 (맞는 판정용)
func center() -> Vector3:
	return global_position + Vector3(0, body_h * 0.5, 0)
