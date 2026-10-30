class_name CamRig
extends Node3D
## 도트 카메라: 직교 투영, 1칸(1m) = 16픽셀.
##  · 탑다운 3/4 시점(위에서 비스듬히) ↔ 측면 시점(옆에서)을 카메라가 돌아가며 부드럽게 전환한다.
##  · 위치는 화면 픽셀 단위로 끊고, 남은 소수점은 PixelView 가 화면을 밀어 처리 (도트 흔들림 없음).
##  · 빌보드(나무·캐릭터)가 기울어 보이지 않게 bb_stretch(=1/cos 기울기)를 셰이더 전역값으로 알려 준다.

const PPU := 16.0                    # 1m 당 픽셀
const TOP_PITCH := deg_to_rad(52.0)  # 탑다운 3/4: 아래로 52도
const SIDE_PITCH := deg_to_rad(7.0)  # 측면: 거의 수평 (땅 윗면이 살짝 보이게)
const DIST := 60.0

var cam: Camera3D
var ear: AudioListener3D              # 소리 귀: 카메라(60m 뒤)가 아니라 초점 자리
var view: PixelView
var target: Node3D
var focus := Vector3.ZERO
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
	shake_amt = maxf(shake_amt, a * (1.0 if Prefs.shake else 0.3))


## 카메라 킥: 맞은 방향으로 툭 밀렸다 돌아온다 (픽셀)
var _kick := Vector2.ZERO
func kick(px: Vector2) -> void:
	_kick += px * (1.0 if Prefs.shake else 0.3)


func _process(dt: float) -> void:
	if cam == null:
		return
	# 전환 속도: 0.7초에 걸쳐 돌아간다
	side_t = move_toward(side_t, side_goal, dt / 0.7)
	yaw = lerp_angle(yaw, yaw_goal if side_goal > 0.5 else 0.0, 1.0 - exp(-6.0 * dt))
	if target and is_instance_valid(target):
		# 측면에선 화면 아래쪽에 땅이 덜 보이게 초점을 위로
		var k_side := side_t * side_t * (3.0 - 2.0 * side_t)
		var want := target.global_position + Vector3(0, 0.6 + 2.4 * k_side, 0) + look_ahead
		focus = focus.lerp(want, 1.0 - exp(-9.0 * dt))
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
		RenderingServer.global_shader_parameter_set("focus_screen", Vector4(sp.x, sp.y, tp.dot(hz), 34.0))
	view.sub_offset = Vector2((cr - sr) * PPU, -(cu - su) * PPU)
	if view.rays:
		var rm := view.rays.material as ShaderMaterial
		rm.set_shader_parameter("cam_px", Vector2(sr * PPU, su * PPU))
		rm.set_shader_parameter("time", Time.get_ticks_msec() / 1000.0)
	cam.size = float(PixelView.GH + PixelView.MARGIN * 2) / PPU / zoom
	RenderingServer.global_shader_parameter_set("bb_stretch", 1.0 / maxf(0.2, cos(p)))
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
		out.z = clampf(f.z, bounds.position.y + depth_h, bounds.end.y - depth_h + 0.6)
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
