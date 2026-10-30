class_name Player
extends Fighter
## 플레이어 캐릭터: 이동(탑다운·측면) + 직업(기본 공격 · Q 스킬 · 패시브).
##  · 입력은 PlayerInput 으로만 받는다 (온라인에서 남의 입력도 같은 길로)
##  · 탑다운: WASD 8방향, Shift 달리기, Space 구르기(짧은 무적 0.18초)
##  · 측면: A/D, Space 점프 (코요테 타임·입력 미리 받기·떼면 낮게)
##  · 조준: 마우스 → 패드 오른스틱 → 보는 방향
## 직업 수치는 DB.JOBS, 근거는 직업 시스템.md · 전투 시스템.md

signal job_changed(p: Player)
signal skill_used(p: Player)

const WALK := 3.4
const RUN := 5.6
const ROLL := 9.0
const ROLL_T := 0.32
const JUMP := 8.6
const COYOTE := 0.1
const BUFFER := 0.12

var ctrl := PlayerInput.new()
var local := true
var job := "warrior"
var J: Dictionary
var spr: CharSprite
var shadow: MeshInstance3D
var ring: MeshInstance3D                # 발밑 팀색 고리 + 조준 표시
var team_color := Color("#ffe066")
var auto_dir := Vector3.ZERO          # 자동 걷기 (영상용)
var roll_t := 0.0
var roll_dir := Vector3.ZERO
var facing := Vector3(0, 0, 1)
var aim := Vector3(0, 0, 1)
var aim_point := Vector3.ZERO
var attack_t := 0.0
var attack_cd := 0.0
var skill_cd := 0.0
var spawn_point := Vector3.ZERO
var respawn_t := 0.0
var _coyote := 0.0
var _buffer := 0.0
var _was_floor := true
var _step_t := 0.0
var _fall_v := 0.0
# 패시브 상태
var _passive_t := 0.0
var _blast_t := 0.0                   # 폭풍 질주 남은 시간
var _still_t := 0.0                   # 궁수 집중


func _ready() -> void:
	team = 0
	collision_layer = 2
	collision_mask = 1
	floor_snap_length = 0.3
	floor_max_angle = deg_to_rad(50)
	body_r = 0.28
	body_h = 1.4
	var cs := CollisionShape3D.new()
	var cap := CapsuleShape3D.new()
	cap.radius = 0.28
	cap.height = 0.9
	cs.shape = cap
	cs.position.y = 0.45
	add_child(cs)
	shadow = BB.blob(0.46, 0.24, 1.0)
	add_child(shadow)
	var q := QuadMesh.new()
	q.size = Vector2(1.5, 1.5)
	q.orientation = PlaneMesh.FACE_Y
	ring = MeshInstance3D.new()
	ring.mesh = q
	var rm := ShaderMaterial.new()
	rm.shader = load("res://shaders/foot_ring.gdshader")
	rm.set_shader_parameter("size_px", Vector2(24, 24))
	rm.set_shader_parameter("col", team_color)
	ring.material_override = rm
	ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	ring.position.y = 0.03
	add_child(ring)
	set_job(job)
	damaged.connect(_on_damaged)


func set_job(j: String) -> void:
	job = j
	J = DB.JOBS[j]
	if spr:
		spr.mesh.queue_free()
	spr = CharSprite.new(String(J["sprite"]))
	add_child(spr.mesh)
	spr_mat = spr.mat
	mods.clear()
	base_max_hp = float(J["hp"])
	hp = max_hp()
	skill_cd = 0.0
	job_changed.emit(self)


func side() -> bool:
	return rig != null and rig.side_goal > 0.5


# ------------------------------------------------------------------ 매 프레임
func _physics_process(dt: float) -> void:
	tick_status(dt)
	if dead:
		_dead_tick(dt)
		return
	if local:
		ctrl.poll_local(rig, global_position.y + 0.5)
	var sd := side()
	var yaw := rig.yaw if rig else 0.0
	var fwd := Vector3(0, 0, -1).rotated(Vector3.UP, yaw)
	var right := Vector3(1, 0, 0).rotated(Vector3.UP, yaw)
	var dir := right * ctrl.move.x - fwd * ctrl.move.y
	if sd:
		dir = right * ctrl.move.x
	if auto_dir != Vector3.ZERO:
		dir = auto_dir
	if dir.length() > 1.0:
		dir = dir.normalized()
	_update_aim(right, fwd, sd)
	attack_t = maxf(0.0, attack_t - dt)
	attack_cd = maxf(0.0, attack_cd - dt)
	skill_cd = maxf(0.0, skill_cd - dt)
	_passives(dt, dir)
	var spd := (RUN if ctrl.run else WALK) * float(J["speed"]) * move_mul()
	if attack_t > 0.0:
		spd *= 0.4
	# ---- 중력·점프
	if is_on_floor():
		_coyote = COYOTE
	else:
		_coyote = maxf(0.0, _coyote - dt)
		velocity.y -= GRAV * dt
		_fall_v = minf(_fall_v, velocity.y)
	_buffer = maxf(0.0, _buffer - dt)
	if sd and ctrl.roll:
		_buffer = BUFFER
	if sd and _buffer > 0.0:
		jump()
	if sd and not ctrl.roll_held and velocity.y > 2.5 and auto_dir == Vector3.ZERO:
		velocity.y = move_toward(velocity.y, 2.5, GRAV * 2.0 * dt)
	# ---- 수평 이동
	if roll_t > 0.0:
		roll_t -= dt
		velocity.x = roll_dir.x * ROLL
		velocity.z = roll_dir.z * ROLL
	else:
		var want := dir * spd
		var acc := 30.0 if dir != Vector3.ZERO else 24.0
		velocity.x = move_toward(velocity.x, want.x, acc * dt)
		velocity.z = move_toward(velocity.z, want.z, acc * dt)
		if not sd and ctrl.roll and dir != Vector3.ZERO:
			roll_t = ROLL_T
			roll_dir = dir.normalized()
			invuln = maxf(invuln, 0.18)
			Audio.play("dash", global_position)
			Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 16.0)
	# ---- 공격 · 스킬
	if ctrl.attack_held and attack_cd <= 0.0 and roll_t <= 0.0:
		_basic()
	if ctrl.skill and skill_cd <= 0.0 and roll_t <= 0.0:
		_skill()
	if ctrl.interact:
		for n in get_tree().get_nodes_in_group("interact"):
			if (n as Node3D).global_position.distance_to(global_position) < float(n.get("reach")):
				n.interact(self)
				break
	if ctrl.switch_job:
		var i := DB.JOB_ORDER.find(job)
		set_job(DB.JOB_ORDER[(i + 1) % DB.JOB_ORDER.size()])
		Audio.play("ui_toggle")
	if dir != Vector3.ZERO and attack_t <= 0.0:
		facing = dir.normalized()
	# ---- 넉백 · 측면 선
	velocity.x += kv.x
	velocity.z += kv.z
	if kv.y > 0.5:
		velocity.y = maxf(velocity.y, kv.y)
		kv.y = 0.0
	if sd and rig:
		var n := rig.lane_normal()
		var off := global_position.dot(n) - rig.lane_z
		velocity -= n * velocity.dot(n)
		velocity -= n * off * 8.0
	move_and_slide()
	velocity.x -= kv.x
	velocity.z -= kv.z
	_after_move(dt)


func _update_aim(right: Vector3, fwd: Vector3, sd: bool) -> void:
	if ctrl.aim_stick != Vector2.ZERO:
		aim = (right * ctrl.aim_stick.x - fwd * ctrl.aim_stick.y).normalized()
		aim_point = global_position + aim * 5.0
	elif ctrl.has_aim:
		aim_point = ctrl.aim_world
		var d := ctrl.aim_world - global_position
		d.y = 0.0
		if sd:
			aim = right * (1.0 if d.dot(right) >= 0.0 else -1.0)
		elif d.length() > 0.2:
			aim = d.normalized()
	else:
		aim = facing
		aim_point = global_position + aim * 4.0
	if sd:
		aim_point.z = global_position.z


func _after_move(dt: float) -> void:
	var on_floor := is_on_floor()
	if on_floor and not _was_floor:
		var hard := _fall_v < -9.0
		Audio.play("land_heavy" if hard else "land", global_position)
		Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 16.0)
		if hard and rig:
			rig.shake(2.0)
		_fall_v = 0.0
	_was_floor = on_floor
	var hv := Vector2(velocity.x, velocity.z).length()
	if on_floor and hv > 0.5 and roll_t <= 0.0:
		_step_t -= dt * hv / WALK
		if _step_t <= 0.0:
			_step_t = 0.34
			Audio.play(_step_sound(), global_position)
			if ctrl.run:
				Fx.pop(get_parent(), global_position - facing * 0.2, ["dust1", "dust2", "dust3"], 14.0)
	# 떨어짐 (지도 밖 · 구덩이)
	if global_position.y < -6.0:
		hp = 0.0
		_die()
		return
	spr.face(aim if attack_t > 0.0 else facing, rig.yaw if rig else 0.0)
	(ring.material_override as ShaderMaterial).set_shader_parameter("aim", atan2(aim.z, aim.x))
	ring.visible = not side()
	if flash > 0.08:
		spr.play("hurt")
	elif attack_t > 0.0:
		spr.play("attack")
	elif roll_t > 0.0:
		spr.play("roll")
	elif not on_floor:
		spr.play("jump" if velocity.y > 0.0 else "fall")
	elif hv > 0.4:
		spr.play("run" if ctrl.run else "walk")
	else:
		spr.play("idle")
	spr.update(dt)


## 튀어 오르기 (점프 발판·폭발 등): v 는 위로, h 는 수평 밀기
func launch(v: Vector3, h: Vector3) -> void:
	velocity.y = v.y
	kv += h
	_coyote = 0.0
	Audio.play("double_jump", global_position)


func jump() -> void:
	if is_on_floor() or _coyote > 0.0:
		velocity.y = JUMP
		_coyote = 0.0
		_buffer = 0.0
		Audio.play("jump", global_position)
		Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 16.0)


func _step_sound() -> String:
	if terrain == null:
		return "step_grass"
	return "step_stone" if terrain.mat_at(global_position) == Region.G.STONE else "step_grass"


# ------------------------------------------------------------------ 맞기 · 죽기 · 부활
func _on_damaged(_f: Fighter, h: Hit, amt: float) -> void:
	if amt > 0.0:
		Audio.play("hurt", global_position)
		invuln = maxf(invuln, 0.45)
		if rig and local:
			rig.shake(2.5)
	# 폭탄마 패시브: 폭발에 맞으면 빨라진다 (자기 폭탄도)
	if job == "bomber" and h.explosive:
		_blast_t = 3.0
		set_mod("blast", {"speed": 0.8})
		Fx.pop(get_parent(), global_position, ["spark0", "spark1", "spark2"], 14.0)


func _die() -> void:
	super._die()
	Audio.play("die", global_position)
	spr.play("hurt")
	spr.update(0.0)
	respawn_t = 1.6
	velocity = Vector3.ZERO
	kv = Vector3.ZERO
	var tw := create_tween()
	tw.tween_property(spr.mesh, "scale", Vector3(1.3, 0.2, 1.0), 0.25)


func _dead_tick(dt: float) -> void:
	respawn_t -= dt
	if respawn_t <= 0.0:
		var party := get_tree().get_first_node_in_group("party")
		if party and not party.use_life(self):
			respawn_t = INF          # 부활이 다 떨어졌다: 관전 (다음 지역·재시작 때 되살아남)
			return
		revive(spawn_point)


func revive(at: Vector3) -> void:
	dead = false
	hp = max_hp()
	poison_t = 0.0
	slow_t = 0.0
	global_position = at + Vector3(0, 0.2, 0)
	velocity = Vector3.ZERO
	spr.mesh.scale = Vector3.ONE
	invuln = 1.5
	Audio.play("revive", global_position)
	Fx.flat_at(get_parent(), global_position, 0.0, ["ring_heal0", "ring_heal1", "ring_heal2", "ring_heal3"], 12.0)


# ------------------------------------------------------------------ 직업: 패시브
func _passives(dt: float, dir: Vector3) -> void:
	_passive_t -= dt
	match job:
		"warrior":
			# 역경의 투지: 건강하면 약하고, 죽기 직전에 가장 강하다
			if _passive_t <= 0.0:
				_passive_t = 0.2
				var r := ratio()
				var m := -0.2 if r > 0.7 else (0.6 if r < 0.35 else 0.0)
				set_mod("rage", {"attack": m} if m != 0.0 else {})
				if spr_mat and poison_t <= 0.0:
					spr_mat.set_shader_parameter("tint", Color(1.0, 0.78, 0.74) if m > 0.0 else Color.WHITE)
		"bomber":
			if _blast_t > 0.0:
				_blast_t -= dt
				if _blast_t <= 0.0:
					set_mod("blast", {})
		"doctor":
			# 응급 분류: 위급한 아군이 가까우면 빨라지지만 최대 체력이 준다
			if _passive_t <= 0.0:
				_passive_t = 0.3
				var crit := false
				for f in get_tree().get_nodes_in_group("fighters"):
					var o := f as Fighter
					if o != self and o.team == team and not o.dead and o.ratio() < 0.4 and o.global_position.distance_to(global_position) < 8.0:
						crit = true
						break
				set_mod("triage", {"speed": 0.5, "max_hp": -0.25} if crit else {})
		"archer":
			# 집중: 멈춰 있을수록 강해진다
			if dir == Vector3.ZERO and is_on_floor():
				_still_t += dt
			else:
				_still_t = 0.0
			set_mod("focus", {"attack": minf(0.6, floorf(_still_t / 0.5) * 0.15)} if _still_t >= 0.5 else {})


# ------------------------------------------------------------------ 직업: 기본 공격
func _basic() -> void:
	var b: Dictionary = J["basic"]
	attack_cd = 1.0 / float(b["rate"])
	attack_t = 0.22
	spr.play("attack", true)
	var atk := stat("attack")
	match String(b["kind"]):
		"melee":
			Audio.play("swing", global_position)
			kv += aim * 2.4
			var c := global_position + aim * 0.35
			var hit_any := false
			for f in Combat.in_fan(get_tree(), c, aim, float(b["range"]), float(b["arc"]) * 0.5):
				if f == self:
					continue
				var fi := f as Fighter
				fi.take(Hit.make(float(b["dmg"]) * atk, self, Combat.push(c, fi.global_position, float(b["knock"]), 0.25), "slash"))
				hit_any = true
			if hit_any:
				Audio.play("sword_hit", global_position + aim)
				Combat.hitstop(get_tree(), 0.035)
			_slash_fx()
		"bomb":
			Audio.play("throw", global_position)
			var bm := Bomb.throw(get_parent(), global_position + Vector3(0, 1.0, 0) + aim * 0.3, _lob(0.55, float(b["range"])), self, false)
			bm.fuse = float(b["fuse"])
			bm.fuse0 = bm.fuse
			bm.radius = float(b["radius"])
			bm.dmg = float(b["dmg"]) * atk
			bm.knock = float(b["knock"])
		"syringe":
			Audio.play("syringe", global_position)
			var p := Projectile.fire(get_parent(), global_position + Vector3(0, 0.75, 0), aim, float(b["speed"]), 9.0, Fx.ATLAS, "syringe", self, null)
			p.hits_allies = true
			var dmg := float(b["dmg"]) * atk
			var heal := float(b["heal"])
			var poison := float(b["poison"])
			p.on_hit = func(fi: Fighter, _pr):
				if fi.team == team:
					var h := Hit.make(heal, self)
					h.heal = true
					fi.take(h)
					Audio.play("heal_get", fi.global_position)
					Fx.pop(fi.get_parent(), fi.center(), ["plus0", "plus1", "plus2"], 10.0, Vector3(0, 1.2, 0))
				else:
					var h2 := Hit.make(dmg, self, Vector3.ZERO, "syringe")
					h2.poison = poison
					fi.take(h2)
					Audio.play("inject", fi.global_position)
		"arrow":
			Audio.play("throw", global_position)
			var h3 := Hit.make(float(b["dmg"]) * atk, self, aim * float(b["knock"]), "arrow")
			var pa := Projectile.fire(get_parent(), global_position + Vector3(0, 0.75, 0), aim, float(b["speed"]), float(b["range"]), Fx.ATLAS, "arrow", self, h3)
			pa.hits_allies = true


## 포물선 투척 속도: t 초 만에 조준점(최대 max_d)에 떨어지게
func _lob(t: float, max_d: float) -> Vector3:
	var target := aim_point
	var d := target - global_position
	d.y = 0.0
	if d.length() > max_d:
		d = d.normalized() * max_d
	if d.length() < 0.8:
		d = aim * 0.8
	var land_y := terrain.height_at(global_position + d) if terrain else global_position.y
	var dy := land_y - (global_position.y + 1.0)
	return Vector3(d.x / t, (dy + 0.5 * Bomb.GRAV * t * t) / t, d.z / t)


func _slash_fx() -> void:
	var frames := ["slash0", "slash1", "slash2", "slash3"]
	if side():
		var f := Fx.pop(get_parent(), global_position + aim * 0.7 + Vector3(0, -0.75, 0), frames, 18.0)
		if aim.dot(Vector3(1, 0, 0).rotated(Vector3.UP, rig.yaw)) < 0.0:
			f.mesh.scale.x = -1.0
	else:
		Fx.flat_at(get_parent(), global_position + aim * 0.55, atan2(aim.z, aim.x), frames, 18.0)


# ------------------------------------------------------------------ 직업: Q 스킬
func _skill() -> void:
	var s: Dictionary = J["skill"]
	skill_cd = float(s["cd"])
	attack_t = 0.3
	spr.play("attack", true)
	skill_used.emit(self)
	var atk := stat("attack")
	match job:
		"warrior":
			# 강타: 넓은 부채꼴 · 아군도 맞음 · 자기 체력 10% · 반동
			Audio.play("smash", global_position)
			var c := global_position + aim * 0.6
			for f in Combat.in_fan(get_tree(), c, aim, float(s["radius"]), float(s["half"])):
				if f == self:
					continue
				var fi := f as Fighter
				fi.take(Hit.make(float(s["dmg"]) * atk, self, Combat.push(c, fi.global_position, float(s["knock"]), 0.5), "smash"))
			hp = maxf(1.0, hp - hp * float(s["self_cost"]))
			kv -= aim * float(s["recoil"])
			var ring := Fx.flat_at(get_parent(), c, 0.0, ["ring_hit0", "ring_hit1", "ring_hit2", "ring_hit3"], 14.0)
			ring.scale = Vector3(2.4, 1, 2.4)
			_slash_fx()
			if rig:
				rig.shake(5.0)
			Combat.hitstop(get_tree(), 0.06)
		"bomber":
			Audio.play("throw", global_position)
			var bm := Bomb.throw(get_parent(), global_position + Vector3(0, 1.1, 0) + aim * 0.3, _lob(0.75, 7.0), self, true)
			bm.fuse = float(s["fuse"])
			bm.fuse0 = bm.fuse
			bm.radius = float(s["radius"])
			bm.dmg = float(s["dmg"]) * atk
			bm.knock = float(s["knock"])
			bm.ff_cur = float(s["ff_cur"])
			bm.self_ratio = float(s["self"])
		"doctor":
			# 수혈 파동: 주변 아군 회복, 살린 수만큼 내 체력을 낸다
			Audio.play("heal_pulse", global_position)
			var n := 0
			for f in Combat.in_radius(get_tree(), global_position, float(s["radius"])):
				var fi := f as Fighter
				if fi == self or fi.team != team:
					continue
				var h := Hit.make(float(s["heal"]), self)
				h.heal = true
				fi.take(h)
				Fx.pop(fi.get_parent(), fi.center(), ["plus0", "plus1", "plus2"], 10.0, Vector3(0, 1.2, 0))
				n += 1
			hp = maxf(1.0, hp - n * float(s["cost"]))
			var ring2 := Fx.flat_at(get_parent(), global_position, 0.0, ["ring_heal0", "ring_heal1", "ring_heal2", "ring_heal3"], 12.0)
			ring2.scale = Vector3(2.8, 1, 2.8)
		"archer":
			# 화살비: 조준점에 잠깐 뒤 여러 번 — 아군 머리 위에도 떨어진다
			var at := aim_point
			var d := at - global_position
			d.y = 0.0
			if d.length() > 10.0:
				at = global_position + d.normalized() * 10.0
			at.y = terrain.height_at(at) if terrain else global_position.y
			_arrow_rain(at, s, atk)


func _arrow_rain(at: Vector3, s: Dictionary, atk: float) -> void:
	var mark := Fx.flat_at(get_parent(), at, 0.0, ["mark0", "mark1", "mark0", "mark1", "mark0", "mark1", "mark0", "mark1", "mark0", "mark1", "mark0", "mark1"], 6.0)
	mark.scale = Vector3(float(s["radius"]) / 1.25, 1, float(s["radius"]) / 1.25)
	Audio.play("throw", global_position)
	var ticks := int(s["ticks"])
	var gap := float(s["time"]) / ticks
	await get_tree().create_timer(0.35, false).timeout
	for i in ticks:
		if not is_inside_tree():
			return
		for f in Combat.in_radius(get_tree(), at, float(s["radius"])):
			(f as Fighter).take(Hit.make(float(s["dmg"]) * atk, self, Vector3.ZERO, "arrow"))
		for k in 2:
			var p := at + Vector3(randf_range(-1, 1), 0, randf_range(-1, 1)).limit_length(1.0) * float(s["radius"])
			Fx.pop(get_parent(), p, ["spark0", "spark1", "spark2"], 20.0)
		Audio.play("sword_hit", at, -8.0)
		await get_tree().create_timer(gap, false).timeout
