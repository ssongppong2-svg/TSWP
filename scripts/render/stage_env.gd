class_name StageEnv
extends Node3D
## 하늘·해·안개·주변광. 지역(숲·동굴·둥지·캠프) 분위기 프리셋.
##  · 해는 왼쪽 위 뒤에서 (레퍼런스처럼 그림자가 오른쪽 아래로)
##  · 화질 단계에 따라 그림자·부피 안개(빛줄기)·SSAO 를 켜고 끈다

const PRESETS := {
	"forest": {"sky": Color("#9cc9b4"), "amb": Color("#dfeef0"), "amb_e": 0.44, "sun": Color("#fff4dc"), "sun_e": 0.62,
		"fog": Color("#a9d1bd"), "fog_d": 0.010, "vol": 0.018, "vol_col": Color("#d9f0d4")},
	"camp": {"sky": Color("#c89a8a"), "amb": Color("#b0a8d8"), "amb_e": 0.40, "sun": Color("#ffc890"), "sun_e": 0.42,
		"fog": Color("#e8b88f"), "fog_d": 0.010, "vol": 0.02, "vol_col": Color("#ffd8b0")},
	"cave": {"sky": Color("#1d2a33"), "amb": Color("#9ab8d0"), "amb_e": 0.42, "sun": Color("#a8c8d8"), "sun_e": 0.25,
		"fog": Color("#203038"), "fog_d": 0.03, "vol": 0.03, "vol_col": Color("#6aa0b0")},
	"nest": {"sky": Color("#2a1e30"), "amb": Color("#b8a0d0"), "amb_e": 0.4, "sun": Color("#d8b0e8"), "sun_e": 0.28,
		"fog": Color("#2c2034"), "fog_d": 0.03, "vol": 0.03, "vol_col": Color("#b080c0"),},
}

var env: Environment
var sun: DirectionalLight3D
var we: WorldEnvironment
var preset := "forest"
var quality := 2


func setup(q: int = 2) -> void:
	quality = q
	we = WorldEnvironment.new()
	env = Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR   # 도트 색을 그대로 (필믹은 색을 바꾼다)
	env.tonemap_exposure = 1.0
	env.glow_enabled = true
	env.glow_intensity = 0.35
	env.glow_bloom = 0.04
	env.glow_hdr_threshold = 1.1
	# 직교 카메라가 60m 뒤에 있어서 거리 안개는 화면 전체를 똑같이 뿌옇게 만든다 → 끔 (분위기는 색 보정·빛줄기 그림으로)
	env.fog_enabled = false
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.0
	env.adjustment_contrast = 1.0
	we.environment = env
	add_child(we)
	sun = DirectionalLight3D.new()
	# 해는 북서쪽(화면 왼쪽 위 뒤)에서 → 그림자·그늘이 오른쪽 아래로 (그림에 칠한 빛과 같은 방향)
	sun.rotation = Vector3(deg_to_rad(-62), deg_to_rad(-144), 0)
	sun.shadow_enabled = true
	sun.shadow_bias = 0.02
	sun.shadow_normal_bias = 0.6
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	sun.directional_shadow_max_distance = 60.0
	sun.light_angular_distance = 0.0
	add_child(sun)
	apply(preset, true)


func apply(name_: String, instant := false) -> void:
	preset = name_
	var p: Dictionary = PRESETS.get(name_, PRESETS["forest"])
	env.background_color = p["sky"]
	env.ambient_light_color = p["amb"]
	env.ambient_light_energy = p["amb_e"]
	sun.light_color = p["sun"]
	sun.light_energy = p["sun_e"]
	var vol := false     # 부피 안개도 같은 이유로 끔 (빛줄기는 따로 그림으로)
	env.volumetric_fog_enabled = vol
	if vol:
		env.volumetric_fog_density = p["vol"]
		env.volumetric_fog_albedo = p["vol_col"]
		env.volumetric_fog_length = 48.0
		env.volumetric_fog_detail_spread = 2.0
		env.volumetric_fog_anisotropy = 0.6
		env.volumetric_fog_sky_affect = 0.0
	env.ssao_enabled = quality >= 1
	env.ssao_radius = 1.2
	env.ssao_intensity = 1.6
	sun.shadow_enabled = quality >= 1
