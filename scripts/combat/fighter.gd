class_name Fighter
extends CharacterBody3D
## 싸우는 것 공통 (플레이어 · 적 · 보스): 체력 · 팀 · 맞기 · 넉백 · 번쩍 · 독/느려짐 · 능력치 덧셈표
##  능력치는 직접 바꾸지 않고 mods[출처] = {이름: 비율} 로 쌓는다 (패시브·아이템이 서로 덮어쓰지 않게)
##  피해 계산 순서: 아군(50% 또는 현재 체력 비율 · ff_taken) → 받는 피해(dmg_taken) · 넉백(knock_taken) → 때린 플레이어에게 on_dealt

signal died(f: Fighter)
signal damaged(f: Fighter, hit: Hit, amount: float)

const DOT_KINDS := ["burn", "poison", "chain", "thorns", "bleed", "status"]
const GRAV := 26.0
var last_crit := false              # 마지막으로 맞은 것이 치명타였나 (마무리 효과용)
static var grav_k := 1.0           # 밈 모드 '달 중력' (0.45)

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
var stun_t := 0.0                  # 넘어짐·기절 (바나나 껍질 등): 움직이지도 공격하지도 못한다
var hidden_t := 0.0                # 숨음 (연막·그림자 망토): 적이 노리지 않는다
var _timed := {}                   # 잠깐 걸린 능력치: 출처 → 남은 시간 (set_timed_mod)
var status := {}                  # 상태이상 이름 → 남은 시간 (상태이상 시스템.md)
var status_src := {}               # 이름 → 건 쪽 (공포: 그 반대로 도망)
var immune: Array = []             # 이 상태이상은 안 걸린다 (보스·엘리트)
var _status_l: Label3D             # 머리 위 상태 글자
var _st_sig := ""
var _bleed_p := Vector3.ZERO
var held := false                  # 누가 들고 있다 (움직임·생각 멈춤 — 들고 있는 쪽이 자리를 정한다)
var thrown_by: Fighter             # 던져진 중: 날아가며 부딪힌 것에 피해 (사람 볼링)
var thrown_t := 0.0
var _thrown_hit := {}


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


## 잠깐만 걸리는 능력치 (t 초 뒤 저절로 풀림) — 장난꾸러기 장갑·폭풍 질주 등
func set_timed_mod(src: String, d: Dictionary, t: float) -> void:
	set_mod(src, d)
	_timed[src] = t


func ratio() -> float:
	return clampf(hp / maxf(1.0, max_hp()), 0.0, 1.0)


## 맞기 (모든 피해·회복이 여기로)
func take(h: Hit) -> void:
	h.sanitize()
	if dead:
		return
	if h.heal:
		if has_status("healblock"):
			return
		var before := hp
		hp = minf(max_hp(), hp + h.amount * stat("heal_taken"))
		if hp > before + 0.5:
			DmgNum.pop(self, hp - before, "heal")
			if h.source is Player and team == 0:
				get_tree().call_group("party", "record", h.source, "heal", hp - before)
		return
	if invuln > 0.0:
		return
	var amt := h.amount
	var friendly := h.team == team and h.source != null and h.source != self
	var knock := h.knock * (0.6 if friendly else 1.0)
	if h.source == self:
		amt *= h.self_ratio
	elif friendly:
		amt = hp * h.ff_cur if h.ff_cur >= 0.0 else amt * 0.5
		amt *= stat("ff_taken")                     # 저주받은 왕관: 아군에게 맞으면 2배
		# 장난꾸러기 장갑: 아군에게는 피해 대신 크게 밀치고 잠깐 빠르게
		if h.source is Player and (h.source as Player).inv and (h.source as Player).inv.fx("prank") > 0:
			amt = 0.0
			var away := global_position - (h.source as Node3D).global_position
			away.y = 0.0
			knock = (away.normalized() if away.length() > 0.01 else Vector3(1, 0, 0)) * 14.0 + Vector3(0, 5.0, 0)
			set_timed_mod("prank", {"speed": 0.5}, 1.0)
			Audio.play("troll", global_position, -6.0)
	# 치명타: 플레이어 → 적 (지속 피해·연쇄 번개 제외). 기본 8% + 아이템 (crit)
	if team == 1 and h.source is Player and not (h.kind in DOT_KINDS):
		var cc := 0.08 + ((h.source as Player).stat("crit") - 1.0)
		if randf() < cc:
			h.crit = true
			amt *= 1.8 * (h.source as Player).stat("crit_dmg")
	amt *= stat("dmg_taken")
	if friendly and h.source is Player and (h.source as Player).local:
		Achievements.add(get_tree(), "ff_hits")
	knock *= stat("knock_taken")
	if amt <= 0.0 and knock == Vector3.ZERO:
		return
	hp -= amt
	last_hit_by = h.source
	last_crit = h.crit
	flash = 0.12
	kv += knock
	if h.poison > 0.0:
		poison_t = 3.0
		poison_dps = h.poison
		poison_src = h.source
	if h.slow > 0.0:
		slow_t = 2.0
		slow_k = h.slow
	for st in h.status:
		apply_status(String(st), float(h.status[st]), h.source)
	if amt > 0.0 and has_status("freeze"):
		status.erase("freeze")                      # 빙결: 맞으면 깨진다
		Audio.play("crystal_ping", global_position, -6.0)
	if amt > 0.0:
		DmgNum.pop(self, amt, "ally" if friendly else ("player" if team == 0 else ("crit" if h.crit else "enemy")))
	damaged.emit(self, h, amt)
	_on_hurt(h, amt)
	# 때린 플레이어에게 알림 (궁극기 충전 · 아이템 효과)
	if amt > 0.0 and h.source is Player and h.source != self:
		(h.source as Player).on_dealt(self, amt, h, friendly)
		get_tree().call_group("party", "record", h.source, "ff" if friendly else "dmg", amt)
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
	stun_t = maxf(0.0, stun_t - dt)
	_status_tick(dt)
	if thrown_t > 0.0:
		_thrown_tick(dt)
	hidden_t = maxf(0.0, hidden_t - dt)
	if not _timed.is_empty():
		for src in _timed.keys():
			_timed[src] = float(_timed[src]) - dt
			if float(_timed[src]) <= 0.0:
				_timed.erase(src)
				set_mod(String(src), {})
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
	var k := stat("speed") * (1.0 - slow_k if slow_t > 0.0 else 1.0)
	if has_status("slow"):
		k *= 0.55
	if has_status("shock"):
		k *= 0.7
	if has_status("root") or has_status("freeze"):
		k = 0.0
	return k


## 움직일 수 없는가 (기절·빙결·넘어짐)
func disabled() -> bool:
	return stun_t > 0.0 or has_status("stun") or has_status("freeze")


## 중심 (맞는 판정용)
func center() -> Vector3:
	return global_position + Vector3(0, body_h * 0.5, 0)



## 던져짐: 날아가는 동안 부딪힌 것들에 피해 · 내려앉으면 나도 아프고 잠깐 넘어짐
func throw_from(by: Fighter, v: Vector3) -> void:
	held = false
	thrown_by = by
	thrown_t = 1.0
	_thrown_hit.clear()
	kv = Vector3(v.x, 0.0, v.z)
	velocity.y = v.y
	if self is CharacterBody3D:
		position.y += 0.2


func _thrown_tick(dt: float) -> void:
	thrown_t -= dt
	for f in Combat.in_radius(get_tree(), global_position, 0.55, 1.2):
		var fi := f as Fighter
		if fi == self or fi == thrown_by or _thrown_hit.has(fi):
			continue
		_thrown_hit[fi] = true
		var src: Node = thrown_by if is_instance_valid(thrown_by) else null
		fi.take(Hit.make(14.0, src, (kv.normalized() if kv.length() > 0.1 else Vector3.RIGHT) * 7.0 + Vector3(0, 3, 0), "smash"))
		Audio.play("smash", global_position, -6.0)
	if (thrown_t < 0.8 and is_on_floor()) or thrown_t <= 0.0:
		thrown_t = 0.0
		stun_t = maxf(stun_t, 0.5)
		var h := Hit.make(10.0 if team == 1 else 4.0, null, Vector3.ZERO, "smash")
		take(h)
		Audio.play("land_heavy", global_position)
		Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 14.0)



# ------------------------------------------------------------------ 상태이상 (상태이상 시스템.md)
## 이름: burn 화상 · poison 중독 · freeze 빙결 · shock 감전 · bleed 출혈 · fear 공포 · confuse 혼란 · silence 침묵
##       slow 둔화 · root 속박 · stun 기절 · weak 약화 · vuln 취약 · healblock 회복 불가
## 같은 상태이상은 시간만 갱신 (겹치지 않음) · 서로 다른 것은 동시에
const STATUS := {
	"burn": ["화상", Color("#ff8a3a")], "poison": ["중독", Color("#7ad06a")], "freeze": ["빙결", Color("#9ae0ff")],
	"shock": ["감전", Color("#ffe060")], "bleed": ["출혈", Color("#ff4a5a")], "fear": ["공포", Color("#b08ad8")],
	"confuse": ["혼란", Color("#d08aff")], "silence": ["침묵", Color("#a8a8b8")], "slow": ["둔화", Color("#7aa8ff")],
	"root": ["속박", Color("#c8a070")], "stun": ["기절", Color("#ffffff")], "weak": ["약화", Color("#a89a8a")],
	"vuln": ["취약", Color("#ff6a8a")], "healblock": ["회복 불가", Color("#c84a4a")],
}


func has_status(n: String) -> bool:
	return float(status.get(n, 0.0)) > 0.0


func apply_status(n: String, t: float, src: Node = null) -> void:
	if dead or n in immune or not STATUS.has(n):
		return
	status[n] = maxf(float(status.get(n, 0.0)), t)
	if src:
		status_src[n] = src
	match n:
		"weak":
			set_timed_mod("st_weak", {"attack": -0.3}, t)
		"vuln":
			set_timed_mod("st_vuln", {"dmg_taken": 0.3}, t)
		"shock":
			set_timed_mod("st_shock", {"atk_rate": -0.3}, t)
		"bleed":
			_bleed_p = global_position


func clear_status() -> void:
	status.clear()
	poison_t = 0.0
	slow_t = 0.0


func _status_tick(dt: float) -> void:
	if status.is_empty():
		if _status_l and _status_l.visible:
			_status_l.visible = false
		return
	for n in status.keys():
		status[n] = float(status[n]) - dt
		if float(status[n]) <= 0.0:
			status.erase(n)
			if n == "freeze" and spr_mat:
				spr_mat.set_shader_parameter("tint", Color.WHITE)
	if dead:
		return
	# 지속 피해: 화상 초당 4 (짧고 아픔) · 출혈은 움직인 만큼
	if has_status("burn"):
		hp -= 4.0 * dt
	if has_status("bleed"):
		var moved := Vector2(global_position.x - _bleed_p.x, global_position.z - _bleed_p.z).length()
		hp -= moved * 2.2
	_bleed_p = global_position
	if hp <= 0.0 and not dead:
		hp = 0.0
		_die()
		return
	# 머리 위 글자 (두 개까지)
	var keys: Array = status.keys()
	var sig := ",".join(keys)
	if sig != _st_sig:
		_st_sig = sig
		if _status_l == null:
			_status_l = Label3D.new()
			_status_l.font = PixelFont.get_font("small")
			_status_l.font_size = PixelFont.size_of("small")
			_status_l.pixel_size = 1.0 / 16.0
			_status_l.billboard = BaseMaterial3D.BILLBOARD_ENABLED
			_status_l.no_depth_test = true
			_status_l.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
			_status_l.outline_modulate = Color(0.1, 0.08, 0.12)
			_status_l.outline_size = 3
			_status_l.render_priority = 5
			add_child(_status_l)
		var names: Array = []
		for k in keys.slice(0, 2):
			names.append(STATUS[k][0])
		_status_l.text = " · ".join(names)
		if not keys.is_empty():
			_status_l.modulate = STATUS[keys[0]][1]
		_status_l.position.y = body_h + 0.75
	_status_l.visible = not keys.is_empty()
	if spr_mat and has_status("freeze"):
		spr_mat.set_shader_parameter("tint", Color(0.6, 0.85, 1.0))
	elif spr_mat and has_status("burn") and int(Time.get_ticks_msec() / 120) % 2 == 0:
		spr_mat.set_shader_parameter("flash", 0.25)
