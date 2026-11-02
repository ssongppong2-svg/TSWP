class_name Player
extends Fighter
## 플레이어 캐릭터: 이동(탑다운·측면) + 직업(기본 공격 · Q 스킬 · R 궁극기 · 패시브) + 가방(장비 5 · 소비 1).
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
var slot := 0                         # 파티 안 번호 (기록·팀색 — 온라인이면 접속 순서)
var nick := "용사"                    # 닉네임 (문서: 스팀 닉네임 — 지금은 설정의 이름)
var look := {}                        # 꾸미기 (CharLook) — 1P 는 설정값, 나머지는 무작위
var pad := -1                         # 로컬 협동: 이 사람의 패드 번호 (-1 = 키보드·마우스 1P)
var carrying: Fighter                 # 들고 있는 것 (아군·적)
var carried_by: Player                # 나를 들고 있는 사람
var _carry_t := 0.0
var _mash := 0                        # 들렸을 때 버둥거린 횟수 (5번이면 빠져나온다)
var leash := false                    # 로컬 협동: 화면 밖으로 못 나가게
var nameplate: Label3D
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
var _ghost_t := 0.0
# 아이템 · 궁극기
var inv: Inventory                    # 가방 (Party 가 파티 번호별로 들고 있음 — 지역을 옮겨도 이어짐)
var ult := 0.0                        # 궁극기 게이지 (0 ~ DB.ULT_MAX)
var _gold_frac := 0.0
var _pact := 0                        # 피의 계약서 쌓인 수 (이 지역)
var _pact_t := 0.0
var _leap := false                    # 영웅의 낙하: 뛰어오른 중
var _leap_t := 0.0
var _ult_lock := 0.0                  # 궁극기 시전 중 (궁수 겨누기)
var crown: MeshInstance3D             # 저주받은 왕관 (머리 위)
var _was_hidden := false
var _shove_t := 0.0
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
	# 내 캐릭터 둘레 은은한 빛 — 어두운 곳에서도 내가 어디 있는지 (시각적 위계)
	var pl := OmniLight3D.new()
	pl.light_color = Color("#fff0d0")
	pl.light_energy = 0.45
	pl.omni_range = 3.2
	pl.omni_attenuation = 1.8
	pl.position = Vector3(0, 1.2, 0.6)
	add_child(pl)
	var party := get_tree().get_first_node_in_group("party")
	inv = party.inv_of(slot) if party else Inventory.new()
	inv.changed.connect(_apply_items)
	crown = BB.make("res://assets/sprites/loot.png", "crown_hat", {"unique": true, "receive_shade": false, "emission": 0.4})
	crown.position.y = 1.55
	crown.visible = false
	add_child(crown)
	set_job(job)
	damaged.connect(_on_damaged)


func set_job(j: String) -> void:
	job = j
	J = DB.JOBS[j]
	if spr:
		spr.mesh.queue_free()
	spr = CharSprite.new(String(J["sprite"]))
	add_child(spr.mesh)
	spr.set_look(look)
	spr_mat = spr.mat
	mods.clear()
	base_max_hp = float(J["hp"])
	_apply_items()
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
	elif pad >= 0:
		ctrl.poll_pad(pad, self)
	if carried_by:
		_carried_tick(dt)
		return
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
	if stun_t > 0.0:
		dir = Vector3.ZERO                       # 넘어짐: 아무것도 못 한다
		ctrl.clear()
	_update_aim(right, fwd, sd)
	_items_tick(dt)
	attack_t = maxf(0.0, attack_t - dt)
	attack_cd = maxf(0.0, attack_cd - dt)
	skill_cd = maxf(0.0, skill_cd - dt)
	_passives(dt, dir)
	var spd := (RUN if ctrl.run else WALK) * float(J["speed"]) * move_mul()
	if attack_t > 0.0:
		spd *= 0.4
	if carrying:
		spd *= 0.75
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
		_ghost_t -= dt
		if _ghost_t <= 0.0:
			_ghost_t = 0.05
			spr.ghost(get_parent(), global_position)
		velocity.x = roll_dir.x * ROLL * stat("roll")
		velocity.z = roll_dir.z * ROLL * stat("roll")
	else:
		var want := dir * spd
		var acc := 30.0 if dir != Vector3.ZERO else 24.0
		if _leap:
			acc = 0.0                            # 영웅의 낙하: 날아가는 중엔 방향을 못 바꾼다
		elif _ult_lock > 0.0:
			want = Vector3.ZERO
			acc = 40.0
		velocity.x = move_toward(velocity.x, want.x, acc * dt)
		velocity.z = move_toward(velocity.z, want.z, acc * dt)
		if not sd and ctrl.roll and dir != Vector3.ZERO:
			roll_t = ROLL_T
			roll_dir = dir.normalized()
			spr.stretch(-0.8)
			invuln = maxf(invuln, 0.18 * stat("iframe"))
			_shove_t = ROLL_T
			if inv and inv.fx("roll_stealth") > 0:
				hidden_t = maxf(hidden_t, ROLL_T + 1.0)
			Audio.play("dash", global_position)
			Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 16.0)
	# ---- 공격 · 스킬
	if carrying and ctrl.attack:
		_throw()
	elif carrying:
		pass
	elif ctrl.attack_held and attack_cd <= 0.0 and roll_t <= 0.0 and _ult_lock <= 0.0 and not _leap:
		_basic()
	if ctrl.skill and skill_cd <= 0.0 and roll_t <= 0.0 and _ult_lock <= 0.0 and not _leap:
		_skill()
	if ctrl.ult and roll_t <= 0.0 and not _leap:
		if ult >= DB.ULT_MAX:
			_ult()
		elif local:
			Audio.play("ui_denied")
	if ctrl.use_item:
		use_consumable()
	if ctrl.emote:
		emote("lol")
	if ctrl.ping:
		get_tree().call_group("game", "ping_at", self, aim_point)
	if ctrl.interact:
		# 가장 가까운 것 하나 (아이템·상자·진열대·표지가 겹쳐 있어도 손 닿는 것부터)
		var pick: Node = null
		var pd := 1e9
		for n in get_tree().get_nodes_in_group("interact"):
			var d := (n as Node3D).global_position.distance_to(global_position)
			if d < float(n.get("reach")) and d < pd:
				pd = d
				pick = n
		if carrying:
			_throw()
		elif pick:
			pick.interact(self)
		else:
			_try_grab()
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
	if leash:
		_leash()
	move_and_slide()
	velocity.x -= kv.x
	velocity.z -= kv.z
	if _shove_t > 0.0:
		_shove_t -= dt
		_shove()
	if carrying:
		_carry_hold(dt)
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
		spr.stretch(clampf(_fall_v / 12.0, -1.6, -0.5))
		Audio.play("land_heavy" if hard else "land", global_position)
		Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 16.0)
		if hard and rig:
			rig.shake(2.0)
		_fall_v = 0.0
	if _leap:
		_leap_t += dt
		if on_floor and _leap_t > 0.15:
			_leap_land()
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
	# 가상 뼈: 달리는 쪽으로 몸이 기운다
	var scr_right := Vector3(1, 0, 0).rotated(Vector3.UP, rig.yaw if rig else 0.0)
	spr.lean_to(velocity.dot(scr_right) * 0.022)
	(ring.material_override as ShaderMaterial).set_shader_parameter("aim", atan2(aim.z, aim.x))
	ring.visible = not side()
	if flash > 0.08 or stun_t > 0.0:
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
		spr.stretch(1.0)
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
		var src := h.source as Node3D
		var side_k := 1.0
		if src and is_instance_valid(src):
			var scr_r := Vector3(1, 0, 0).rotated(Vector3.UP, rig.yaw if rig else 0.0)
			side_k = signf((global_position - src.global_position).dot(scr_r))
		spr.wobble(side_k, 1.6)
		Particles.burst(get_parent(), center(), Color("#ffd0c0"), Color("#c03030"), 10, 3.5)
		spr.stretch(-0.6)
		if rig and local:
			rig.shake(2.5)
			rig.kick(Vector2(side_k * 3.0, -1.5))
			get_tree().call_group("hud", "hurt_flash", clampf(amt / 30.0, 0.3, 1.0))
	if amt > 0.0 and inv:
		_items_on_hurt(h, amt)
	# 폭탄마 패시브: 폭발에 맞으면 빨라진다 (자기 폭탄도)
	if job == "bomber" and h.explosive:
		_blast_t = 3.0
		set_mod("blast", {"speed": 0.8})
		Fx.pop(get_parent(), global_position, ["spark0", "spark1", "spark2"], 14.0)


func _die() -> void:
	super._die()
	get_tree().call_group("hud", "notify", "%s 쓰러짐!" % nick, Color("#ff8a7a"))
	if local:
		Achievements.add(get_tree(), "deaths")
	_leap = false
	_ult_lock = 0.0
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
	# 불사조 깃털: 부활 횟수를 쓰지 않고 그 자리에서 (깃털은 타서 없어짐)
	if respawn_t <= 0.8 and respawn_t > -1.0 and inv and inv.fx("revive") > 0:
		for id in inv.slots:
			if "revive" in (Items.get_item(id).get("fx", []) as Array):
				inv.remove(id)
				break
		revive(global_position)
		hp = max_hp() * 0.5
		Particles.burst(get_parent(), center(), Color("#fff0a0"), Color("#f06a2a"), 30, 6.0)
		if local:
			get_tree().call_group("hud", "toast_text", "불사조 깃털이 타올랐다!", Color("#ffb040"))
		return
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
	get_tree().call_group("hud", "notify", "%s 다시 일어남" % nick, Color("#9aff9a"))
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
	attack_cd = 1.0 / (float(b["rate"]) * stat("atk_rate"))
	attack_t = 0.26
	var scr_r := Vector3(1, 0, 0).rotated(Vector3.UP, rig.yaw if rig else 0.0)
	spr.wobble(1.0 if aim.dot(scr_r) >= 0.0 else -1.0, 0.8)
	spr.stretch(-0.35)
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
	skill_cd = float(s["cd"]) * stat("cd")
	skill_used.emit(self)
	_skill_cast(1.0)
	# 메아리 종: 잠깐 뒤 절반 위력으로 한 번 더
	if inv and inv.fx("echo") > 0:
		await get_tree().create_timer(0.7, false).timeout
		if is_inside_tree() and not dead:
			Audio.play("crystal_ping", global_position, -4.0)
			_skill_cast(0.5)


func _skill_cast(power: float) -> void:
	var s: Dictionary = J["skill"]
	attack_t = 0.3
	spr.play("attack", true)
	var atk := stat("attack") * power
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


# ------------------------------------------------------------------ 아이템
## 가방이 바뀌면 능력치 다시 계산 (출처 "items" 하나로)
func _apply_items() -> void:
	if inv == null:
		return
	set_mod("items", inv.mods())
	if crown:
		crown.visible = inv.fx("crown") > 0


## 매 프레임: 피의 계약서(체력이 샌다) · 숨음 표시
func _items_tick(dt: float) -> void:
	if inv == null:
		return
	if inv.fx("pact") > 0:
		_pact_t += dt
		if _pact_t >= 0.25:
			_pact_t = 0.0
			hp = maxf(1.0, hp - 0.8 * 0.25 * inv.fx("pact"))
	# 숨음: 푸르스름하게 (바뀔 때만 칠한다 — 독·분노 색과 다투지 않게)
	var hid := hidden_t > 0.0
	if hid != _was_hidden and spr_mat:
		_was_hidden = hid
		spr_mat.set_shader_parameter("tint", Color(0.55, 0.6, 0.85) if hid else Color.WHITE)
	if crown and crown.visible:
		crown.position.y = 1.55 + (1.0 / 16.0 if int(Time.get_ticks_msec() / 400) % 2 == 0 else 0.0)


## 내가 누군가를 때림 (Fighter.take 가 부른다)
func on_dealt(target: Fighter, amt: float, h: Hit, friendly: bool) -> void:
	if not friendly and target.team != team:
		ult = minf(DB.ULT_MAX, ult + amt * DB.ULT_PER_DMG * stat("ult"))
	if inv == null or h.kind in ["chain", "burn", "thorns", "item"]:
		return
	var atk := stat("attack")
	# 불꽃 부적 · 독사의 이빨: 맞은 쪽에 지속 피해 (센 쪽이 남는다)
	var burn := inv.fx("burn") * 3.0
	var venom := inv.fx("venom") * 2.5
	if burn + venom > 0.0 and not target.dead:
		target.poison_t = 3.0
		target.poison_dps = maxf(target.poison_dps if target.poison_t > 0.0 else 0.0, burn + venom)
		target.poison_src = self
		if burn > 0.0:
			Fx.pop(target.get_parent(), target.center(), ["spark0", "spark1"], 16.0)
	# 폭풍 반지: 20% 확률로 번개가 둘레 3명에게 (아군도!)
	var chain := inv.fx("chain")
	if chain > 0 and randf() < 0.2 * chain:
		_chain_lightning(target, 8.0 * atk)


func _chain_lightning(from: Fighter, dmg: float) -> void:
	var cands: Array = []
	for f in Combat.in_radius(get_tree(), from.global_position, 4.5):
		if f != from and f != self:
			cands.append(f)
	cands.sort_custom(func(a, b): return (a as Node3D).global_position.distance_to(from.global_position) < (b as Node3D).global_position.distance_to(from.global_position))
	var prev := from.center()
	for i in mini(3, cands.size()):
		var fi := cands[i] as Fighter
		var to := fi.center()
		for k in 4:
			Fx.pop(get_parent(), prev.lerp(to, (k + 0.5) / 4.0) + Vector3(randf_range(-0.15, 0.15), randf_range(-0.15, 0.15), 0), ["spark0", "spark1", "spark2"], 24.0)
		fi.take(Hit.make(dmg, self, Vector3.ZERO, "chain"))
		prev = to
	if not cands.is_empty():
		Audio.play("crystal_ping", from.global_position)


## 내가 적을 쓰러뜨림 (Party 가 부른다)
func on_kill(_e: Node) -> void:
	ult = minf(DB.ULT_MAX, ult + DB.ULT_PER_KILL * stat("ult"))
	if inv == null:
		return
	var vamp := inv.fx("kill_heal")
	if vamp > 0 and not dead:
		var h := Hit.make(5.0 * vamp, self, Vector3.ZERO, "item")
		h.heal = true
		take(h)
	if inv.fx("pact") > 0:
		_pact = mini(20, _pact + inv.fx("pact"))
		set_mod("pact", {"attack": 0.04 * _pact})
		Fx.pop(get_parent(), center() + Vector3(0, 0.6, 0), ["spark0", "spark1", "spark2"], 14.0)


## 맞았을 때 아이템 (가시 갑옷 · 행운의 동전 · 불꽃 부적의 위험)
func _items_on_hurt(h: Hit, _amt: float) -> void:
	var src := h.source as Fighter
	var thorns := inv.fx("thorns")
	if thorns > 0 and src and src != self and not src.dead and h.kind != "thorns":
		src.take(Hit.make(8.0 * thorns, self, Vector3.ZERO, "thorns"))
		Fx.pop(get_parent(), src.center(), ["spark0", "spark1"], 18.0)
	if inv.fx("coin_drop") > 0:
		var party := get_tree().get_first_node_in_group("party")
		if party and party.gold > 0:
			party.add_gold(-1)
			Coin.burst(get_parent(), global_position, 1, terrain)
	if inv.fx("burn") > 0 and randf() < 0.2:
		poison_t = 2.0
		poison_dps = maxf(poison_dps, 3.0)
		poison_src = self


## 금화 줍기 (행운의 동전·왕관만큼 더)
func collect_gold(n: int) -> void:
	_gold_frac += n * stat("gold")
	var whole := int(floorf(_gold_frac))
	_gold_frac -= whole
	var party := get_tree().get_first_node_in_group("party")
	if party and whole > 0:
		party.add_gold(whole)


## 아이템 줍기: 자리가 없으면 오래된 것을 발밑에 떨어뜨린다
func pick_up(id: String) -> void:
	if inv == null:
		return
	for d in inv.add(id):
		for k in int(d[1]):
			ItemDrop.spawn(get_parent(), String(d[0]), global_position + Vector3(0, 0.6, 0), terrain, -facing * 2.0)
	Audio.play(String(Items.RARITY[String(Items.get_item(id).get("rarity", "common"))]["sfx"]), global_position)
	Fx.pop(get_parent(), center() + Vector3(0, 0.5, 0), ["spark0", "spark1", "spark2"], 16.0)
	if local:
		get_tree().call_group("hud", "toast_item", id)
		Achievements.add(get_tree(), "items")
	get_tree().call_group("hud", "notify", "%s: %s" % [nick, Items.get_item(id).get("name", id)], Items.color(id), Hud._item_tex(id))


## 장비 하나 내려놓기 (메뉴에서 — 친구에게 주거나 버리기)
func drop_item(i: int) -> void:
	var id := inv.remove_at(i) if inv else ""
	if id != "":
		ItemDrop.spawn(get_parent(), id, global_position + Vector3(0, 0.6, 0), terrain, facing * 2.5)
		Audio.play("item_c", global_position, -6.0)


## 소비 아이템 쓰기 (F)
func use_consumable() -> void:
	if inv == null or dead or stun_t > 0.0:
		return
	var id := inv.take_consumable()
	if id == "":
		if local:
			Audio.play("ui_denied")
		return
	match id:
		"potion", "big_potion":
			var h := Hit.make(45.0 if id == "potion" else max_hp(), self, Vector3.ZERO, "item")
			h.heal = true
			take(h)
			if id == "big_potion":
				poison_t = 0.0
				slow_t = 0.0
				if spr_mat:
					spr_mat.set_shader_parameter("tint", Color.WHITE)
			Audio.play("heal_get", global_position)
			Fx.pop(get_parent(), center(), ["plus0", "plus1", "plus2"], 10.0, Vector3(0, 1.2, 0))
		"banana":
			Audio.play("pickup", global_position)
			Peel.drop(get_parent(), global_position - facing * 0.6, self, terrain)
		"smoke":
			hidden_t = 4.0
			Audio.play("fuse", global_position)
			for k in 10:
				Fx.pop(get_parent(), global_position + Vector3(randf_range(-1.2, 1.2), randf_range(0.0, 1.0), randf_range(-1.0, 1.0)), ["dust0", "dust1", "dust2", "dust3"], 8.0)
			for f in get_tree().get_nodes_in_group("enemies"):
				if (f as Enemy).target == self:
					(f as Enemy).target = null
		"firecracker":
			Audio.play("fuse", global_position)
			for k in 6:
				var ang := randf() * TAU
				var d := randf_range(1.0, 3.2)
				var bm := Bomb.throw(get_parent(), global_position + Vector3(0, 1.0, 0), Vector3(cos(ang) * d / 0.6, 5.0, sin(ang) * d / 0.6), self, false)
				bm.fuse = 0.6 + k * 0.15
				bm.fuse0 = bm.fuse
				bm.radius = 1.5
				bm.dmg = 18.0 * stat("attack")
				bm.knock = 7.0
		"recall":
			Audio.play("revive", global_position)
			get_tree().call_group("game", "go_camp")
		"curse_doll":
			var best: Fighter = null
			var bd := 8.0
			for f in get_tree().get_nodes_in_group("fighters"):
				var o := f as Fighter
				if o != self and o.team == team and not o.dead and o.global_position.distance_to(global_position) < bd:
					bd = o.global_position.distance_to(global_position)
					best = o
			if best:
				var steal := best.hp * 0.25
				best.hp = maxf(1.0, best.hp - steal)
				best.flash = 0.12
				var h2 := Hit.make(steal, self, Vector3.ZERO, "item")
				h2.heal = true
				take(h2)
				Audio.play("troll", global_position)
				Fx.pop(get_parent(), best.center(), ["spark0", "spark1", "spark2"], 14.0)
			elif local:
				Audio.play("ui_denied")
				get_tree().call_group("hud", "toast_text", "저주할 친구가 없다…", Color("#c8a0ff"))


# ------------------------------------------------------------------ 직업: R 궁극기
func _ult() -> void:
	ult = 0.0
	var u: Dictionary = J["ult"]
	var atk := stat("attack")
	spr.play("attack", true)
	attack_t = 0.35
	Audio.play("q_roar" if job == "warrior" else "item_e", global_position, -4.0)
	if rig and local:
		rig.kick(Vector2(0, -2.0))
	match job:
		"warrior":
			# 영웅의 낙하: 조준점으로 높이 뛰어 내려찍기
			var to := aim_point - global_position
			to.y = 0.0
			if to.length() > float(u["range"]):
				to = to.normalized() * float(u["range"])
			var t := 0.55
			velocity = Vector3(to.x / t, 0.5 * GRAV * t, to.z / t)
			_leap = true
			_leap_t = 0.0
			_coyote = 0.0
			invuln = maxf(invuln, t)
			spr.stretch(1.2)
			Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 14.0)
		"bomber":
			# 폭죽 축제: 둘레에 폭탄을 뿌리고 차례로 터진다
			var n := int(u["n"])
			for k in n:
				var ang := TAU * k / n + randf_range(-0.2, 0.2)
				var d := randf_range(2.0, 4.6)
				var bm := Bomb.throw(get_parent(), global_position + Vector3(0, 1.1, 0), Vector3(cos(ang) * d / 0.7, 6.5, sin(ang) * d / 0.7), self, false)
				bm.fuse = 0.9 + k * 0.12
				bm.fuse0 = bm.fuse
				bm.radius = float(u["radius"])
				bm.dmg = float(u["dmg"]) * atk
				bm.knock = float(u["knock"])
			Audio.play("throw", global_position)
		"doctor":
			# 기적: 원 안의 아군 부활·가득 회복 — 적도 40% 회복 (트롤 위험)
			var r := float(u["radius"])
			for f in get_tree().get_nodes_in_group("fighters"):
				var fi := f as Fighter
				if fi.global_position.distance_to(global_position) > r:
					continue
				if fi.team == team and fi != self:
					if fi.dead and fi is Player:
						(fi as Player).revive(fi.global_position)
					var h := Hit.make(fi.max_hp(), self, Vector3.ZERO, "item")
					h.heal = true
					fi.take(h)
					fi.poison_t = 0.0
					Fx.pop(fi.get_parent(), fi.center(), ["plus0", "plus1", "plus2"], 10.0, Vector3(0, 1.2, 0))
				elif fi.team != team and not fi.dead:
					var h2 := Hit.make(fi.max_hp() * float(u["enemy_heal"]), self, Vector3.ZERO, "item")
					h2.heal = true
					fi.take(h2)
			var hs := Hit.make(max_hp() * float(u["self_heal"]), self, Vector3.ZERO, "item")
			hs.heal = true
			take(hs)
			Audio.play("heal_pulse", global_position)
			var ring_ := Fx.flat_at(get_parent(), global_position, 0.0, ["ring_heal0", "ring_heal1", "ring_heal2", "ring_heal3"], 9.0)
			ring_.scale = Vector3(r / 1.25, 1, r / 1.25)
			Particles.burst(get_parent(), center(), Color("#ffffff"), Color("#7af08a"), 40, 7.0, 4.0)
		"archer":
			# 꿰뚫는 화살: 잠깐 멈춰 겨눈 뒤 일직선
			_ult_lock = float(u["charge"])
			var dir := aim
			var line := Fx.flat_at(get_parent(), global_position + dir * 4.0, atan2(dir.z, dir.x), ["mark0", "mark1", "mark0", "mark1"], 8.0)
			line.scale = Vector3(4.0, 1, 0.35)
			Audio.play("fuse", global_position)
			await get_tree().create_timer(float(u["charge"]), false).timeout
			_ult_lock = 0.0
			if not is_inside_tree() or dead:
				return
			dir = aim
			var h3 := Hit.make(float(u["dmg"]) * atk, self, dir * float(u["knock"]), "arrow")
			var pa := Projectile.fire(get_parent(), global_position + Vector3(0, 0.75, 0), dir, float(u["speed"]), float(u["range"]), Fx.ATLAS, "arrow", self, h3)
			pa.hits_allies = true
			pa.pierce = 99
			pa.radius = 0.6
			pa.sprite.scale = Vector3(2.0, 2.0, 2.0)
			Audio.play("smash", global_position)
			if rig and local:
				rig.shake(4.0)


func _leap_land() -> void:
	_leap = false
	var u: Dictionary = J["ult"]
	var c := global_position
	Audio.play("smash", c)
	Audio.play("land_heavy", c)
	for f in Combat.in_radius(get_tree(), c, float(u["radius"])):
		if f == self:
			continue
		var fi := f as Fighter
		fi.take(Hit.make(float(u["dmg"]) * stat("attack"), self, Combat.push(c, fi.global_position, float(u["knock"]), 0.7), "smash"))
	var ring_ := Fx.flat_at(get_parent(), c, 0.0, ["ring_hit0", "ring_hit1", "ring_hit2", "ring_hit3"], 12.0)
	ring_.scale = Vector3(float(u["radius"]) / 1.1, 1, float(u["radius"]) / 1.1)
	Fx.pop(get_parent(), c, ["dust0", "dust1", "dust2", "dust3"], 12.0)
	Particles.burst(get_parent(), c + Vector3(0, 0.2, 0), Color("#fff0c0"), Color("#8a6a4a"), 36, 7.0)
	spr.stretch(-1.6)
	if rig:
		rig.shake(7.0)
	Combat.hitstop(get_tree(), 0.08)



# ------------------------------------------------------------------ 파티: 들기 · 던지기 · 밀치기 · 이모트
## 손 닿는 곳의 아군(또는 작은 적)을 번쩍 든다 — 상호작용할 게 없을 때 E
func _try_grab() -> void:
	var best: Fighter = null
	var bd := 1.1
	for f in get_tree().get_nodes_in_group("fighters"):
		var o := f as Fighter
		if o == self or o.dead or o.held or o is Queen:
			continue
		if o is Player and (o as Player).carrying:
			continue
		var d := o.global_position.distance_to(global_position)
		if d < bd:
			bd = d
			best = o
	if best == null:
		return
	carrying = best
	best.held = true
	_carry_t = 0.0
	if best is Player:
		(best as Player).carried_by = self
		(best as Player)._mash = 0
	Audio.play("pickup", global_position)
	spr.stretch(-0.5)
	if best is Player:
		get_tree().call_group("hud", "notify", "%s 이(가) %s 을(를) 번쩍!" % [nick, (best as Player).nick], Color("#ffd860"))


func _carry_hold(dt: float) -> void:
	_carry_t += dt
	if not is_instance_valid(carrying) or carrying.dead or dead:
		_drop_carry()
		return
	carrying.global_position = global_position + Vector3(0, 1.25, 0)
	carrying.velocity = Vector3.ZERO
	# 적은 4초, 아군은 버둥거리면 빠져나간다
	if carrying is Enemy and _carry_t > 4.0:
		_drop_carry()


func _drop_carry() -> void:
	if is_instance_valid(carrying):
		carrying.held = false
		if carrying is Player:
			(carrying as Player).carried_by = null
	carrying = null


## 조준한 쪽으로 던지기 (아군 피해는 없지만 부딪힌 것들은 아프다 · 절벽 너머로도…)
func _throw() -> void:
	if not is_instance_valid(carrying):
		carrying = null
		return
	var c := carrying
	_drop_carry()
	var dir := aim if aim.length() > 0.1 else facing
	c.throw_from(self, dir.normalized() * 11.0 + Vector3(0, 6.5, 0))
	if c is Player and local:
		Achievements.add(get_tree(), "ally_throws")
	Audio.play("throw", global_position)
	Audio.play("troll", global_position, -8.0)
	spr.play("attack", true)
	attack_t = 0.25
	spr.wobble(1.0, 1.2)
	Fx.pop(get_parent(), global_position + dir * 0.5, ["dust0", "dust1", "dust2", "dust3"], 16.0)


## 들려 있을 때: 꼼짝 못 하고 구르기 연타 5번이면 빠져나간다
func _carried_tick(dt: float) -> void:
	if not is_instance_valid(carried_by) or carried_by.dead or carried_by.carrying != self:
		carried_by = null
		held = false
		return
	velocity = Vector3.ZERO
	spr.play("fall")
	if ctrl.roll or ctrl.attack:
		_mash += 1
		spr.wobble(1.0 if _mash % 2 == 0 else -1.0, 1.0)
		if _mash >= 5:
			var by := carried_by
			by._drop_carry()
			throw_from(by, Vector3(randf_range(-2, 2), 6.0, randf_range(-2, 2)))
			thrown_t = 0.0
			Audio.play("jump", global_position)
	spr.update(dt)


## 구르면서 아군에 부딪히면 밀쳐 낸다 (트롤)
func _shove() -> void:
	for f in Combat.in_radius(get_tree(), global_position + roll_dir * 0.3, 0.45, 1.0):
		var o := f as Fighter
		if o == self or o.team != team or o.held:
			continue
		if o.kv.length() > 5.0:
			continue
		o.kv += roll_dir * 8.0 + Vector3(0, 2.5, 0)
		o.flash = 0.06
		Audio.play("troll", global_position, -10.0)


## 로컬 협동: 다른 사람들 가운데에서 너무 멀어지지 못하게 (한 화면 안에)
func _leash() -> void:
	var c := Vector3.ZERO
	var n := 0
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player and f != self and not (f as Player).dead:
			c += (f as Node3D).global_position
			n += 1
	if n == 0:
		return
	c /= n
	var d := global_position - c
	var lim := Vector2(12.5, 7.5)
	if absf(d.x) > lim.x and signf(velocity.x) == signf(d.x):
		velocity.x = 0.0
	if absf(d.z) > lim.y and signf(velocity.z) == signf(d.z):
		velocity.z = 0.0


## 이모트: 머리 위 말풍선 그림 (UI 시스템.md)
func emote(id: String) -> void:
	Emote.pop_on(self, id)


## 머리 위 이름 + 체력바 (여럿일 때) — 체력이 낮으면 바 색이 바뀌고 깜빡인다
var _hpbar: HpBar
func update_nameplate(show: bool) -> void:
	if not show:
		if nameplate:
			nameplate.visible = false
		if _hpbar:
			_hpbar.always = false
		return
	if nameplate == null:
		nameplate = Label3D.new()
		nameplate.font = PixelFont.get_font("small")
		nameplate.font_size = PixelFont.size_of("small")
		nameplate.pixel_size = 1.0 / 16.0
		nameplate.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		nameplate.no_depth_test = true
		nameplate.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
		nameplate.outline_modulate = Color(0.1, 0.08, 0.12)
		nameplate.outline_size = 3
		nameplate.render_priority = 4
		nameplate.outline_render_priority = 3
		add_child(nameplate)
		_hpbar = HpBar.attach(self, 18)
	_hpbar.always = true
	_hpbar.position.y = 1.62 if not crown or not crown.visible else 1.95
	nameplate.visible = not dead
	var title := ("[%s] " % Achievements.title_name(Prefs.title_id)) if local and Prefs.title_id != "" else ""
	nameplate.text = title + nick
	nameplate.modulate = team_color
	nameplate.position.y = _hpbar.position.y + 0.38
