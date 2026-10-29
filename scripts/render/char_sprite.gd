class_name CharSprite
extends RefCounted
## 캐릭터 그림 (chars.png): <캐릭터>_<동작>_<방향><번호>
##  · 방향은 화면 기준: s 앞 · n 뒤 · e 옆 (왼쪽은 뒤집기)
##  · 동작마다 초당 프레임 수가 다르고, 한 번만 도는 동작(attack·hurt)은 마지막 프레임에 멈춘다

const ATLAS := "res://assets/sprites/chars.png"
const FPS := {"idle": 2.5, "walk": 8.0, "run": 11.0, "roll": 14.0, "jump": 1.0, "fall": 1.0, "attack": 14.0, "hurt": 1.0}
const COUNT := {"idle": 2, "walk": 4, "run": 4, "roll": 4, "jump": 1, "fall": 1, "attack": 3, "hurt": 1}
const ONCE := ["attack", "hurt"]

var mesh: MeshInstance3D
var mat: ShaderMaterial
var who := "warrior"
var anim := "idle"
var dir := "s"
var flip := false
var t := 0.0


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


func update(dt: float) -> void:
	t += dt
	var n: int = COUNT[anim]
	var i := int(t * FPS[anim])
	i = mini(i, n - 1) if anim in ONCE else i % n
	mat.set_shader_parameter("region", BB.region_uv(ATLAS, "%s_%s_%s%d" % [who, anim, dir, i]))
	mat.set_shader_parameter("flip", 1.0 if flip else 0.0)


func done() -> bool:
	return anim in ONCE and t * FPS[anim] >= COUNT[anim]
