class_name CamRig
extends Node3D

## 도트 카메라: 직교 투영, 1칸(1m) = 16픽셀.
##  · 탑다운 3/4 시점(위에서 비스듬히) ↔ 측면 시점(옆에서)을 카메라가 돌아가며 부드럽게 전환한다.
##  · 위치는 화면 픽셀 단위로 끊고, 남은 소수점은 PixelView 가 화면을 밀어 처리 (도트 흔들림 없음).
##  · 빌보드(나무·캐릭터)가 기울어 보이지 않게 bb_stretch(=1/cos 기울기)를 셰이더 전역값으로 알려 준다.
##  · 따라가기 (걸을 때 화면이 출렁이지 않게):
##      - 물리 틱에서 움직인다 (캐릭터와 같은 박자 — 화면 주사율이 달라도 덜컹임 없음)
##      - 여유 칸(dead-zone): 캐릭터가 가운데 작은 상자 안에서 움직이면 화면은 가만히,
##        상자 끝을 밀 때만 정확히 같이 움직인다 (지연 보간이 없어 캐릭터가 픽셀 사이에서 떨리지 않음)
##      - 높이는 발 디딘 땅 기준으로만 천천히 (점프·계단에 화면이 위아래로 튀지 않게)
##      - 여럿(로컬 협동)이면 살아 있는 사람들의 가운데

## 셰이더 전역값을 스크립트에서도 읽을 수 있게 (global_shader_parameter_get 은 느리다)
static var g_bb := 1.6
static var g_fwd := Vector3(0, -0.8, -0.6)

const PPU := 16.0                    # 1m 당 픽셀
const TOP_PITCH := deg_to_rad(52.0)  # 탑다운 3/4: 아래로 52도
const SIDE_PITCH := deg_to_rad(7.0)  # 측면: 거의 수평 (땅 윗면이 살짝 보이게)
const DIST := 60.0

var cam: Camera3D
var ear: AudioListener3D              # 소리 귀: 카메라(60m 뒤)가 아니라 초점 자리
var view: PixelView
var target: Node3D
var targets: Array = []               # 여럿이면 가운데를 본다 (비면 target 하나)
var focus := Vector3.ZERO
var _goal := Vector3.ZERO
var _goal_set := false
var _ground_y := 0.0
var look_ahead := Vector3.ZERO
var side_t := 0.0                    # 0 탑다운 … 1 측면 (현재)
var side_goal := 0.0
var yaw := 0.0                       # 측면일 때 바라보는 방향 (구간마다 다를 수 있음)
var yaw_goal := 0.0
var zoom := 1.0
var bounds := Rect2()                # 지역 크기 (x, z) — 화면이 지역 밖을 보이지 않게 초점을 묶는다
var shake_amt := 0.0
var _shake_t := 0.0
var lane_z := 0.0                    # 측면 구간 선: 카메라 정면 방향 깊이 (yaw 0 이면 세계 z)
const CUT_NEAR := 0.6                # 측면에서 단면이 선보다 이만큼 앞에 (선 위의 풀·사람은 남게)
signal side_changed(on: bool)


func setup(v: PixelView) -> void:
	view = v
	cam = Camera3D.new()
	cam.projection = Camera3D.PROJECTION_ORTHOGONAL
	cam.keep_aspect = Camera3D.KEEP_HEIGHT
	cam.size = float(PixelView.GH + PixelView.MARGIN * 2) / PPU
	cam.near = 0.5
	cam.far = 200.0
	add_child(cam)
	cam.make_current()
	ear = AudioListener3D.new()
	add_child(ear)
	ear.make_current()


## 측면 시점으로 (on=true) · 되돌리기. yaw: 측면에서 카메라가 바라보는 방향(라디안, 0 = 북쪽 -Z)
func set_side(on: bool, yaw_rad := 0.0, lane := 0.0) -> void:
	var was := side_goal > 0.5
	side_goal = 1.0 if on else 0.0
	if on:
		yaw_goal = yaw_rad
		lane_z = lane
	if was != on:
		side_changed.emit(on)


## 측면 구간 선의 바깥 방향(카메라 쪽, 수평)
func lane_normal() -> Vector3:
	return Basis(Vector3.UP, yaw_goal if side_goal > 0.5 else yaw).z


func pitch() -> float:
	var k := side_t * side_t * (3.0 - 2.0 * side_t)       # 부드럽게
	return lerpf(TOP_PITCH, SIDE_PITCH, k)


func shake(a: float) -> void:
	shake_amt = maxf(shake_amt, a * _shake_mul())


func _shake_mul() -> float:
	return (Prefs.shake_k if Prefs.shake else 0.0) * 0.8


## 카메라 킥: 맞은 방향으로 툭 밀렸다 돌아온다 (픽셀)
var _kick := Vector2.ZERO
func kick(px: Vector2) -> void:
	_kick += px * _shake_mul()


## 지금 따라갈 자리 (여럿이면 살아 있는 사람들의 가운데)
func _follow_point() -> Variant:
	var pts: Array = []
	for t in targets:
		if is_instance_valid(t) and not (t is Fighter and (t as Fighter).dead):
			pts.append((t as Node3D).global_position)
	if pts.is_empty() and target and is_instance_valid(target):
		pts.append(target.global_position)
	if pts.is_empty():
		return null
	var c := Vector3.ZERO
	for p in pts:
		c += p
	return c / pts.size()


func _physics_process(dt: float) -> void:
	if cam == null:
		return
	# 전환 속도: 0.7초에 걸쳐 돌아간다
	side_t = move_toward(side_t, side_goal, dt / 0.7)
	yaw = lerp_angle(yaw, yaw_goal if side_goal > 0.5 else 0.0, 1.0 - exp(-6.0 * dt))
	var fp: Variant = _follow_point()
	if fp != null:
		var tp: Vector3 = fp
		var k_side := side_t * side_t * (3.0 - 2.0 * side_t)
		if not _goal_set:
			_goal = tp
			_ground_y = tp.y
			_goal_set = true
		# 높이: 땅에 서 있을 때만 따라간다 (점프 중엔 크게 벗어날 때만)
		var on_floor := target is CharacterBody3D and (target as CharacterBody3D).is_on_floor()
		if on_floor or absf(tp.y - _ground_y) > 2.5 or side_t > 0.5:
			_ground_y = lerpf(_ground_y, tp.y, 1.0 - exp(-(6.0 if side_t > 0.5 else 4.0) * dt))
		# 여유 칸 (화면 기준 좌우·앞뒤 m)
		var dz := [Vector2(1.0, 0.7), Vector2(0.0, 0.0), Vector2(2.4, 1.5)][clampi(Prefs.cam_follow, 0, 2)] as Vector2
		if targets.size() > 1:
			dz = Vector2(0.4, 0.3)
		var right := Vector3(1, 0, 0).rotated(Vector3.UP, yaw)
		var fwd := Vector3(0, 0, 1).rotated(Vector3.UP, yaw)
		var d := tp - _goal
		var dr := d.dot(right)
		var df := d.dot(fwd)
		if side_t > 0.5:
			df = 0.0                                    # 측면: 앞뒤는 구간 선에 붙어 있다
		var mr := signf(dr) * maxf(0.0, absf(dr) - dz.x)
		var mf := signf(df) * maxf(0.0, absf(df) - dz.y)
		_goal += right * mr + fwd * mf
		if side_t > 0.5:
			_goal += fwd * (tp - _goal).dot(fwd)
		focus = Vector3(_goal.x, _ground_y + 0.6 + 2.4 * k_side, _goal.z) + look_ahead
	var fc := _clamped(focus)
	var p := pitch()
	var basis := Basis(Vector3.UP, yaw) * Basis(Vector3.RIGHT, -p)
	# 흔들림
	_shake_t += dt * 38.0
	shake_amt = move_toward(shake_amt, 0.0, dt * 9.0)
	_kick = _kick.lerp(Vector2.ZERO, 1.0 - exp(-14.0 * dt))
	var sh := (Vector2(sin(_shake_t * 1.3), cos(_shake_t * 1.7)) * shake_amt + _kick) / PPU
	var back := basis.z
	var right := basis.x
	var up := basis.y
	var c := fc + back * DIST + right * sh.x + up * sh.y
	# 픽셀 끊기: 카메라 평면의 오른쪽·위 성분만
	var cr := c.dot(right)
	var cu := c.dot(up)
	var cb := c.dot(back)
	var sr := roundf(cr * PPU) / PPU
	var su := roundf(cu * PPU) / PPU
	cam.global_transform = Transform3D(basis, right * sr + up * su + back * cb)
	ear.global_transform = Transform3D(Basis(Vector3.UP, yaw), focus)
	# 주인공 화면 위치 (가리는 나무 비춰 보기)
	if target and is_instance_valid(target):
		var tp := target.global_position + Vector3(0, 0.7, 0)
		var sp := cam.unproject_position(tp)
		var hz := Vector3(-basis.z.x, 0, -basis.z.z).normalized()
		RenderingServer.global_shader_parameter_set("focus_screen", Vector4(sp.x, sp.y, tp.dot(hz), 24.0))     # 반지름 24px (캐릭터 키만큼)
	view.sub_offset = Vector2((cr - sr) * PPU, -(cu - su) * PPU)
	if view.rays:
		var rm := view.rays.material as ShaderMaterial
		rm.set_shader_parameter("cam_px", Vector2(sr * PPU, su * PPU))
		rm.set_shader_parameter("time", Time.get_ticks_msec() / 1000.0)
	cam.size = float(PixelView.GH + PixelView.MARGIN * 2) / PPU / zoom
	g_bb = 1.0 / maxf(0.2, cos(p))
	g_fwd = -basis.z
	RenderingServer.global_shader_parameter_set("bb_stretch", g_bb)
	RenderingServer.global_shader_parameter_set("cam_pitch", p)
	RenderingServer.global_shader_parameter_set("cam_fwd", -basis.z)
	# 단면: 측면으로 갈수록 멀리서부터 선 앞(CUT_NEAR)까지 다가온다
	var k := side_t * side_t * (3.0 - 2.0 * side_t)
	var n := lane_normal()
	var dist := lerpf(80.0, CUT_NEAR, k) if k > 0.001 else 100000.0
	RenderingServer.global_shader_parameter_set("cut_plane", Vector4(n.x, n.y, n.z, lane_z + dist))
	RenderingServer.global_shader_parameter_set("side_amt", k)
	RenderingServer.global_shader_parameter_set("lane_plane", Vector4(n.x, n.y, n.z, lane_z))


## 초점을 지역 안으로: 화면 가장자리가 지역 밖(빈 하늘)을 보이지 않게
func _clamped(f: Vector3) -> Vector3:
	if bounds.size == Vector2.ZERO:
		return f
	var hw := PixelView.GW * 0.5 / PPU / zoom
	var hh := PixelView.GH * 0.5 / PPU / zoom
	var p := pitch()
	var out := f
	if bounds.size.x > hw * 2.0:
		out.x = clampf(f.x, bounds.position.x + hw, bounds.end.x - hw)
	# 탑다운에서 세로: 화면 위·아래가 땅에 닿는 깊이 (높이는 대충 초점 높이 기준)
	var depth_h := hh / maxf(sin(p), 0.2)
	if side_t < 0.5 and bounds.size.y > depth_h * 2.0:
		# 위쪽은 HUD(약 40px) 만큼 더 열어 둔다 — 북쪽 끝에 선 캐릭터가 HUD 밑에 깔리지 않게 (그 너머는 원경이 보인다)
		out.z = clampf(f.z, bounds.position.y + depth_h - 2.4, bounds.end.y - depth_h + 0.6)
	return out


## 화면(창) 좌표 → 높이 plane_y 평면 위 세계 좌표
func screen_to_plane(p: Vector2, plane_y: float) -> Vector3:
	var lp := view.to_low(p)
	var o := cam.project_ray_origin(lp)
	var d := cam.project_ray_normal(lp)
	if absf(d.y) < 0.0001:
		return o
	var t := (plane_y - o.y) / d.y
	return o + d * t


## 화면 좌표 → 카메라를 지나는 측면 평면(z = plane_z) 위 세계 좌표 (측면 조준용)
func screen_to_lane(p: Vector2, plane_z: float) -> Vector3:
	var lp := view.to_low(p)
	var o := cam.project_ray_origin(lp)
	var d := cam.project_ray_normal(lp)
	var n := cam.global_transform.basis.z
	var t := (plane_z - o.dot(n)) / d.dot(n) if absf(d.dot(n)) > 0.0001 else 0.0
	return o + d * t
