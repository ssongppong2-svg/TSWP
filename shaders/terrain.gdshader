shader_type spatial;
// 높이 칸 지형: 윗면은 땅 재질(잔디·짙은 잔디·흙길·돌바닥…)을 세계 좌표로, 벽은 절벽 바위로.
//  · 재질 경계는 픽셀마다 흔들어(노이즈) 자연스러운 들쭉날쭉 경계 — 칸 모양이 보이지 않게
//  · 큰 노이즈로 색을 살짝 출렁여 반복 무늬가 안 보이게
//  · 절벽 꼭대기엔 잔디가 늘어지고, 밑동은 그늘
render_mode cull_back;

#include "res://shaders/inc/toon.gdshaderinc"

uniform sampler2D ground : source_color, filter_nearest, repeat_enable;   // 세로로 재질 N장 (각 128x128)
uniform sampler2D wall_tex : source_color, filter_nearest, repeat_enable; // 절벽 64x64
uniform sampler2D map : filter_nearest;                                   // 칸별 재질 (R = id/255)
uniform vec2 map_size = vec2(64.0, 64.0);
uniform float n_mats = 4.0;
uniform float tex_px = 128.0;
uniform vec3 fringe_dark : source_color = vec3(0.24, 0.40, 0.33);
uniform vec3 fringe_mid : source_color = vec3(0.36, 0.58, 0.45);
uniform vec3 fringe_hi : source_color = vec3(0.49, 0.76, 0.54);
uniform vec3 wall_shadow : source_color = vec3(0.16, 0.20, 0.21);

varying vec3 wpos;
varying vec3 wnrm;
varying vec2 wall_span;   // (아래, 위) 높이

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	wall_span = UV2;
}

float mat_at(vec2 xz) {
	vec2 uv = (floor(xz) + 0.5) / map_size;
	return floor(texture(map, uv).r * 255.0 + 0.5);
}

void fragment() {
	vec3 p = floor(wpos * 16.0 + 0.001) / 16.0;      // 텍셀 단위로 끊기
	vec3 col;
	if (wnrm.y > 0.5) {
		// ---- 윗면: 재질 경계를 흔든다
		vec2 xz = p.xz;
		vec2 j = vec2(vnoise(xz * 1.7), vnoise(xz * 1.7 + 19.3)) - 0.5;
		j += (vec2(hash(xz * 16.0), hash(xz * 16.0 + 7.1)) - 0.5) * 0.18;
		float m = mat_at(xz + j * 0.8);
		vec2 tuv = fract(xz * 16.0 / tex_px);
		col = texture(ground, vec2(tuv.x, (tuv.y + m) / n_mats)).rgb;
		// 큰 출렁임 (반복 감추기)
		float big = vnoise(xz * 0.12) * 0.6 + vnoise(xz * 0.31 + 4.0) * 0.4;
		col *= mix(0.92, 1.07, big);
	} else {
		// ---- 벽: 절벽 바위
		float along = (abs(wnrm.x) > 0.5) ? p.z : p.x;
		vec2 wuv = vec2(along * 16.0 / 64.0, -p.y * 16.0 / 64.0);
		col = texture(wall_tex, wuv).rgb;
		float from_top = (wall_span.y - p.y) * 16.0;      // 위에서 몇 픽셀
		float from_bot = (p.y - wall_span.x) * 16.0;
		// 늘어진 잔디
		float fl = 2.0 + floor(vnoise(vec2(along * 3.1, wall_span.y)) * 4.0) + floor(hash(vec2(floor(along * 16.0), wall_span.y)) * 2.0);
		if (from_top < fl) {
			col = from_top < 1.0 ? fringe_hi : (from_top < fl - 1.0 ? fringe_mid : fringe_dark);
		} else if (from_top < fl + 1.0) {
			col *= 0.62;                                   // 잔디 밑 그늘 한 줄
		}
		// 밑동 그늘
		col = mix(col, wall_shadow, (1.0 - smoothstep(0.0, 5.0, from_bot)) * 0.55);
	}
	ALBEDO = col;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
