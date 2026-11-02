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
var ult := false                  # 궁극기 (R)
var use_item := false             # 소비 아이템 (F)
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
	ping = Input.is_action_just_pressed("ping")
	ult = Input.is_action_just_pressed("ult")
	use_item = Input.is_action_just_pressed("use_item")
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
	ult = false
	use_item = false
	emote = false
	ping = false


# ------------------------------------------------------------------ 로컬 협동: 패드 하나 = 한 사람
## 패드 dev 에서 읽기 (InputMap 을 거치지 않고 장치 번호로 직접 — 패드끼리 섞이지 않게)
##  왼스틱/십자 이동 · 오른스틱 조준 · A 구르기 · X/RT 공격 · Y 스킬 · RB 궁극기 · B 상호작용·들기
##  LB 달리기 · 십자 오른쪽 소비 · 십자 위 이모트 · R3 핑
var _prev := {}
var emote := false
var ping := false


func _btn(dev: int, b: int) -> bool:
	return Input.is_joy_button_pressed(dev, b)


func _just(dev: int, key: String, now: bool) -> bool:
	var was: bool = _prev.get(key, false)
	_prev[key] = now
	return now and not was


func poll_pad(dev: int, player: Node3D) -> void:
	var lx := Input.get_joy_axis(dev, JOY_AXIS_LEFT_X)
	var ly := Input.get_joy_axis(dev, JOY_AXIS_LEFT_Y)
	move = Vector2(lx, ly)
	if move.length() < 0.22:
		move = Vector2.ZERO
	if _btn(dev, JOY_BUTTON_DPAD_LEFT):
		move.x = -1.0
	if _btn(dev, JOY_BUTTON_DPAD_RIGHT) and false:
		move.x = 1.0
	if _btn(dev, JOY_BUTTON_DPAD_DOWN):
		move.y = 1.0
	move = move.limit_length(1.0)
	run = _btn(dev, JOY_BUTTON_LEFT_SHOULDER)
	roll_held = _btn(dev, JOY_BUTTON_A)
	roll = _just(dev, "a", roll_held)
	attack_held = _btn(dev, JOY_BUTTON_X) or Input.get_joy_axis(dev, JOY_AXIS_TRIGGER_RIGHT) > 0.5
	attack = _just(dev, "x", attack_held)
	skill = _just(dev, "y", _btn(dev, JOY_BUTTON_Y))
	ult = _just(dev, "rb", _btn(dev, JOY_BUTTON_RIGHT_SHOULDER))
	interact = _just(dev, "b", _btn(dev, JOY_BUTTON_B))
	use_item = _just(dev, "dr", _btn(dev, JOY_BUTTON_DPAD_RIGHT))
	emote = _just(dev, "du", _btn(dev, JOY_BUTTON_DPAD_UP))
	ping = _just(dev, "r3", _btn(dev, JOY_BUTTON_RIGHT_STICK))
	switch_job = false
	var r := Vector2(Input.get_joy_axis(dev, JOY_AXIS_RIGHT_X), Input.get_joy_axis(dev, JOY_AXIS_RIGHT_Y))
	aim_stick = r if r.length() > 0.3 else Vector2.ZERO
	has_aim = false
