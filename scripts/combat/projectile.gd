class_name Projectile
extends Node3D
## 곧게 날아가는 것 (화살 · 주사 · 포자 · 거미줄)
##  · 맞는 대상: 기본은 다른 팀. hits_allies 면 아군도 (궁수 화살은 아군에 50%)
##  · on_hit(대상) 콜백이 있으면 그걸로, 없으면 hit 을 그대로 준다
##  · 땅(지형 높이)에 닿거나 사거리를 넘으면 사라진다

var vel := Vector3.ZERO
var owner_f: Fighter
var hit: Hit
var radius := 0.35
var life := 1.0
var hits_allies := false
var pierce := 0
var on_hit: Callable
var sprite: MeshInstance3D
var _hit_set := {}
var terrain: Terrain
var trail_t := 0.0


static func fire(parent: Node, from: Vector3, dir: Vector3, speed: float, range_: float, atlas: String, frame: String, owner_: Fighter, hit_: Hit) -> Projectile:
	var p := Projectile.new()
	p.vel = dir.normalized() * speed
	p.life = range_ / speed
	p.owner_f = owner_
	p.hit = hit_
	p.terrain = owner_.terrain if owner_ else null
	p.sprite = BB.make(atlas, frame, {"unique": true, "center": true, "up_normal": 1.0, "receive_shade": false})
	p.add_child(p.sprite)
	parent.add_child(p)
	p.global_position = from
	p._orient()
	return p


func _orient() -> void:
	var cam := get_viewport().get_camera_3d()
	if cam == null:
		return
	var b := cam.global_transform.basis
	var sx := vel.dot(b.x)
	var sy := vel.dot(b.y)
	(sprite.material_override as ShaderMaterial).set_shader_parameter("spin", atan2(sy, sx))


func _physics_process(dt: float) -> void:
	position += vel * dt
	life -= dt
	_orient()
	if life <= 0.0:
		queue_free()
		return
	if terrain and global_position.y < terrain.height_at(global_position) + 0.05:
		Fx.pop(get_parent(), global_position, ["spark0", "spark1"], 20.0)
		queue_free()
		return
	for f in Combat.in_radius(get_tree(), global_position - Vector3(0, 0.4, 0), radius, 0.9):
		var fi := f as Fighter
		if fi == owner_f or _hit_set.has(fi):
			continue
		if owner_f and fi.team == owner_f.team and not hits_allies:
			continue
		_hit_set[fi] = true
		if on_hit.is_valid():
			on_hit.call(fi, self)
		else:
			var h := hit
			h.knock = vel.normalized() * h.knock.length() if h.knock != Vector3.ZERO else Vector3.ZERO
			fi.take(h)
		Fx.pop(get_parent(), global_position, ["spark0", "spark1", "spark2"], 24.0)
		if pierce <= 0:
			queue_free()
			return
		pierce -= 1
