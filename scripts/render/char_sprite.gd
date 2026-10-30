class_name CharSprite
extends RefCounted
## 캐릭터 그림 (chars.png): <캐릭터>_<동작>_<방향><번호>
##  · 방향은 화면 기준: s 앞 · n 뒤 · e 옆 (왼쪽은 뒤집기)
##  · 동작마다 초당 프레임 수가 다르고, 한 번만 도는 동작(attack·hurt)은 마지막 프레임에 멈춘다

const ATLAS := "res://assets/sprites/chars.png"
const FPS := {"idle": 5.0, "walk": 11.0, "run": 15.0, "roll": 16.0, "jump": 1.0, "fall": 1.0, "attack": 18.0, "hurt": 1.0}
const COUNT := {"idle": 4, "walk": 6, "run": 6, "roll": 6, "jump": 1, "fall": 1, "attack": 5, "hurt": 1}
const ONCE := ["attack", "hurt"]

var mesh: MeshInstance3D
var mat: ShaderMaterial
var who := "warrior"
var anim := "idle"
var dir := "s"
var flip := false
var t := 0.0
# 가상 뼈 (스프링): 가로·세로 늘림, 기울기 — 프레임 사이를 물리로 채운다
var bone_sx := SpringBone.new(1.0, 260.0, 13.0)
var bone_sy := SpringBone.new(1.0, 260.0, 13.0)
var bone_lean := SpringBone.new(0.0, 160.0, 12.0)


func _init(char_name: String) -> void:
	who = char_name
	mesh = BB.make(ATLAS, "%s_idle_s0" % who, {"unique": true, "up_normal": 0.6})
	mat = mesh.material_override


func play(a: String, restart := false) -> void:
	if a != anim or restart:
		anim = a
		t = 0.0


## 세계 방향 → 화면 방향 (카메라 yaw 기준)
func face(world_dir: Vector3, yaw: float) -> void:
	if world_dir.length() < 0.01:
		return
	var right := Vector3(1, 0, 0).rotated(Vector3.UP, yaw)
	var down := Vector3(0, 0, 1).rotated(Vector3.UP, yaw)
	var sx := world_dir.dot(right)
	var sy := world_dir.dot(down)
	if absf(sx) > absf(sy) * 0.85:
		dir = "e"
		flip = sx < 0.0
	else:
		dir = "s" if sy > 0.0 else "n"
		flip = sx < -0.2


## 점프할 때 길쭉하게 · 착지할 때 납작하게 (+면 세로로 늘어남)
func stretch(k: float) -> void:
	bone_sy.kick(k * 9.0)
	bone_sx.kick(-k * 7.0)


## 맞았을 때 흔들림 / 공격할 때 앞으로 쏠림 (화면 방향 +1 오른쪽)
func wobble(side: float, k := 1.0) -> void:
	bone_lean.kick(side * 3.0 * k)


func lean_to(v: float) -> void:
	bone_lean.target = clampf(v, -0.18, 0.18)


## 잔상 (구르기·돌진): 지금 그림을 복사해 푸르게 빛나다 사라진다
func ghost(parent: Node, at: Vector3) -> void:
	var g := MeshInstance3D.new()
	g.mesh = mesh.mesh
	var m := mat.duplicate() as ShaderMaterial
	m.set_shader_parameter("tint", Color(0.55, 0.75, 1.0))
	m.set_shader_parameter("emission", 0.6)
	m.set_shader_parameter("flash", 0.3)
	g.material_override = m
	g.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(g)
	g.global_position = at
	var tw := g.create_tween()
	tw.tween_property(g, "scale", Vector3(0.6, 0.85, 1.0), 0.18)
	tw.tween_callback(g.queue_free)


func update(dt: float) -> void:
	t += dt
	mat.set_shader_parameter("squash", Vector2(bone_sx.step(dt), bone_sy.step(dt)))
	mat.set_shader_parameter("skew", bone_lean.step(dt))
	var n: int = COUNT[anim]
	var i := int(t * FPS[anim])
	i = mini(i, n - 1) if anim in ONCE else i % n
	mat.set_shader_parameter("region", BB.region_uv(ATLAS, "%s_%s_%s%d" % [who, anim, dir, i]))
	mat.set_shader_parameter("flip", 1.0 if flip else 0.0)


func done() -> bool:
	return anim in ONCE and t * FPS[anim] >= COUNT[anim]
