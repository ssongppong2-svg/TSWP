class_name PlayerInput
extends RefCounted
## 한 플레이어의 입력 한 묶음. 내 컴퓨터면 키보드·마우스·패드에서 채우고,
## 온라인이면 네트워크에서 받은 값으로 채운다 (플레이어 코드는 Input 을 직접 보지 않는다)

var move := Vector2.ZERO          # 화면 기준 (-1~1)
var aim_world := Vector3.ZERO     # 조준점 (세계) — has_aim 일 때만
var has_aim := false
var aim_stick := Vector2.ZERO
var run := false
var roll := false                 # 이번 프레임에 눌림
var roll_held := false
var attack := false
var attack_held := false
var skill := false
var interact := false
var switch_job := false
var _mouse_t := -10.0


## 내 입력 읽기
func poll_local(rig: CamRig, aim_y: float) -> void:
	move = Controls.move_vec()
	run = Input.is_action_pressed("run")
	roll = Input.is_action_just_pressed("roll")
	roll_held = Input.is_action_pressed("roll")
	attack = Input.is_action_just_pressed("attack")
	attack_held = Input.is_action_pressed("attack")
	skill = Input.is_action_just_pressed("skill")
	interact = Input.is_action_just_pressed("interact")
	switch_job = Input.is_action_just_pressed("switch_job")
	aim_stick = Controls.aim_stick()
	if Input.get_last_mouse_velocity().length() > 1.0:
		_mouse_t = Time.get_ticks_msec() / 1000.0
	has_aim = rig != null and Time.get_ticks_msec() / 1000.0 - _mouse_t < 4.0
	if has_aim:
		aim_world = rig.screen_to_plane(rig.get_tree().root.get_mouse_position(), aim_y)


func clear() -> void:
	move = Vector2.ZERO
	run = false
	roll = false
	roll_held = false
	attack = false
	attack_held = false
	skill = false
	interact = false
	switch_job = false
