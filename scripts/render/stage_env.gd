class_name StageEnv
extends Node3D
## 하늘·해·주변광·간접광(GI)·색 보정. 지역(숲·캠프·둥지) 분위기 프리셋.
##  · 해: 지역마다 높이·방향·색 (숲 = 늦은 오후 황금빛, 왼쪽 위에서 길게)
##  · 간접광: SSIL (화면 공간 간접광 — 밝은 땅빛이 나무 밑·벽에 번진다) + SSAO (구석 어둡게) — 화질 단계에 따라
##  · 반구 환경광: 하늘색(위) · 땅색(아래)을 섞은 주변광 — 그늘이 탁한 회색이 되지 않게
##  · 색 보정: 3D LUT 로 그림자는 차갑게, 밝은 곳은 따뜻하게 (스플릿 토닝) + 지역별 채도
## 직교 카메라가 60m 뒤에 있어서 거리 안개·부피 안개는 화면 전체를 똑같이 덮는다 → 끔. 빛줄기는 GodRays(화면 효과)로.

const PRESETS := {
	"forest": {"sun": Color("#ffd8a0"), "sun_e": 0.78, "elev": -42.0, "yaw": -132.0,
		"amb": Color("#b8cce8"), "amb_e": 0.40, "sky": Color("#8ab8b0"),
		"cool": Color("#2a4a6a"), "warm": Color("#ffe0b0"), "sat": 1.08, "rays": 0.55, "ray_col": Color("#ffe6b0"), "leaf": 0.55},
	"camp": {"sun": Color("#ffb070"), "sun_e": 0.42, "elev": -24.0, "yaw": -120.0,
		"amb": Color("#9a90c8"), "amb_e": 0.38, "sky": Color("#c08a7a"),
		"cool": Color("#3a2a5a"), "warm": Color("#ffc890"), "sat": 1.06, "rays": 0.35, "ray_col": Color("#ffc080"), "leaf": 0.35},
	"nest": {"sun": Color("#c8a0e0"), "sun_e": 0.30, "elev": -55.0, "yaw": -140.0,
		"amb": Color("#a090c8"), "amb_e": 0.38, "sky": Color("#24182c"),
		"cool": Color("#1a1030"), "warm": Color("#e0a0ff"), "sat": 1.0, "rays": 0.25, "ray_col": Color("#d0a0ff"), "leaf": 0.0},
	"cave": {"sun": Color("#a8c8d8"), "sun_e": 0.22, "elev": -70.0, "yaw": -140.0,
		"amb": Color("#9ab8d0"), "amb_e": 0.40, "sky": Color("#1d2a33"),
		"cool": Color("#0a1a2a"), "warm": Color("#a0e0ff"), "sat": 0.95, "rays": 0.4, "ray_col": Color("#a0e8ff"), "leaf": 0.0},
}

var env: Environment
var sun: DirectionalLight3D
var we: WorldEnvironment
var preset := "forest"
var quality := 2
var P: Dictionary


func setup(q: int = 2) -> void:
	quality = q
	we = WorldEnvironment.new()
	env = Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR   # 도트 색을 그대로 (필믹은 색을 바꾼다)
	env.tonemap_exposure = 1.0
	env.glow_enabled = true
	env.glow_intensity = 0.55
	env.glow_bloom = 0.06
	env.glow_hdr_threshold = 0.95
	env.glow_blend_mode = Environment.GLOW_BLEND_MODE_SCREEN
	env.fog_enabled = false
	env.adjustment_enabled = true
	we.environment = env
	add_child(we)
	sun = DirectionalLight3D.new()
	sun.shadow_enabled = true
	sun.shadow_bias = 0.02
	sun.shadow_normal_bias = 0.6
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	sun.directional_shadow_max_distance = 60.0
	sun.light_angular_distance = 0.0
	add_child(sun)
	apply(preset, true)


func apply(name_: String, _instant := false) -> void:
	preset = name_
	P = PRESETS.get(name_, PRESETS["forest"])
	env.background_color = P["sky"]
	env.ambient_light_color = P["amb"]
	env.ambient_light_energy = P["amb_e"]
	sun.light_color = P["sun"]
	sun.light_energy = P["sun_e"]
	sun.rotation = Vector3(deg_to_rad(P["elev"]), deg_to_rad(P["yaw"]), 0)
	sun.shadow_enabled = quality >= 1
	# 간접광 (GI): 화질 2 이상 SSIL, 1 이상 SSAO
	env.ssao_enabled = quality >= 1
	env.ssao_radius = 1.4
	env.ssao_intensity = 1.8
	env.ssao_power = 1.6
	env.ssao_light_affect = 0.15
	env.ssil_enabled = quality >= 2
	env.ssil_radius = 4.0
	env.ssil_intensity = 1.2
	env.ssil_sharpness = 0.9
	env.ssil_normal_rejection = 1.0
	env.volumetric_fog_enabled = false
	env.adjustment_saturation = P["sat"]
	env.adjustment_contrast = 1.04
	env.adjustment_color_correction = _lut(P["cool"], P["warm"])


## 스플릿 토닝 3D LUT (17³): 어두운 곳은 cool 쪽으로, 밝은 곳은 warm 쪽으로 살짝 — 색 단계는 유지 (도트 깨짐 없음)
func _lut(cool: Color, warm: Color) -> ImageTexture3D:
	const N := 17
	var imgs: Array[Image] = []
	for b in N:
		var img := Image.create(N, N, false, Image.FORMAT_RGB8)
		for g in N:
			for r in N:
				var c := Color(r / float(N - 1), g / float(N - 1), b / float(N - 1))
				var l := c.r * 0.3 + c.g * 0.59 + c.b * 0.11
				var sh := clampf(1.0 - l * 2.2, 0.0, 1.0) * 0.18
				var hi := clampf((l - 0.55) * 2.0, 0.0, 1.0) * 0.10
				var o := c.lerp(Color(cool.r * (0.6 + l), cool.g * (0.6 + l), cool.b * (0.6 + l)), sh)
				o = o.lerp(Color(minf(1.0, warm.r * (0.5 + l * 0.6)), minf(1.0, warm.g * (0.5 + l * 0.6)), minf(1.0, warm.b * (0.5 + l * 0.6))), hi)
				img.set_pixel(r, g, o)
		imgs.append(img)
	var t := ImageTexture3D.new()
	t.create(Image.FORMAT_RGB8, N, N, N, false, imgs)
	return t
