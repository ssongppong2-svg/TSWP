class_name Enemy
extends Fighter
## 숲 몬스터 (DB.ENEMIES). 적 시스템.md: 역할이 분명하고, 공격은 예고(빨갛게 번쩍 + 소리) 후에 온다.
##  · melee  (도토리 병정): 다가와서 찌르기
##  · ranged (독버섯): 거리를 두고 포자 발사, 가까우면 물러남
##  · lunger (숲 거미): 빠르게 둘러싸다 도약 물기, 가끔 거미줄(느려짐)
## 상태: 쉼(배회) → 추격 → 준비(예고) → 공격 → 회복. 생성 자리에서 너무 멀어지면 돌아간다.

signal killed(e: Enemy, by: Node)

const ATLAS := "res://assets/sprites/enemies.png"
const FPS := {"idle": 5.0, "move": 11.0, "attack": 10.0, "hurt": 8.0, "die": 11.0}
const COUNTS := {"idle": 4, "move": 6, "attack": 3, "hurt": 2, "die": 4}

var kind := "acorn"
var sk := "acorn"                  # 소리·파편 색 이름 (특수 적은 기본 적 것을 쓴다)
var art := "acorn"                 # 그림 이름 (특수 적도 자기 그림 — 없으면 기본 적 그림 + 색)
var elite := false                 # 엘리트: 크고 질기고 일부 상태이상 면역 · 좋은 것을 떨어뜨린다
var viewer := ""                   # 방송: 이 적에 붙은 시청자 이름
var E: Dictionary
var _life := 0.0
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


static func spawn(parent: Node, k: String, at: Vector3, terrain_: Terrain, rig_: CamRig, elite_ := false, viewer_ := "") -> Enemy:
	var e := Enemy.new()
	e.kind = k
	e.elite = elite_
	e.viewer = viewer_
	e.terrain = terrain_
	e.rig = rig_
	parent.add_child(e)
	e.global_position = at
	e.home = at
	return e


func _ready() -> void:
	team = 1
	E = (DB.ENEMIES[kind] as Dictionary).duplicate()
	sk = String(E.get("sprite", kind))
	art = kind if (BB.atlas(ATLAS)["rects"] as Dictionary).has("%s_idle0" % kind) else sk
	var lv := Diff.level()
	E["dmg"] = float(E["dmg"]) * Diff.DMG[lv]
	base_max_hp = float(E["hp"]) * Diff.HP[lv]
	if Diff.meme("tiny_foes"):
		E["speed"] = float(E["speed"]) * 1.5
		base_max_hp *= 0.6
	if elite:
		base_max_hp *= 2.6
		E["dmg"] = float(E["dmg"]) * 1.4
		E["gold"] = int(E["gold"]) * 3
		immune = ["stun", "freeze", "fear", "confuse"]
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
	mesh = BB.make(ATLAS, "%s_idle0" % art, {"unique": true, "up_normal": 0.7, "shade_k": 0.6})
	spr_mat = mesh.material_override
	add_child(mesh)
	if E.has("tint") and art == sk:
		spr_mat.set_shader_parameter("tint", E["tint"])
	if elite or Diff.meme("tiny_foes"):
		var sc := 1.32 if elite else 0.7
		mesh.scale = Vector3(sc, sc, 1)
		body_h *= sc
	if elite or viewer != "" or (E.has("tint") and art == sk):    # 특수 적은 이제 자기 그림이 있어서 이름표 없이도 알아본다
		var nl := Label3D.new()
		nl.text = viewer if viewer != "" else ("엘리트 " + String(E["name"]) if elite else String(E["name"]))
		nl.font = PixelFont.get_font("small")
		nl.font_size = PixelFont.size_of("small")
		nl.pixel_size = 1.0 / 16.0
		nl.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		nl.no_depth_test = true
		nl.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
		nl.outline_modulate = Color(0.1, 0.08, 0.12)
		nl.outline_size = 3
		nl.modulate = Color("#ffd860") if elite else (Color("#e8f4ff") if viewer != "" else (E["tint"] as Color).lightened(0.2))
		nl.position.y = body_h + 0.55
		add_child(nl)
	add_child(BB.blob(body_r + 0.1, (body_r + 0.1) * 0.5, 0.9))
	HpBar.attach(self, 14)
	wander = home
	st_t = randf() * 2.0
	# 나타남: 아래에서 솟듯
	var s0 := mesh.scale.x
	mesh.scale = Vector3(s0, 0.1, 1.0)
	create_tween().tween_property(mesh, "scale", Vector3(mesh.scale.x, mesh.scale.x, 1), 0.25).set_trans(Tween.TRANS_BACK)


func _physics_process(dt: float) -> void:
	tick_status(dt)
	if dead:
		# 쓰러지면서도 맞은 쪽으로 미끄러진다 (손맛)
		if kv.length() > 0.2 or not is_on_floor():
			velocity = Vector3(kv.x, velocity.y - 22.0 * dt + maxf(0.0, kv.y), kv.z)
			kv = Vector3(kv.x, 0.0, kv.z).move_toward(Vector3.ZERO, 26.0 * dt)
			move_and_slide()
		return
	if held:
		_set_anim("attack", 0)
		_anim_tick(dt)
		return
	st_t -= dt
	_think -= dt
	_life += dt
	if String(E["role"]) == "thief" and _life > 22.0:
		Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 12.0)
		get_tree().call_group("hud", "notify", "황금 거미가 도망쳤다…", Color("#ffe070"))
		queue_free()
		return
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
	if disabled() or has_status("fear") or has_status("confuse"):
		# 넘어짐·기절·빙결: 하던 공격이 끊기고 가만히 · 공포: 건 쪽에서 도망 · 혼란: 아무 데나
		if state in ["windup", "strike"]:
			state = "recover"
			st_t = 0.3
			spr_mat.set_shader_parameter("flash", 0.0)
		if has_status("fear") and not disabled():
			var src: Variant = status_src.get("fear", null)
			if src is Node3D and is_instance_valid(src):
				var away := global_position - (src as Node3D).global_position
				away.y = 0.0
				want = away.normalized() * spd
				_set_anim("move")
		elif has_status("confuse") and not disabled():
			want = Vector3(cos(st_t * 3.0 + home.x), 0, sin(st_t * 2.3 + home.z)) * spd * 0.7
			_set_anim("move")
	else:
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
					if role == "thief":
						# 황금 거미: 사람에게서 도망 (잡으면 금화 · 아이템)
						want = -to.normalized() * spd
						want = want.rotated(Vector3.UP, sin(_life * 1.7) * 0.8)
					elif role == "ranged" or role == "healer":
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
				var fr := 30.0 if String(E["role"]) == "bomber" else 20.0
				spr_mat.set_shader_parameter("flash", 0.45 if int(st_t * fr) % 2 == 0 else 0.0)
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
		velocity.y -= GRAV * grav_k * dt
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


## 무리 경보: 한 마리가 누군가를 보면 둘레 7m 동료도 같이 덤빈다 (하나씩 끌려오는 지루함 없애기)
func alert(t: Fighter) -> void:
	if target == null and not dead and t and not t.dead:
		target = t


func _pick_target() -> void:
	var had := target != null
	_pick_target_inner()
	if not had and target != null:
		for e in get_tree().get_nodes_in_group("enemies"):
			if e != self and (e as Node3D).global_position.distance_to(global_position) < 7.0:
				(e as Enemy).alert(target)


func _pick_target_inner() -> void:
	var best: Fighter = null
	var bd := float(E["aggro"]) * (1.6 if target else 1.0)
	for f in get_tree().get_nodes_in_group("fighters"):
		var o := f as Fighter
		if o.team == team or o.dead or o.hidden_t > 0.0:
			continue
		# 노림 정도(threat): 저주받은 왕관을 쓰면 멀리서도 그 사람만 본다
		var d := o.global_position.distance_to(global_position) / o.stat("threat")
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
		"bomber":
			_explode()
			hp = 0.0
			_die()
			return
		"healer":
			st_t = 0.3
			Audio.play("heal_pulse", global_position, -4.0)
			var ring := Fx.flat_at(get_parent(), global_position, 0.0, ["ring_heal0", "ring_heal1", "ring_heal2", "ring_heal3"], 10.0)
			ring.scale = Vector3(float(E["heal_r"]) / 1.25, 1, float(E["heal_r"]) / 1.25)
			for f in Combat.in_radius(get_tree(), global_position, float(E["heal_r"])):
				var fi := f as Fighter
				if fi.team == team and fi != self:
					var h := Hit.make(float(E["heal"]), self)
					h.heal = true
					fi.take(h)
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
			var hb := Hit.make(float(E["dmg"]), self, lunge_dir * float(E["knock"]), "bite")
			if E.has("bite_status"):
				hb.status = E["bite_status"]
			fi.take(hb)
	if state != "strike":
		_bitten.clear()


## 자폭: 둘레 모두(적끼리도)에게 폭발 — 죽을 때도 터진다 (근처에서 잡으면 같이 휘말림)
var _exploded := false
func _explode() -> void:
	if _exploded:
		return
	_exploded = true
	var r := float(E.get("blast", 1.8)) * (2.0 if Diff.meme("double_boom") else 1.0)
	get_tree().call_group("breakable", "blast", global_position, r)
	Audio.play("explode_s", global_position)
	Fx.pop(get_parent(), global_position + Vector3(0, 0.4, 0), ["boom0", "boom1", "boom2", "boom3", "boom4"], 16.0)
	Particles.burst(get_parent(), global_position + Vector3(0, 0.4, 0), Color("#fff0a0"), Color("#e05a2a"), 26, 6.0)
	for f in Combat.in_radius(get_tree(), global_position, r):
		var fi := f as Fighter
		if fi == self:
			continue
		var h := Hit.make(float(E["dmg"]), self, Combat.push(global_position, fi.global_position, float(E["knock"]), 0.5), "bomb")
		h.explosive = true
		fi.take(h)
	if rig:
		rig.shake(3.0)


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
	var a := "hurt" if flash > 0.06 or disabled() else anim
	var i: int = int(anim_t * float(FPS[a])) % int(COUNTS[a])
	if a == "attack":
		i = int({"windup": 0, "strike": 1, "recover": 2}.get(state, 0))
	spr_mat.set_shader_parameter("region", BB.region_uv(ATLAS, "%s_%s%d" % [art, a, i]))
	spr_mat.set_shader_parameter("flip", 1.0 if flip else 0.0)


func _on_hurt(h: Hit, amt: float) -> void:
	if amt > 0.0:
		Audio.play("%s_hurt" % sk, global_position)
		var scr_r := Vector3(1, 0, 0).rotated(Vector3.UP, rig.yaw if rig else 0.0)
		bone_lean.kick(signf(h.knock.dot(scr_r) + 0.001) * 3.5)
		Particles.burst(get_parent(), center(), Color("#fff4d0"), Color("#c88a4a"), 8, 3.5)
		# 맞은 자리 터짐: 때린 쪽을 향한 몸 가장자리 (치명타는 크고 노랗게 + 멈춤 + 흔들림)
		var hp_at := center()
		if h.source is Node3D and is_instance_valid(h.source):
			var to := (h.source as Node3D).global_position - global_position
			to.y = 0.0
			if to.length() > 0.01:
				hp_at += to.normalized() * body_r * 0.8
		if h.crit:
			Fx.pop(get_parent(), hp_at, ["crit0", "crit1", "crit2", "crit3"], 22.0)
			Combat.hitstop(get_tree(), 0.06)
			if rig:
				rig.shake(1.6)
			Audio.play("crit", global_position)
		elif not (h.kind in DOT_KINDS):
			Fx.pop(get_parent(), hp_at, ["hit0", "hit1", "hit2", "hit3"], 24.0)
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
	Audio.play("%s_die" % sk, global_position)
	killed.emit(self, last_hit_by if is_instance_valid(last_hit_by) else null)
	var pc := {"acorn": [Color("#e8b070"), Color("#6e4426")], "shroom": [Color("#ffb0b0"), Color("#8a2e4e")], "spider": [Color("#c0a0e0"), Color("#2e1e3e")]}
	var cc: Array = pc.get(sk, [Color.WHITE, Color.GRAY])
	if String(E["role"]) == "bomber":
		_explode()
	Particles.burst(get_parent(), center(), cc[0], cc[1], 22, 5.0)
	Fx.pop(get_parent(), center(), ["kpop0", "kpop1", "kpop2", "kpop3"], 16.0)
	if last_hit_by is Player:
		Combat.hitstop(get_tree(), 0.05 if not elite else 0.12)
		if rig:
			rig.shake(1.2 if not elite else 3.0)
	Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 12.0)
	Fx.pop(get_parent(), global_position + Vector3(0, 0.3, 0), ["spark0", "spark1", "spark2"], 12.0)
	collision_layer = 0
	# 쓰러지는 그림 4장 → 잠깐 누워 있다가 사라짐
	var sc := mesh.scale
	var tw := create_tween()
	tw.tween_property(mesh, "scale", Vector3(sc.x * 1.25, sc.y * 0.8, 1.0), 0.06)
	tw.tween_property(mesh, "scale", sc, 0.1)
	for k in 4:
		tw.tween_callback(func(): spr_mat.set_shader_parameter("region", BB.region_uv(ATLAS, "%s_die%d" % [art, k])))
		tw.tween_interval(0.09)
	tw.tween_interval(0.5)
	tw.tween_property(mesh, "transparency", 1.0, 0.35)
	tw.tween_callback(queue_free)
