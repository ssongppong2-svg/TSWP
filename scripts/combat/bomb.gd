class_name Bomb
extends Node3D
## 던진 폭탄: 포물선 → 땅에 튀고 굴러 → 도화선이 끝나면 폭발
##  폭발은 진영을 가리지 않는다 (적·아군·던진 사람·구조물). 근거: 직업 시스템.md 폭탄마
##  도화선이 짧아질수록 빨갛게 빨리 깜빡인다 (언제 터질지 보여야 피할 수 있다)

signal exploded(at: Vector3)

const GRAV := 18.0

var vel := Vector3.ZERO
var fuse := 1.0
var fuse0 := 1.0
var radius := 2.0
var dmg := 20.0
var knock := 8.0
var ff_cur := -1.0
var self_ratio := 0.5
var big := false
var owner_f: Fighter
var terrain: Terrain
var sprite: MeshInstance3D
var mat: ShaderMaterial
var _rest := false
var _fuse_snd: AudioStreamPlayer3D


static func throw(parent: Node, from: Vector3, v: Vector3, owner_: Fighter, big_: bool) -> Bomb:
	var b := Bomb.new()
	b.vel = v
	b.owner_f = owner_
	b.terrain = owner_.terrain
	b.big = big_
	b.sprite = BB.make(Fx.ATLAS, "bomb_big" if big_ else "bomb", {"unique": true, "up_normal": 1.0, "receive_shade": false})
	b.mat = b.sprite.material_override
	b.add_child(b.sprite)
	var sh := BB.blob(0.3 if not big_ else 0.5, 0.16 if not big_ else 0.26, 0.8)
	b.add_child(sh)
	parent.add_child(b)
	b.global_position = from
	if big_:
		b._fuse_snd = Audio.loop_at(b, "fuse_loop")
	return b


func _physics_process(dt: float) -> void:
	fuse -= dt
	# 깜빡임: 남은 시간이 짧을수록 빠르게
	var k := clampf(fuse / maxf(fuse0, 0.01), 0.0, 1.0)
	var blink := sin(Time.get_ticks_msec() / 1000.0 * lerpf(26.0, 8.0, k)) > 0.0
	mat.set_shader_parameter("flash", 0.55 if blink and k < 0.6 else 0.0)
	mat.set_shader_parameter("tint", Color(1.0, 0.6, 0.55) if blink else Color.WHITE)
	if fuse <= 0.0:
		_explode()
		return
	if not _rest:
		vel.y -= GRAV * dt
		var nxt := global_position + vel * dt
		var g := terrain.height_at(nxt) if terrain else 0.0
		# 벽: 다음 칸이 지금보다 높으면 튕긴다
		if g > global_position.y + 0.2:
			vel.x *= -0.45
			vel.z *= -0.45
			nxt = global_position + Vector3(0, vel.y * dt, 0)
			g = terrain.height_at(nxt)
		if nxt.y <= g:
			nxt.y = g
			if absf(vel.y) > 2.0:
				Audio.play("bomb_bounce", nxt)
			vel.y = -vel.y * 0.35
			vel.x *= 0.6
			vel.z *= 0.6
			if absf(vel.y) < 1.0 and Vector2(vel.x, vel.z).length() < 0.6:
				_rest = true
		global_position = nxt


func _explode() -> void:
	var at := global_position + Vector3(0, 0.3, 0)
	var tree := get_tree()
	for f in Combat.in_radius(tree, at, radius, radius):
		var fi := f as Fighter
		var d := (fi.center() - at).length()
		var fall := clampf(1.0 - d / (radius + fi.body_r) * 0.5, 0.5, 1.0)
		var h := Hit.make(dmg * fall, owner_f, Combat.push(at, fi.global_position, knock * fall, 0.6), "bomb")
		h.explosive = true
		h.ff_cur = ff_cur
		h.self_ratio = self_ratio
		fi.take(h)
	exploded.emit(at)
	Particles.burst(get_parent(), at, Color("#fff0a0"), Color("#c84a28"), 40 if big else 20, 9.0 if big else 6.0, 9.0)
	get_tree().call_group("pond", "splash", at, 0.6 if big else 0.35, 4.0 if big else 2.5)
	# 연출: 불덩이 · 불빛 번쩍 · 흔들림 · 소리
	var fx := Fx.pop(get_parent(), global_position - Vector3(0, 0.7 if big else 0.4, 0), ["boom0", "boom1", "boom2", "boom3", "boom4", "boom5"], 16.0)
	fx.mesh.material_override.set_shader_parameter("emission", 0.7)
	if big:
		fx.mesh.scale = Vector3(1.6, 1.6, 1.6)
	var l := OmniLight3D.new()
	l.light_color = Color("#ffb050")
	l.light_energy = 6.0 if big else 3.0
	l.omni_range = radius * 2.5
	get_parent().add_child(l)
	l.global_position = at + Vector3(0, 0.8, 0)
	var tw := l.create_tween()
	tw.tween_property(l, "light_energy", 0.0, 0.35)
	tw.tween_callback(l.queue_free)
	Audio.play("explode_l" if big else "explode_s", at)
	if owner_f and owner_f.rig:
		owner_f.rig.shake(6.0 if big else 2.5)
	if big:
		Combat.hitstop(tree, 0.07)
	queue_free()
