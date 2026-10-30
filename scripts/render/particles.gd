class_name Particles
extends RefCounted
## GPU 파티클 공장 (전부 도트 크기 정사각형 — 1~2px, 화면에서 번지지 않게)
##  · ambient: 카메라를 따라다니는 공기 속 입자 (숲: 떨어지는 잎·꽃잎·꽃가루 / 둥지: 보라 포자 / 캠프: 불티)
##  · embers: 모닥불 불티 + 연기
##  · burst: 한 번 튀는 불꽃 (타격·폭발·처치)
## 양은 화질 단계(Prefs.quality)에 비례

const PX := 1.0 / 16.0


static func _mat(emission := 0.0) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	m.vertex_color_use_as_albedo = true
	m.albedo_color = Color.WHITE
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
	m.alpha_scissor_threshold = 0.5
	if emission > 0.0:
		m.emission_enabled = true
		m.emission = Color.WHITE
		m.emission_energy_multiplier = emission
		m.emission_operator = BaseMaterial3D.EMISSION_OP_MULTIPLY
	return m


static func _quad(px: float, emission := 0.0) -> QuadMesh:
	var q := QuadMesh.new()
	q.size = Vector2(px * PX, px * PX)
	q.material = _mat(emission)
	return q


static func _grad(cols: Array) -> GradientTexture1D:
	var g := Gradient.new()
	var offs := PackedFloat32Array()
	var cs := PackedColorArray()
	for i in cols.size():
		offs.append(float(i) / maxf(1.0, cols.size() - 1))
		cs.append(Color(cols[i]))
	g.offsets = offs
	g.colors = cs
	var t := GradientTexture1D.new()
	t.gradient = g
	return t


static func _q(n: int) -> int:
	return int(n * [0.0, 0.5, 1.0, 1.6][clampi(Prefs.quality, 0, 3)])


## 공기 속 입자 (카메라 둘레 상자에서 계속)
static func ambient(kind: String) -> GPUParticles3D:
	var p := GPUParticles3D.new()
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	pm.emission_box_extents = Vector3(18, 1, 14)
	pm.turbulence_enabled = true
	pm.turbulence_noise_strength = 1.2
	pm.turbulence_noise_scale = 4.0
	pm.turbulence_influence_min = 0.05
	pm.turbulence_influence_max = 0.2
	match kind:
		"leaves":
			p.amount = _q(70)
			p.lifetime = 9.0
			pm.direction = Vector3(0.4, -1, 0.2)
			pm.spread = 25.0
			pm.initial_velocity_min = 0.6
			pm.initial_velocity_max = 1.4
			pm.gravity = Vector3(0.2, -0.35, 0.1)
			pm.color_initial_ramp = _grad(["#4c7440", "#86b05a", "#d88a3a", "#e6a8b0", "#a6c85e", "#b8662e"])
			pm.angular_velocity_min = -180.0
			pm.angular_velocity_max = 180.0
			p.draw_pass_1 = _quad(2.0)
			p.position = Vector3(0, 7, 0)
		"pollen":
			p.amount = _q(60)
			p.lifetime = 7.0
			pm.direction = Vector3(0, 1, 0)
			pm.spread = 180.0
			pm.initial_velocity_min = 0.05
			pm.initial_velocity_max = 0.25
			pm.gravity = Vector3(0.05, 0.02, 0)
			pm.emission_box_extents = Vector3(18, 2.5, 14)
			pm.color_initial_ramp = _grad(["#fff4c0", "#fffbe0", "#f0f8d0"])
			p.draw_pass_1 = _quad(1.0, 1.5)
			p.position = Vector3(0, 1.5, 0)
		"spores":
			p.amount = _q(70)
			p.lifetime = 6.0
			pm.direction = Vector3(0, 1, 0)
			pm.spread = 40.0
			pm.initial_velocity_min = 0.1
			pm.initial_velocity_max = 0.4
			pm.gravity = Vector3(0, 0.05, 0)
			pm.emission_box_extents = Vector3(18, 0.5, 14)
			pm.color_initial_ramp = _grad(["#c080ff", "#e0b0ff", "#80e0f0"])
			p.draw_pass_1 = _quad(1.0, 2.0)
			p.position = Vector3(0, 0.3, 0)
	var fade := GradientTexture1D.new()
	var fg := Gradient.new()
	fg.set_color(0, Color(1, 1, 1, 0))
	fg.set_color(1, Color(1, 1, 1, 0))
	fg.add_point(0.1, Color(1, 1, 1, 1))
	fg.add_point(0.85, Color(1, 1, 1, 1))
	fade.gradient = fg
	pm.color_ramp = fade
	p.process_material = pm
	p.local_coords = false
	p.visibility_aabb = AABB(Vector3(-30, -10, -30), Vector3(60, 30, 60))
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	p.preprocess = 4.0
	return p


## 모닥불 불티 + 연기
static func embers(parent: Node, at: Vector3) -> void:
	var p := GPUParticles3D.new()
	var pm := ParticleProcessMaterial.new()
	p.amount = _q(28)
	p.lifetime = 1.6
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 0.35
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 15.0
	pm.initial_velocity_min = 1.2
	pm.initial_velocity_max = 2.6
	pm.gravity = Vector3(0.3, 0.6, 0)
	pm.turbulence_enabled = true
	pm.turbulence_noise_strength = 2.0
	pm.color_ramp = _grad(["#fff0a0", "#ffb040", "#e05a20", "#802010"])
	p.draw_pass_1 = _quad(1.0, 3.0)
	p.process_material = pm
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(p)
	p.global_position = at + Vector3(0, 0.6, 0)
	# 연기
	var s := GPUParticles3D.new()
	var sm := ParticleProcessMaterial.new()
	s.amount = _q(16)
	s.lifetime = 3.0
	sm.direction = Vector3(0, 1, 0)
	sm.spread = 10.0
	sm.initial_velocity_min = 0.6
	sm.initial_velocity_max = 1.0
	sm.gravity = Vector3(0.25, 0.1, 0)
	sm.scale_min = 1.0
	sm.scale_max = 2.0
	sm.color_ramp = _grad(["#6a5a5a", "#5a5058", "#4a4450"])
	s.draw_pass_1 = _quad(3.0)
	s.process_material = sm
	s.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(s)
	s.global_position = at + Vector3(0, 1.3, 0)


## 한 번 튀는 불꽃 (타격·처치·폭발). col: 색 둘 (밝은 → 어두운)
static func burst(parent: Node, at: Vector3, c0: Color, c1: Color, n := 14, speed := 4.0, gravity := 12.0) -> void:
	if Prefs.quality <= 0 or parent == null:
		return
	var p := GPUParticles3D.new()
	var pm := ParticleProcessMaterial.new()
	p.amount = maxi(4, _q(n))
	p.lifetime = 0.5
	p.one_shot = true
	p.explosiveness = 1.0
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 80.0
	pm.initial_velocity_min = speed * 0.5
	pm.initial_velocity_max = speed
	pm.gravity = Vector3(0, -gravity, 0)
	pm.damping_min = 2.0
	pm.damping_max = 4.0
	var g := Gradient.new()
	g.set_color(0, c0)
	g.set_color(1, c1)
	var gt := GradientTexture1D.new()
	gt.gradient = g
	pm.color_ramp = gt
	p.draw_pass_1 = _quad(1.0, 2.0)
	p.process_material = pm
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(p)
	p.global_position = at
	p.emitting = true
	p.finished.connect(p.queue_free)
