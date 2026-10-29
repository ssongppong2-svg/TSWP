class_name Queen
extends Fighter
## 보스 01 · 부화의 여왕 (거미 여왕). 근거: 보스 시스템.md + Unity 판 Boss_HatchQueen 설정값
##  패턴 (상황 따라 고른다 — 무작위 아님)
##   · 물기: 가까운 대상 앞 부채꼴
##   · 휩쓸기: 가장 가까운 플레이어 자리에 원 (예고 0.5초)
##   · 내려찍기: 여왕 둘레 큰 원 (예고 0.9초, 폭발 판정)
##   · 돌진: 멀리 있는 대상에게 직선 돌진 (예고 0.8초)
##   · 고치: 둘레에 고치 3개 → 4초 뒤 거미 부화 (최대 6개)
##   · 거미줄: 플레이어 자리에 거미줄 장판 (느려짐)
##  기믹
##   · 위험 게이지: 시간이 갈수록, 가까이 붙어 있을수록 빨리 찬다 → 가득 차면 둘레 대폭발 (흩어질지 계속 때릴지 판단)
##   · 껍질 (체력 66%·33%): 껍질이 굳어 거의 안 아프다 → 돌기둥이 솟고 점프 발판이 열린다
##       기둥 위(높은 곳)에서 때려야 등의 약점이 깨진다 → 깨지면 3초 기절 (역할 분담: 올라갈 사람 · 아래서 버틸 사람)
##   · 광폭화 (30%): 빨라지고 패턴 간격이 짧아진다
##   · 2막: 쓰러지면 거미 떼 + 레버 두 개를 거의 동시에 당겨야 진짜 끝 (협동 퍼즐)

signal phase_changed(q: Queen, phase: String)
signal defeated(q: Queen)

const ATLAS := "res://assets/sprites/boss.png"

var mesh: MeshInstance3D
var state := "intro"
var st_t := 2.2
var pattern := ""
var target: Fighter
var flip := false
var anim := "idle"
var anim_t := 0.0
var danger := 0.0
var danger_hold := 0.0
var shell := false
var shell_t := 0.0
var shell_hp := 0.0
var shells_done := 0
var stun_t := 0.0
var enraged := false
var cds := {}
var charge_dir := Vector3.ZERO
var _hit_once := {}
var arena: Node                     # 지역 (돌기둥·레버 만드는 쪽)
var cocoons: Array = []
var pending := []                   # [남은 시간, Callable] — 예고 뒤 실행
var intro_done := false


func _ready() -> void:
	team = 1
	base_max_hp = 1500.0
	hp = base_max_hp
	body_r = 1.6
	body_h = 3.0
	collision_layer = 4
	collision_mask = 1
	var cs := CollisionShape3D.new()
	var sph := SphereShape3D.new()
	sph.radius = 0.9
	cs.shape = sph
	cs.position.y = 0.9
	add_child(cs)
	mesh = BB.make(ATLAS, "queen_idle0", {"unique": true, "up_normal": 0.75, "see_through": true})
	spr_mat = mesh.material_override
	add_child(mesh)
	add_child(BB.blob(2.6, 1.1, 1.0))
	add_to_group("boss")
	mesh.scale = Vector3(1, 0.05, 1)
	var tw := create_tween()
	tw.tween_interval(0.4)
	tw.tween_property(mesh, "scale", Vector3.ONE, 0.6).set_trans(Tween.TRANS_BACK)
	Audio.play("q_roar", global_position)
	if rig:
		rig.shake(7.0)


func _physics_process(dt: float) -> void:
	tick_status(dt)
	if dead:
		return
	st_t -= dt
	for k in cds.keys():
		cds[k] = maxf(0.0, float(cds[k]) - dt)
	_run_pending(dt)
	_pick_target()
	_danger_tick(dt)
	if shell:
		shell_t -= dt
		if shell_t <= 0.0:
			_end_shell(false)
	var want := Vector3.ZERO
	var spd := (2.3 if not enraged else 2.9) * move_mul()
	var to := Vector3.ZERO
	if target:
		to = target.global_position - global_position
		to.y = 0.0
	match state:
		"intro":
			_set_anim("ready")
			if st_t <= 0.0:
				state = "idle"
				st_t = 0.8
				intro_done = true
		"stun":
			_set_anim("hurt")
			stun_t -= dt
			if stun_t <= 0.0:
				state = "idle"
				st_t = 0.6
		"idle":
			if target:
				if to.length() > 2.2:
					want = to.normalized() * spd
				_set_anim("walk" if want != Vector3.ZERO else "idle")
				if st_t <= 0.0:
					_choose(to.length())
			else:
				_set_anim("idle")
		"busy":
			pass
		"charge":
			_set_anim("charge")
			want = charge_dir * (14.0 if not enraged else 16.0)
			for f in Combat.in_radius(get_tree(), global_position + charge_dir, 1.8):
				var fi := f as Fighter
				if fi.team != team and not _hit_once.has(fi):
					_hit_once[fi] = true
					fi.take(Hit.make(16.0, self, Combat.push(global_position, fi.global_position, 10.0, 0.4), "charge"))
			if st_t <= 0.0 or is_on_wall():
				if is_on_wall():
					Audio.play("q_wallhit", global_position)
					if rig:
						rig.shake(6.0)
					stun_t = 1.2
					state = "stun"
				else:
					state = "idle"
					st_t = 0.8
	if not is_on_floor():
		velocity.y -= GRAV * dt
	velocity.x = move_toward(velocity.x, want.x, 40.0 * dt)
	velocity.z = move_toward(velocity.z, want.z, 40.0 * dt)
	move_and_slide()
	if want.length() > 0.2:
		_face(want)
	elif target and state != "charge":
		_face(to)
	_anim_tick(dt)


# ------------------------------------------------------------------ 패턴 고르기 (상황 기반)
func _choose(dist: float) -> void:
	var near := _players_within(5.0)
	var gap := 1.0 if not enraged else 0.65
	if cocoons.filter(func(c): return is_instance_valid(c)).size() < 4 and _ready_cd("cocoon"):
		_cocoon()
		cds["cocoon"] = 14.0 * gap
	elif near >= 2 and _ready_cd("slam") or (dist < 3.0 and _ready_cd("slam") and randf() < 0.4):
		_slam()
		cds["slam"] = 7.0 * gap
	elif dist > 7.0 and _ready_cd("charge"):
		_charge()
		cds["charge"] = 6.0 * gap
	elif _ready_cd("web") and randf() < 0.5:
		_web()
		cds["web"] = 9.0 * gap
	elif dist < 2.8:
		_bite()
	elif _ready_cd("sweep"):
		_sweep()
		cds["sweep"] = 3.0 * gap
	else:
		st_t = 0.4
		return
	st_t = 0.0


func _ready_cd(k: String) -> bool:
	return float(cds.get(k, 0.0)) <= 0.0


func _busy(t: float) -> void:
	state = "busy"
	st_t = t
	pending.append([t, func():
		if state == "busy":
			state = "idle"
			st_t = 0.5 if not enraged else 0.3])


func _later(t: float, f: Callable) -> void:
	pending.append([t, f])


func _run_pending(dt: float) -> void:
	var keep: Array = []
	var due: Array = []
	for p in pending:
		p[0] -= dt
		if p[0] <= 0.0:
			due.append(p[1])
		else:
			keep.append(p)
	pending = keep
	for f in due:
		if not dead:
			(f as Callable).call()


func _bite() -> void:
	_busy(1.0)
	_set_anim("ready")
	var dir := (target.global_position - global_position).normalized() if target else Vector3(1, 0, 0)
	dir.y = 0.0
	Telegraph.rect(get_parent(), global_position + dir * 1.6, 3.2, 2.6, atan2(dir.z, dir.x), 0.55)
	Audio.play("telegraph", global_position)
	_later(0.55, func():
		_set_anim("slam")
		Audio.play("q_bite", global_position)
		for f in Combat.in_fan(get_tree(), global_position, dir, 3.2, 40.0):
			var fi := f as Fighter
			if fi.team != team:
				fi.take(Hit.make(14.0, self, Combat.push(global_position, fi.global_position, 5.0, 0.35), "bite")))


func _sweep() -> void:
	_busy(1.0)
	var at := _nearest_player_pos()
	Telegraph.circle(get_parent(), at, 3.0, 0.5)
	Audio.play("telegraph", global_position)
	_set_anim("ready")
	_later(0.5, func():
		_set_anim("slam")
		Audio.play("q_screech", global_position, -4.0)
		for f in Combat.in_radius(get_tree(), at, 3.0):
			var fi := f as Fighter
			if fi.team != team:
				fi.take(Hit.make(12.0, self, Combat.push(at, fi.global_position, 6.0, 0.3), "sweep"))
		var ring := Fx.flat_at(get_parent(), at, 0.0, ["ring_hit0", "ring_hit1", "ring_hit2", "ring_hit3"], 14.0)
		ring.scale = Vector3(2.4, 1, 2.4))


func _slam() -> void:
	_busy(1.7)
	var t := 0.9 if not enraged else 0.72
	Telegraph.circle(get_parent(), global_position, 4.5, t)
	Audio.play("telegraph", global_position)
	_set_anim("ready")
	_later(t, func():
		_set_anim("slam")
		Audio.play("q_slam", global_position)
		if rig:
			rig.shake(8.0)
		for f in Combat.in_radius(get_tree(), global_position, 4.5):
			var fi := f as Fighter
			if fi.team != team:
				var h := Hit.make(22.0, self, Combat.push(global_position, fi.global_position, 12.0, 0.6), "slam")
				h.explosive = true
				fi.take(h)
		var ring := Fx.flat_at(get_parent(), global_position, 0.0, ["ring_hit0", "ring_hit1", "ring_hit2", "ring_hit3"], 12.0)
		ring.scale = Vector3(3.6, 1, 3.6)
		for k in 6:
			var p := global_position + Vector3(cos(k), 0, sin(k)) * 3.0
			Fx.pop(get_parent(), p, ["dust0", "dust1", "dust2", "dust3"], 12.0))


func _charge() -> void:
	_busy(3.0)
	charge_dir = (target.global_position - global_position)
	charge_dir.y = 0.0
	charge_dir = charge_dir.normalized()
	var t := 0.8 if not enraged else 0.6
	Telegraph.rect(get_parent(), global_position + charge_dir * 8.0, 16.0, 3.6, atan2(charge_dir.z, charge_dir.x), t)
	Audio.play("q_charge", global_position)
	_set_anim("ready")
	_later(t, func():
		state = "charge"
		st_t = 1.1
		_hit_once.clear())


func _cocoon() -> void:
	_busy(1.6)
	_set_anim("ready")
	Audio.play("q_screech", global_position)
	var spots: Array = []
	var a0 := randf() * TAU
	for k in 3:
		var p := global_position + Vector3(cos(a0 + k * TAU / 3.0), 0, sin(a0 + k * TAU / 3.0)) * 4.0
		p.y = terrain.height_at(p) if terrain else 0.0
		spots.append(p)
		Telegraph.circle(get_parent(), p, 0.8, 1.0, Color(0.7, 0.4, 1.0))
	_later(1.0, func():
		for p in spots:
			cocoons.append(BossBits.cocoon(get_parent(), p, terrain, rig, 4.0)))


func _web() -> void:
	_busy(1.2)
	_set_anim("ready")
	var spots: Array = []
	for f in get_tree().get_nodes_in_group("fighters"):
		var o := f as Fighter
		if o.team == 0 and not o.dead and spots.size() < 3:
			spots.append(o.global_position)
	while spots.size() < 3:
		spots.append(global_position + Vector3(randf_range(-4, 4), 0, randf_range(-4, 4)))
	for p in spots:
		Telegraph.circle(get_parent(), p, 2.8, 0.7, Color(0.9, 0.9, 1.0))
	Audio.play("web_shot", global_position)
	_later(0.7, func():
		for p in spots:
			BossBits.web_field(get_parent(), p, 2.8, self))


# ------------------------------------------------------------------ 위험 게이지
func _danger_tick(dt: float) -> void:
	if not intro_done or shell:
		return
	if danger_hold > 0.0:
		danger_hold -= dt
		return
	danger += dt / 45.0 + _players_within(5.0) * dt / 30.0
	if danger >= 1.0:
		danger = 0.0
		danger_hold = 3.5
		Audio.play("q_hiss", global_position)
		Telegraph.circle(get_parent(), global_position, 8.0, 2.0, Color(0.6, 1.0, 0.3))
		_later(2.0, func():
			Audio.play("q_burst", global_position)
			if rig:
				rig.shake(10.0)
			for f in Combat.in_radius(get_tree(), global_position, 8.0):
				var fi := f as Fighter
				if fi.team != team:
					var h := Hit.make(35.0, self, Combat.push(global_position, fi.global_position, 14.0, 0.6), "burst")
					h.explosive = true
					h.poison = 3.0
					fi.take(h)
			var fx := Fx.pop(get_parent(), global_position, ["boom0", "boom1", "boom2", "boom3", "boom4", "boom5"], 12.0)
			fx.mesh.scale = Vector3(3.2, 3.2, 3.2)
			fx.mesh.material_override.set_shader_parameter("tint", Color(0.7, 1.0, 0.5)))


# ------------------------------------------------------------------ 맞기 · 껍질 · 광폭화 · 쓰러짐
func take(h: Hit) -> void:
	if dead or h.heal:
		super.take(h)
		return
	var mul := 1.0
	var high := h.source is Node3D and (h.source as Node3D).global_position.y > global_position.y + 1.2
	if shell:
		if high:
			mul = 2.5
			shell_hp -= h.amount * 2.5
			Audio.play("q_weak", global_position)
			Fx.pop(get_parent(), global_position + Vector3(-1.0, 2.6, 0), ["spark0", "spark1", "spark2"], 16.0)
			if shell_hp <= 0.0:
				_end_shell(true)
		else:
			mul = 0.15
			Audio.play("q_clink", global_position, -4.0)
	elif state == "stun":
		mul = 1.5
	h.amount *= mul
	h.knock = Vector3.ZERO           # 여왕은 밀리지 않는다
	super.take(h)
	# 단계
	var r := ratio()
	if shells_done == 0 and r < 0.66 and not shell:
		_start_shell()
	elif shells_done == 1 and r < 0.33 and not shell:
		_start_shell()
	if r < 0.3 and not enraged:
		enraged = true
		Audio.play("q_roar", global_position)
		Audio.set_intensity(1.0)
		spr_mat.set_shader_parameter("tint", Color(1.0, 0.75, 0.75))
		phase_changed.emit(self, "enrage")


func _start_shell() -> void:
	shell = true
	shell_t = 14.0
	shell_hp = 90.0
	shells_done += 1
	pending.clear()
	state = "idle"
	st_t = 1.5
	Audio.play("q_shield", global_position)
	phase_changed.emit(self, "shell")
	if arena and arena.has_method("raise_pillars"):
		arena.raise_pillars(true)


func _end_shell(broken: bool) -> void:
	shell = false
	if broken:
		Audio.play("q_shatter", global_position)
		stun_t = 3.0
		state = "stun"
		pending.clear()
		if rig:
			rig.shake(8.0)
		Combat.hitstop(get_tree(), 0.1)
		for k in 8:
			Fx.pop(get_parent(), global_position + Vector3(randf_range(-2, 2), randf_range(1, 3), 0), ["spark0", "spark1", "spark2"], 10.0, Vector3(randf_range(-3, 3), 3, 0))
	phase_changed.emit(self, "shell_end")
	if arena and arena.has_method("raise_pillars"):
		arena.raise_pillars(false)


func _die() -> void:
	super._die()
	pending.clear()
	Audio.play("q_die", global_position)
	if rig:
		rig.shake(12.0)
	Combat.hitstop(get_tree(), 0.15)
	for c in cocoons:
		if is_instance_valid(c):
			(c as Fighter).take(Hit.make(999.0, null))
	_set_anim("hurt")
	_anim_tick(0.0)
	var tw := create_tween()
	tw.tween_property(mesh, "modulate", Color(1, 1, 1, 1), 0.01)
	tw.tween_property(mesh, "scale", Vector3(1.15, 0.5, 1.0), 0.8)
	defeated.emit(self)


# ------------------------------------------------------------------ 도우미
func _pick_target() -> void:
	var best: Fighter = null
	var bd := 1e9
	for f in get_tree().get_nodes_in_group("fighters"):
		var o := f as Fighter
		if o.team == team or o.dead:
			continue
		var d := o.global_position.distance_to(global_position)
		if d < bd:
			bd = d
			best = o
	target = best


func _players_within(r: float) -> int:
	var n := 0
	for f in get_tree().get_nodes_in_group("fighters"):
		var o := f as Fighter
		if o.team != team and not o.dead and o is Player and o.global_position.distance_to(global_position) < r:
			n += 1
	return n


func _nearest_player_pos() -> Vector3:
	return target.global_position if target else global_position


func _face(v: Vector3) -> void:
	var right := Vector3(1, 0, 0).rotated(Vector3.UP, rig.yaw if rig else 0.0)
	var sx := v.dot(right)
	if absf(sx) > 0.05:
		flip = sx < 0.0


func _set_anim(a: String) -> void:
	if a != anim:
		anim = a
		anim_t = 0.0


func _anim_tick(dt: float) -> void:
	anim_t += dt
	var counts := {"idle": 2, "walk": 4, "ready": 1, "slam": 1, "charge": 2, "hurt": 1}
	var fps := {"idle": 2.5, "walk": 7.0, "ready": 1.0, "slam": 1.0, "charge": 10.0, "hurt": 1.0}
	var a := anim
	if flash > 0.08 and state != "busy" and state != "charge":
		a = "hurt"
	var i: int = int(anim_t * float(fps[a])) % int(counts[a])
	var pre := "queenS_" if shell else "queen_"
	spr_mat.set_shader_parameter("region", BB.region_uv(ATLAS, "%s%s%d" % [pre, a, i]))
	spr_mat.set_shader_parameter("flip", 1.0 if flip else 0.0)
