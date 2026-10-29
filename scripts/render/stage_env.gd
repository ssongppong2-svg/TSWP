class_name StageEnv
extends Node3D
## 하늘·해·안개·주변광. 지역(숲·동굴·둥지·캠프) 분위기 프리셋.
##  · 해는 왼쪽 위 뒤에서 (레퍼런스처럼 그림자가 오른쪽 아래로)
##  · 화질 단계에 따라 그림자·부피 안개(빛줄기)·SSAO 를 켜고 끈다

const PRESETS := {
	"forest": {"sky": Color("#9cc9b4"), "amb": Color("#7fa898"), "amb_e": 0.55, "sun": Color("#fff1d2"), "sun_e": 1.35,
		"fog": Color("#a9d1bd"), "fog_d": 0.010, "vol": 0.018, "vol_col": Color("#d9f0d4")},
	"camp": {"sky": Color("#f0c49a"), "amb": Color("#9a8fa8"), "amb_e": 0.5, "sun": Color("#ffd6a0"), "sun_e": 1.3,
		"fog": Color("#e8b88f"), "fog_d": 0.010, "vol": 0.02, "vol_col": Color("#ffd8b0")},
	"cave": {"sky": Color("#1d2a33"), "amb": Color("#4a6070"), "amb_e": 0.45, "sun": Color("#a8c8d8"), "sun_e": 0.35,
		"fog": Color("#203038"), "fog_d": 0.03, "vol": 0.03, "vol_col": Color("#6aa0b0")},
	"nest": {"sky": Color("#2a1e30"), "amb": Color("#5a4868"), "amb_e": 0.5, "sun": Color("#d8b0e8"), "sun_e": 0.4,
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
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.tonemap_exposure = 1.0
	env.glow_enabled = true
	env.glow_intensity = 0.35
	env.glow_bloom = 0.04
	env.glow_hdr_threshold = 1.1
	env.fog_enabled = true
	env.fog_mode = Environment.FOG_MODE_EXPONENTIAL
	env.fog_sky_affect = 0.0
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.04
	env.adjustment_contrast = 1.03
	we.environment = env
	add_child(we)
	sun = DirectionalLight3D.new()
	sun.rotation = Vector3(deg_to_rad(-58), deg_to_rad(38), 0)
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
	env.fog_light_color = p["fog"]
	env.fog_density = p["fog_d"]
	sun.light_color = p["sun"]
	sun.light_energy = p["sun_e"]
	var vol: bool = quality >= 2
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
