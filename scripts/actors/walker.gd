class_name Walker
extends CharacterBody3D
## 임시 조작 캐릭터 (그림·카메라 확인용). 진짜 캐릭터는 다음 단계에서.
##  · 탑다운: WASD 8방향, Shift 달리기, Space 구르기
##  · 측면: A/D 이동, Space 점프 (깊이는 구간 선에 붙는다)

const WALK := 3.4
const RUN := 5.6
const ROLL := 9.0
const GRAV := 26.0
const JUMP := 8.6

var rig: CamRig
var spr: CharSprite
var shadow: MeshInstance3D
var who := "warrior"
var auto_dir := Vector3.ZERO          # 자동 걷기 (영상용)
var roll_t := 0.0
var roll_dir := Vector3.ZERO
var facing := Vector3(0, 0, 1)


func _ready() -> void:
	collision_layer = 2
	collision_mask = 1
	floor_snap_length = 0.3
	floor_max_angle = deg_to_rad(50)
	var cs := CollisionShape3D.new()
	var cap := CapsuleShape3D.new()
	cap.radius = 0.28
	cap.height = 0.9
	cs.shape = cap
	cs.position.y = 0.45
	add_child(cs)
	spr = CharSprite.new(who)
	add_child(spr.mesh)
	shadow = BB.blob(0.42, 0.2, 0.9)
	add_child(shadow)


func jump() -> void:
	if is_on_floor():
		velocity.y = JUMP
		Audio.play("jump", global_position)


func _physics_process(dt: float) -> void:
	var side := rig != null and rig.side_goal > 0.5
	var mv := Controls.move_vec()
	var yaw := rig.yaw if rig else 0.0
	var fwd := Vector3(0, 0, -1).rotated(Vector3.UP, yaw)
	var right := Vector3(1, 0, 0).rotated(Vector3.UP, yaw)
	var dir := (right * mv.x - fwd * mv.y)
	if side:
		dir = right * mv.x
	if auto_dir != Vector3.ZERO:
		dir = auto_dir
	if dir.length() > 1.0:
		dir = dir.normalized()
	var spd := RUN if Input.is_action_pressed("run") else WALK
	if not is_on_floor():
		velocity.y -= GRAV * dt
	if roll_t > 0.0:
		roll_t -= dt
		velocity.x = roll_dir.x * ROLL
		velocity.z = roll_dir.z * ROLL
	else:
		var want := dir * spd
		var acc := 30.0 if dir != Vector3.ZERO else 24.0
		velocity.x = move_toward(velocity.x, want.x, acc * dt)
		velocity.z = move_toward(velocity.z, want.z, acc * dt)
		if Input.is_action_just_pressed("roll"):
			if side:
				jump()
			elif dir != Vector3.ZERO:
				roll_t = 0.32
				roll_dir = dir.normalized()
				Audio.play("dash", global_position)
	if dir != Vector3.ZERO:
		facing = dir.normalized()
	# 그림
	if spr:
		spr.face(facing, yaw)
		if roll_t > 0.0:
			spr.play("roll")
		elif not is_on_floor():
			spr.play("jump" if velocity.y > 0.0 else "fall")
		elif Vector2(velocity.x, velocity.z).length() > 0.4:
			spr.play("run" if spd > WALK + 0.1 else "walk")
		else:
			spr.play("idle")
		spr.update(dt)
	if side and rig:
		# 측면: 구간 선에 붙는다 (앞뒤로는 못 움직인다)
		var n := rig.lane_normal()
		var off := global_position.dot(n) - rig.lane_z
		velocity -= n * velocity.dot(n)
		velocity -= n * off * 8.0
	if Input.is_action_just_pressed("debug_side") and rig:
		rig.set_side(rig.side_goal < 0.5, 0.0, global_position.z)
	move_and_slide()
