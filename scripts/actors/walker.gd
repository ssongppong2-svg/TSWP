class_name Walker
extends CharacterBody3D
## 조작 캐릭터 (임시 — 진짜 직업·능력은 다음 단계에서 붙인다).
##  · 탑다운: WASD 8방향, Shift 달리기, Space 구르기(무적은 아직), 왼클릭 베기(마우스 방향)
##  · 측면: A/D 이동, Space 점프 (코요테 타임·입력 미리 받기·떼면 낮게), 깊이는 구간 선에 붙는다
##  · 조준: 마우스가 움직였으면 마우스, 패드 오른스틱을 쓰면 스틱, 둘 다 없으면 보는 방향

const WALK := 3.4
const RUN := 5.6
const ROLL := 9.0
const ROLL_T := 0.32
const GRAV := 26.0
const JUMP := 8.6
const COYOTE := 0.1
const BUFFER := 0.12
const ATTACK_T := 0.26
const ATTACK_CD := 0.34

var rig: CamRig
var terrain: Terrain
var spr: CharSprite
var shadow: MeshInstance3D
var who := "warrior"
var auto_dir := Vector3.ZERO          # 자동 걷기 (영상용)
var roll_t := 0.0
var roll_dir := Vector3.ZERO
var facing := Vector3(0, 0, 1)
var aim := Vector3(0, 0, 1)           # 조준 방향 (수평, 길이 1)
var attack_t := 0.0
var attack_cd := 0.0
var _coyote := 0.0
var _buffer := 0.0
var _was_floor := true
var _step_t := 0.0
var _mouse_t := -10.0                 # 마지막으로 마우스가 움직인 시각
var _fall_v := 0.0


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


func _input(e: InputEvent) -> void:
	if e is InputEventMouseMotion:
		_mouse_t = Time.get_ticks_msec() / 1000.0


func jump() -> void:
	if is_on_floor() or _coyote > 0.0:
		velocity.y = JUMP
		_coyote = 0.0
		_buffer = 0.0
		Audio.play("jump", global_position)
		Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 16.0)


func _side() -> bool:
	return rig != null and rig.side_goal > 0.5


## 조준 방향 갱신
func _update_aim(yaw: float, right: Vector3, fwd: Vector3) -> void:
	var stick := Controls.aim_stick()
	if stick != Vector2.ZERO:
		aim = (right * stick.x - fwd * stick.y).normalized()
	elif rig and Time.get_ticks_msec() / 1000.0 - _mouse_t < 3.0:
		var mp := get_tree().root.get_mouse_position()
		var p := rig.screen_to_plane(mp, global_position.y + 0.5)
		var d := p - global_position
		d.y = 0.0
		if _side():
			# 측면: 화면 좌우만 (위아래 조준은 나중에 던지기에)
			var sx := d.dot(right)
			aim = right * (1.0 if sx >= 0.0 else -1.0)
		elif d.length() > 0.2:
			aim = d.normalized()
	else:
		aim = facing


func _physics_process(dt: float) -> void:
	var side := _side()
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
	_update_aim(yaw, right, fwd)
	var running := Input.is_action_pressed("run")
	var spd := RUN if running else WALK
	if attack_t > 0.0:
		spd *= 0.35
	attack_t = maxf(0.0, attack_t - dt)
	attack_cd = maxf(0.0, attack_cd - dt)
	# ---- 중력·점프
	if is_on_floor():
		_coyote = COYOTE
	else:
		_coyote = maxf(0.0, _coyote - dt)
		velocity.y -= GRAV * dt
		_fall_v = minf(_fall_v, velocity.y)
	_buffer = maxf(0.0, _buffer - dt)
	if side and Input.is_action_just_pressed("roll"):
		_buffer = BUFFER
	if side and _buffer > 0.0:
		jump()
	if side and not Input.is_action_pressed("roll") and velocity.y > 2.5 and auto_dir == Vector3.ZERO:
		velocity.y = move_toward(velocity.y, 2.5, GRAV * 2.0 * dt)       # 떼면 낮게
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
		if not side and Input.is_action_just_pressed("roll") and dir != Vector3.ZERO:
			roll_t = ROLL_T
			roll_dir = dir.normalized()
			Audio.play("dash", global_position)
			Fx.pop(get_parent(), global_position, ["dust0", "dust1", "dust2", "dust3"], 16.0)
	# ---- 베기
	if Input.is_action_just_pressed("attack") and attack_cd <= 0.0 and roll_t <= 0.0:
		_attack()
	if dir != Vector3.ZERO and attack_t <= 0.0:
		facing = dir.normalized()
	# ---- 측면: 구간 선에 붙는다 (앞뒤로는 못 움직인다)
	if side and rig:
		var n := rig.lane_normal()
		var off := global_position.dot(n) - rig.lane_z
		velocity -= n * velocity.dot(n)
		velocity -= n * off * 8.0
	if Input.is_action_just_pressed("debug_side") and rig:
		rig.set_side(rig.side_goal < 0.5, 0.0, global_position.z)
	move_and_slide()
	# ---- 착지 · 발소리 · 먼지
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
			if running:
				Fx.pop(get_parent(), global_position - facing * 0.2, ["dust1", "dust2", "dust3"], 14.0)
	# ---- 그림
	spr.face(aim if attack_t > 0.0 else facing, yaw)
	if attack_t > 0.0:
		spr.play("attack")
	elif roll_t > 0.0:
		spr.play("roll")
	elif not on_floor:
		spr.play("jump" if velocity.y > 0.0 else "fall")
	elif hv > 0.4:
		spr.play("run" if running else "walk")
	else:
		spr.play("idle")
	spr.update(dt)


func _step_sound() -> String:
	if terrain == null:
		return "step_grass"
	var m := terrain.mat_at(global_position)
	return "step_stone" if m == Region.G.STONE else "step_grass"


func _attack() -> void:
	attack_t = ATTACK_T
	attack_cd = ATTACK_CD
	spr.play("attack", true)
	Audio.play("swing", global_position)
	velocity.x += aim.x * 2.4
	velocity.z += aim.z * 2.4
	var frames := ["slash0", "slash1", "slash2", "slash3"]
	if _side():
		var f := Fx.pop(get_parent(), global_position + aim * 0.7 + Vector3(0, -0.75, 0), frames, 18.0)
		if aim.dot(Vector3(1, 0, 0).rotated(Vector3.UP, rig.yaw)) < 0.0:
			f.mesh.scale.x = -1.0
	else:
		var ang := atan2(aim.z, aim.x)
		Fx.flat_at(get_parent(), global_position + aim * 0.55, ang, frames, 18.0)
