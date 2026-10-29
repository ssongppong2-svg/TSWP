extends Node
## 시작점. 지금은 그림 확인용: 저해상도 화면 + 숲 지역 + 카메라.
##  실행 인자 (-- 뒤):
##    --shot=<경로.png>   몇 프레임 뒤 화면을 저장하고 끝낸다
##    --side              측면 시점으로 시작
##    --tour              정해진 길을 걸으며 중간에 측면 전환 (godot --write-movie 와 함께 영상 만들기용)
##    --at=x,z            시작 위치
##    --map=camp|forest   지역 (기본: 캠프)

var pv: PixelView
var world: Node3D
var rig: CamRig
var env: StageEnv
var map: Region
var walker: Walker
var args := {}
var _frame := 0


func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		var kv := String(a).trim_prefix("--").split("=", true, 1)
		args[kv[0]] = kv[1] if kv.size() > 1 else "1"
	Controls.ensure()
	pv = PixelView.new()
	add_child(pv)
	world = Node3D.new()
	world.name = "World"
	pv.vp.add_child(world)
	env = StageEnv.new()
	world.add_child(env)
	env.setup(Prefs.quality)
	map = ForestMap.new() if args.get("map", "camp") == "forest" else CampMap.new()
	world.add_child(map)
	map.build(7)
	env.apply(map.env_preset, true)
	walker = Walker.new()
	world.add_child(walker)
	var at := map.spawn
	if args.has("at"):
		var p := String(args["at"]).split(",")
		at = Vector3(float(p[0]), 0, float(p[1]))
	at.y = map.terrain.height_at(at) + 0.1
	walker.position = at
	rig = CamRig.new()
	world.add_child(rig)
	rig.setup(pv)
	rig.target = walker
	rig.focus = walker.position
	walker.rig = rig
	rig.side_changed.connect(func(on: bool):
		if on:
			map.terrain.make_cut(rig.lane_z + CamRig.CUT_NEAR)
		else:
			map.terrain.clear_cut())
	if args.has("side"):
		rig.set_side(true, 0.0, walker.position.z)
		rig.side_t = 1.0
	Audio.attach_world(world, pv.vp, func(_p): return {"room": "forest", "occl": 0})
	Audio.music(map.music)
	Audio.ambience(map.ambience)


func _process(dt: float) -> void:
	_frame += 1
	RenderingServer.global_shader_parameter_set("wind_time", Time.get_ticks_msec() / 1000.0)
	# 사람이 지나가면 풀이 밀리게 (최대 4명)
	if walker:
		var wp := walker.global_position
		RenderingServer.global_shader_parameter_set("push_a", Vector4(wp.x, wp.y, wp.z, clampf(Vector2(walker.velocity.x, walker.velocity.z).length(), 0.3, 1.0)))
	if args.has("shot") and _frame == int(args.get("frames", "45")):
		var img := get_viewport().get_texture().get_image()
		img.save_png(String(args["shot"]))
		print("saved ", args["shot"])
		get_tree().quit()
	if args.has("tour"):
		_tour(dt)


## 영상용: 숲길을 걷다가 측면으로 돌아가 점프, 다시 탑다운으로
var _tour_t := 0.0
var _tour_jumps := {}
func _tour(dt: float) -> void:
	_tour_t += dt
	var t := _tour_t
	if t < 4.6:
		walker.auto_dir = Vector3(sin(t * 0.9) * 0.5, 0, -1).normalized()
	elif t < 10.0:
		walker.auto_dir = Vector3(1, 0, 0)
	else:
		walker.auto_dir = Vector3(0.6, 0, 1).normalized()
	if t > 4.6 and not _tour_jumps.has("side"):
		_tour_jumps["side"] = true
		rig.set_side(true, 0.0, walker.position.z)
	for jt in [6.6, 7.6, 8.8]:
		if t > jt and not _tour_jumps.has(jt):
			_tour_jumps[jt] = true
			walker.jump()
	if t > 10.0 and not _tour_jumps.has("top"):
		_tour_jumps["top"] = true
		rig.set_side(false)
	if t > 13.0:
		get_tree().quit()
