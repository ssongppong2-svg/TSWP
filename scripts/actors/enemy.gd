class_name Enemy
extends Fighter
## 숲 몬스터 (DB.ENEMIES). 적 시스템.md: 역할이 분명하고, 공격은 예고(빨갛게 번쩍 + 소리) 후에 온다.
##  · melee  (도토리 병정): 다가와서 찌르기
##  · ranged (독버섯): 거리를 두고 포자 발사, 가까우면 물러남
##  · lunger (숲 거미): 빠르게 둘러싸다 도약 물기, 가끔 거미줄(느려짐)
## 상태: 쉼(배회) → 추격 → 준비(예고) → 공격 → 회복. 생성 자리에서 너무 멀어지면 돌아간다.

signal killed(e: Enemy, by: Node)

const ATLAS := "res://assets/sprites/enemies.png"
const FPS := {"idle": 3.0, "move": 9.0, "attack": 10.0, "hurt": 1.0}

var kind := "acorn"
var E: Dictionary
var mesh: MeshInstance3D
var state := "idle"
var st_t := 0.0
var target: Fighter
var home := Vector3.ZERO
var wander := Vector3.ZERO
var flip := false
var anim := "idle"
var anim_t := 0.0
var lunge_dir := Vector3.ZERO
var _think := 0.0
var _side_lane := false
# 가상 뼈 (스프링): 예비동작에 움츠렸다 공격에 튀어나오고, 맞으면 출렁인다
var bone_sx := SpringBone.new(1.0, 240.0, 12.0)
var bone_sy := SpringBone.new(1.0, 240.0, 12.0)
var bone_lean := SpringBone.new(0.0, 150.0, 10.0)


static func spawn(parent: Node, k: String, at: Vector3, terrain_: Terrain, rig_: CamRig) -> Enemy:
	var e := Enemy.new()
	e.kind = k
	e.terrain = terrain_
	e.rig = rig_
	parent.add_child(e)
	e.global_position = at
	e.home = at
	return e


func _ready() -> void:
	team = 1
	E = DB.ENEMIES[kind]
	base_max_hp = float(E["hp"])
	hp = base_max_hp
	body_r = float(E["radius"])
	body_h = float(E["height"])
	collision_layer = 4
	collision_mask = 1
	floor_snap_length = 0.3
	var cs := CollisionShape3D.new()
	var sph := SphereShape3D.new()
	sph.radius = 0.3
	cs.shape = sph
	cs.position.y = 0.3
	add_child(cs)
	mesh = BB.make(ATLAS, "%s_idle0" % kind, {"unique": true, "up_normal": 0.7})
	spr_mat = mesh.material_override
	add_child(mesh)
	add_child(BB.blob(body_r + 0.1, (body_r + 0.1) * 0.5, 0.9))
	HpBar.attach(self, 14)
	wander = home
	st_t = randf() * 2.0
	# 나타남: 아래에서 솟듯
	mesh.scale = Vector3(1.0, 0.1, 1.0)
	create_tween().tween_property(mesh, "scale", Vector3.ONE, 0.25).set_trans(Tween.TRANS_BACK)


func _physics_process(dt: float) -> void:
	tick_status(dt)
	if dead:
		return
	st_t -= dt
	_think -= dt
	if _think <= 0.0:
		_think = 0.25
		_pick_target()
	var want := Vector3.ZERO
	var spd := float(E["speed"]) * move_mul()
	var to := Vector3.ZERO
	var dist := 999.0
	if target:
		to = target.global_position - global_position
		to.y = 0.0
		dist = to.length()
	match state:
		"idle":
			_set_anim("move" if global_position.distance_to(wander) > 0.4 else "idle")
			if global_position.distance_to(wander) > 0.4:
				want = (wander - global_position).normalized() * spd * 0.4
			if st_t <= 0.0:
				st_t = randf_range(1.5, 3.5)
				wander = home + Vector3(randf_range(-2.5, 2.5), 0, randf_range(-2.5, 2.5))
			if target:
				state = "chase"
				Audio.play("telegraph", global_position, -6.0)
		"chase":
			if target == null or global_position.distance_to(home) > 16.0:
				state = "idle"
				target = null
				wander = home
			else:
				_set_anim("move")
				var role := String(E["role"])
				if role == "ranged":
					# 알맞은 거리 유지: 너무 가까우면 물러나고, 멀면 다가간다
					var ideal := float(E["range"]) * 0.75
					if dist < ideal - 1.0:
						want = -to.normalized() * spd
					elif dist > ideal + 0.6:
						want = to.normalized() * spd
					else:
						want = to.normalized().rotated(Vector3.UP, PI / 2) * spd * 0.4 * (1.0 if int(home.x) % 2 == 0 else -1.0)
					if dist < float(E["range"]) and st_t <= 0.0:
						_windup()
				elif role == "lunger":
					# 옆으로 돌다가 거리가 맞으면 도약
					var side_dir := to.normalized().rotated(Vector3.UP, PI / 2 * (1.0 if int(home.z) % 2 == 0 else -1.0))
					want = (to.normalized() * (1.0 if dist > float(E["range"]) else -0.3) + side_dir * 0.6).normalized() * spd
					if dist < float(E["range"]) and st_t <= 0.0:
						_windup()
				else:
					want = to.normalized() * spd
					if dist < float(E["range"]) and st_t <= 0.0:
						_windup()
		"windup":
			_set_anim("attack", 0)
			spr_mat.set_shader_parameter("flash", 0.45 if int(st_t * 20.0) % 2 == 0 else 0.0)
			if target:
				_face(to)
			if st_t <= 0.0:
				_strike(to)
		"strike":
			_set_anim("attack", 1)
			if String(E["role"]) == "lunger":
				want = lunge_dir * float(E["lunge"])
				_bite_check()
			if st_t <= 0.0:
				state = "recover"
				st_t = float(E["recover"])
		"recover":
			_set_anim("attack", 2)
			if st_t <= 0.0:
				state = "chase"
				st_t = randf_range(0.3, 1.0)
	# 서로 밀어내기 (겹치지 않게)
	for f in get_tree().get_nodes_in_group("enemies"):
		if f == self:
			continue
		var d: Vector3 = global_position - (f as Node3D).global_position
		d.y = 0.0
		var l := d.length()
		if l < 0.7 and l > 0.001:
			want += d / l * (0.7 - l) * 6.0
	# 측면 구간 안이면 구간 선에 붙는다
	if rig and rig.side_goal > 0.5 and absf(global_position.z - rig.lane_z) < 3.0:
		var n := rig.lane_normal()
		want -= n * want.dot(n)
		want -= n * (global_position.dot(n) - rig.lane_z) * 6.0
	if not is_on_floor():
		velocity.y -= GRAV * dt
	velocity.x = move_toward(velocity.x, want.x, 30.0 * dt) + kv.x
	velocity.z = move_toward(velocity.z, want.z, 30.0 * dt) + kv.z
	if kv.y > 0.5:
		velocity.y = maxf(velocity.y, kv.y)
		kv.y = 0.0
	move_and_slide()
	velocity.x -= kv.x
	velocity.z -= kv.z
	if want.length() > 0.2 and state != "windup":
		_face(want)
	if global_position.y < -6.0:
		hp = 0.0
		_die()
	_anim_tick(dt)


func _enter_tree() -> void:
	super._enter_tree()
	add_to_group("enemies")


func _pick_target() -> void:
	var best: Fighter = null
	var bd := float(E["aggro"]) * (1.6 if target else 1.0)
	for f in get_tree().get_nodes_in_group("fighters"):
		var o := f as Fighter
		if o.team == team or o.dead:
			continue
		var d := o.global_position.distance_to(global_position)
		if d < bd:
			bd = d
			best = o
	target = best


func _windup() -> void:
	state = "windup"
	bone_sy.target = 0.82          # 움츠림 (예고)
	bone_sx.target = 1.15
	st_t = float(E["windup"])
	Audio.play("telegraph", global_position, -3.0)


func _strike(to: Vector3) -> void:
	state = "strike"
	bone_sy.target = 1.0
	bone_sx.target = 1.0
	bone_sy.kick(10.0)             # 튀어나옴
	bone_sx.kick(-8.0)
	spr_mat.set_shader_parameter("flash", 0.0)
	var dir := to.normalized() if to.length() > 0.01 else Vector3(1, 0, 0)
	match String(E["role"]):
		"melee":
			st_t = 0.18
			Audio.play("acorn_swing", global_position)
			for f in Combat.in_fan(get_tree(), global_position + dir * 0.3, dir, float(E["range"]) + 0.3, 40.0):
				var fi := f as Fighter
				if fi.team != team:
					fi.take(Hit.make(float(E["dmg"]), self, Combat.push(global_position, fi.global_position, float(E["knock"]), 0.3), "stab"))
		"ranged":
			st_t = 0.2
			Audio.play("shroom_shoot", global_position)
			var p := Projectile.fire(get_parent(), global_position + Vector3(0, 0.6, 0), dir, float(E["shot"]), float(E["range"]) + 3.0, ATLAS, "spore0", self, Hit.make(float(E["dmg"]), self, Vector3(1, 0, 0) * float(E["knock"]), "spore"))
			p.hit.poison = 2.0
		"lunger":
			st_t = 0.28
			lunge_dir = dir
			Audio.play("spider_bite", global_position)
			# 가끔은 물기 대신 거미줄 (느려짐)
			if randf() < 0.3:
				st_t = 0.1
				lunge_dir = Vector3.ZERO
				Audio.play("web_shot", global_position)
				var w := Projectile.fire(get_parent(), global_position + Vector3(0, 0.4, 0), dir, 7.0, 8.0, ATLAS, "web0", self, Hit.make(2.0, self, Vector3.ZERO, "web"))
				w.hit.slow = 0.5


var _bitten := {}
func _bite_check() -> void:
	for f in Combat.in_radius(get_tree(), global_position + lunge_dir * 0.3, 0.5):
		var fi := f as Fighter
		if fi.team != team and not _bitten.has(fi):
			_bitten[fi] = true
			fi.take(Hit.make(float(E["dmg"]), self, lunge_dir * float(E["knock"]), "bite"))
	if state != "strike":
		_bitten.clear()


func _face(v: Vector3) -> void:
	var right := Vector3(1, 0, 0).rotated(Vector3.UP, rig.yaw if rig else 0.0)
	var sx := v.dot(right)
	if absf(sx) > 0.05:
		flip = sx < 0.0


func _set_anim(a: String, frame := -1) -> void:
	if a != anim:
		anim = a
		anim_t = 0.0
	if frame >= 0:
		anim_t = frame / FPS[a]


func _anim_tick(dt: float) -> void:
	anim_t += dt
	var scr_r := Vector3(1, 0, 0).rotated(Vector3.UP, rig.yaw if rig else 0.0)
	bone_lean.target = clampf(velocity.dot(scr_r) * 0.03, -0.2, 0.2)
	if state != "windup":
		bone_sy.target = 1.0
		bone_sx.target = 1.0
	spr_mat.set_shader_parameter("squash", Vector2(bone_sx.step(dt), bone_sy.step(dt)))
	spr_mat.set_shader_parameter("skew", bone_lean.step(dt))
	var counts := {"idle": 2, "move": 4, "attack": 3, "hurt": 1}
	var a := "hurt" if flash > 0.06 else anim
	var i: int = int(anim_t * float(FPS[a])) % int(counts[a])
	if a == "attack":
		i = int({"windup": 0, "strike": 1, "recover": 2}.get(state, 0))
	spr_mat.set_shader_parameter("region", BB.region_uv(ATLAS, "%s_%s%d" % [kind, a, i]))
	spr_mat.set_shader_parameter("flip", 1.0 if flip else 0.0)


func _on_hurt(h: Hit, amt: float) -> void:
	if amt > 0.0:
		Audio.play("%s_hurt" % kind, global_position)
		var scr_r := Vector3(1, 0, 0).rotated(Vector3.UP, rig.yaw if rig else 0.0)
		bone_lean.kick(signf(h.knock.dot(scr_r) + 0.001) * 3.5)
		Particles.burst(get_parent(), center(), Color("#fff4d0"), Color("#c88a4a"), 8, 3.5)
		bone_sy.kick(-7.0)
		bone_sx.kick(6.0)
		# 맞으면 예고 중이던 공격이 끊긴다 (때리는 맛)
		if state == "windup" and amt >= 10.0:
			state = "recover"
			st_t = 0.35
		if target == null and h.source is Fighter:
			target = h.source


func _die() -> void:
	super._die()
	Audio.play("%s_die" % kind, global_position)
	killed.emit(self, last_hit_by)
	var pc := {"acorn": [Color("#e8b070"), Color("#6e4426")], "shroom": [Color("#ffb0b0"), Color("#8a2e4e")], "spider": [Color("#c0a0e0"), Color("#2e1e3e")]}
	var cc: Array = pc.get(kind, [Color.WHITE, Color.GRAY])
	Particles.burst(get_parent(), center(), cc[0], cc[1], 22, 5.0)
	Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 12.0)
	Fx.pop(get_parent(), global_position + Vector3(0, 0.3, 0), ["spark0", "spark1", "spark2"], 12.0)
	collision_layer = 0
	var tw := create_tween()
	tw.tween_property(mesh, "scale", Vector3(1.4, 0.0, 1.0), 0.22)
	tw.tween_callback(queue_free)
