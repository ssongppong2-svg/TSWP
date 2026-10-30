shader_type spatial;
// 높이 칸 지형: 윗면은 땅 재질(잔디·짙은 잔디·흙길·돌바닥…)을 세계 좌표로, 벽은 절벽 바위로.
//  · 화면 1픽셀 = 텍스처 1텍셀이 되도록 기울기만큼 세로 밀도를 맞춘다
//    (윗면 z: 16·sin(기울기) 텍셀/m, 벽 y: 16·cos(기울기) 텍셀/m) → 도트가 찌그러지거나 줄이 빠지지 않는다
//  · 재질 경계는 픽셀마다 흔들어(노이즈) 자연스러운 들쭉날쭉 경계 — 칸 모양이 보이지 않게
//  · 큰 노이즈로 색을 살짝 출렁여 반복 무늬가 안 보이게
//  · 절벽 꼭대기엔 잔디가 늘어지고, 밑동은 그늘
render_mode cull_back;

#include "res://shaders/inc/toon.gdshaderinc"

global uniform float cam_pitch;
global uniform float wind_time;
global uniform float leaf_k;       // 흘러가는 나뭇잎 그림자 세기
global uniform vec4 cut_plane;
global uniform float side_amt;
global uniform vec4 lane_plane;     // 측면 구간 선 (n, 깊이) — 멀리 있는 것일수록 공기 색
global uniform vec4 atmo_col;

uniform sampler2D ground : source_color, filter_nearest, repeat_enable;   // 세로로 재질 N장 (각 128x128)
uniform sampler2D wall_tex : source_color, filter_nearest, repeat_enable; // 절벽 64x64
uniform sampler2D ground_n : hint_normal, filter_nearest, repeat_enable;   // 땅 노멀맵 (같은 배치)
uniform sampler2D wall_n : hint_normal, filter_nearest, repeat_enable;
uniform float normal_k = 0.35;
uniform sampler2D decal_map : source_color, filter_nearest;               // 구워 넣은 땅 무늬 (낙엽·자갈·이끼…)
uniform vec2 decal_size = vec2(1.0);
uniform sampler2D map : filter_nearest;                                   // 칸별 R = 재질 id/255, G = 높이(층)/255
uniform vec2 grid_size = vec2(64.0, 64.0);                                // 칸 수
uniform float cell = 0.5;                                                 // 칸 한 변 (m)
uniform vec3 rim_hi : source_color = vec3(0.776, 0.878, 0.557);              // 절벽 윗가장자리 밝은 테두리
uniform vec3 rim_dark : source_color = vec3(0.16, 0.13, 0.12);            // 뒤·옆 가장자리 짙은 테두리
uniform float n_mats = 4.0;
uniform float tex_px = 128.0;
uniform vec3 fringe_dark : source_color = vec3(0.298, 0.455, 0.251);
uniform vec3 fringe_mid : source_color = vec3(0.443, 0.620, 0.314);
uniform vec3 fringe_hi : source_color = vec3(0.604, 0.761, 0.400);
uniform vec3 wall_shadow : source_color = vec3(0.10, 0.08, 0.10);
uniform sampler2D shade_map : filter_nearest;                             // 구워 둔 그늘 (1m = 16칸)
uniform vec3 shade_col : source_color = vec3(0.50, 0.66, 0.70);          // 그늘 곱하기 색 (청록)
uniform vec3 shade_deep : source_color = vec3(0.36, 0.50, 0.56);

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
	vec2 uv = (floor(xz / cell) + 0.5) / grid_size;
	return floor(texture(map, uv).r * 255.0 + 0.5);
}

float lvl_of(vec2 c) {
	if (c.x < 0.0 || c.y < 0.0 || c.x >= grid_size.x || c.y >= grid_size.y) {
		return -2.0;
	}
	return floor(texture(map, (c + 0.5) / grid_size).g * 255.0 + 0.5);
}

void fragment() {
	// 측면 단면: 카메라 쪽은 걷어 낸다 (단면 뚜껑은 Terrain.make_cut 이 따로 세운다)
	float cut_d = dot(cut_plane.xyz, wpos) - cut_plane.w;
	if (cut_d > 0.0) {
		discard;
	}
	float sp = max(sin(cam_pitch), 0.12);
	vec3 nw = wnrm;
	float cp = max(cos(cam_pitch), 0.2);
	vec3 col;
	if (wnrm.y > 0.5) {
		// ---- 윗면 (텍셀 좌표: x 16/m, z 16·sin/m)
		vec2 tx = floor(vec2(wpos.x * 16.0, wpos.z * 16.0 * sp) + 0.001);
		vec2 xz = vec2(tx.x / 16.0, tx.y / (16.0 * sp));          // 끊은 세계 좌표 (경계 흔들기용)
		vec2 j = vec2(vnoise(xz * 1.7), vnoise(xz * 1.7 + 19.3)) - 0.5;
		j += (vec2(hash(tx), hash(tx + 7.1)) - 0.5) * 0.18;
		float m = mat_at(xz + j * 0.8);
		// 반복 깨기: 6m 덩어리마다 텍스처를 다른 자리에서 가져오고 좌우를 뒤집는다 (경계는 노이즈로 흔들어 직선 이음새가 안 보이게)
		vec2 bc = floor((xz + (vec2(vnoise(xz * 0.5), vnoise(xz * 0.5 + 11.0)) - 0.5) * 2.4) / 6.0);
		vec2 boff = floor(vec2(hash(bc), hash(bc + 3.7)) * tex_px);
		vec2 btx = tx + boff;
		if (hash(bc + 9.1) > 0.5) {
			btx.x = -btx.x - 1.0;
		}
		vec2 tuv = fract((btx + 0.5) / tex_px);
		col = texture(ground, vec2(tuv.x, (tuv.y + m) / n_mats)).rgb;
		vec3 gn = texture(ground_n, vec2(tuv.x, (tuv.y + m) / n_mats)).rgb * 2.0 - 1.0;
		nw = normalize(mix(vec3(0.0, 1.0, 0.0), normalize(vec3(gn.x, max(gn.z, 0.2), -gn.y)), normal_k));
		// 구운 땅 무늬 (데칼)
		vec4 dc = texture(decal_map, (tx + 0.5) / decal_size);
		if (dc.a > 0.5 && decal_size.x > 1.0) {
			col = dc.rgb;
		}
		// 큰 출렁임 (반복 감추기)
		float big = vnoise(xz * 0.12) * 0.6 + vnoise(xz * 0.31 + 4.0) * 0.4;
		col *= mix(0.92, 1.07, big);
		// 높이별 명도: 높은 곳은 밝고 따뜻하게, 낮은 곳은 살짝 어둡게 — 층이 한눈에 읽히게
		float lv = floor(wpos.y + 0.01);
		col *= mix(0.9, 1.12, clamp(lv / 6.0, 0.0, 1.0));
		col += vec3(0.02, 0.015, 0.0) * clamp(lv / 6.0, 0.0, 1.0);
		// 구운 그늘: 두 단계 + 경계는 4x4 디더 (부드럽지만 도트답게)
		float sh = texture(shade_map, wpos.xz / (grid_size * cell)).r;
		ivec2 bi = ivec2(mod(tx, 4.0));
		float b4[16] = float[](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
		float th = (b4[bi.y * 4 + bi.x] + 0.5) / 16.0;
		if (sh > 0.62 + (th - 0.5) * 0.25) {
			col *= shade_deep;
		} else if (sh > 0.22 + (th - 0.5) * 0.3) {
			col *= shade_col;
		}
		// 흘러가는 나뭇잎·구름 그림자 (세계에 붙어 천천히 흐름, 2단계 디더)
		if (leaf_k > 0.0) {
			vec2 lp = xz * 0.22 + vec2(wind_time * 0.035, wind_time * 0.02);
			float lv2 = vnoise(lp) * 0.6 + vnoise(lp * 2.7 + 3.0) * 0.4;
			if (lv2 > 0.62 + (th - 0.5) * 0.12) {
				col *= mix(vec3(1.0), shade_col, leaf_k * 0.8);
			}
		}
		// 절벽 가장자리 테두리: 앞(남)쪽이 낮으면 밝은 줄, 뒤·옆이 낮으면 짙은 줄 (높이가 또렷하게 읽히게)
		vec2 c = floor(wpos.xz / cell);
		float me = lvl_of(c);
		vec2 f = wpos.xz - c * cell;                             // 칸 안 위치 (m)
		float px_s = (cell - f.y) * 16.0 * sp;                   // 남쪽 가장자리까지 화면 픽셀
		float px_n = f.y * 16.0 * sp;
		float px_w = f.x * 16.0;
		float px_e = (cell - f.x) * 16.0;
		if (lvl_of(c + vec2(0.0, 1.0)) < me && px_s < 1.0) {
			col = rim_hi;
		} else if (lvl_of(c + vec2(0.0, -1.0)) < me && px_n < 1.0) {
			col = rim_dark;
		} else if ((lvl_of(c + vec2(-1.0, 0.0)) < me && px_w < 1.0) || (lvl_of(c + vec2(1.0, 0.0)) < me && px_e < 1.0)) {
			col = rim_dark;
		}
	} else {
		// ---- 벽: 절벽 바위 (텍셀 좌표: 가로 16/m, 세로 16·cos/m)
		float along = (abs(wnrm.x) > 0.5) ? wpos.z : wpos.x;
		float ty = floor((wall_span.y - wpos.y) * 16.0 * cp + 0.001);   // 위에서 몇 픽셀
		float tx = floor(along * 16.0 + 0.001);
		vec2 wuv = vec2(tx, ty + floor(wall_span.y * 16.0 * cp)) / 64.0;
		col = texture(wall_tex, wuv).rgb;
		vec3 wn = texture(wall_n, wuv).rgb * 2.0 - 1.0;
		vec3 T = normalize(cross(vec3(0.0, 1.0, 0.0), wnrm));
		nw = normalize(mix(wnrm, normalize(T * wn.x + vec3(0.0, 1.0, 0.0) * wn.y + wnrm * max(wn.z, 0.2)), normal_k));
		float from_top = ty;
		float from_bot = (wpos.y - wall_span.x) * 16.0 * cp;
		// 늘어진 잔디
		float fl = 2.0 + floor(vnoise(vec2(tx * 0.2, wall_span.y)) * 4.0) + floor(hash(vec2(tx, wall_span.y)) * 2.0);
		if (from_top < fl) {
			col = from_top < 1.0 ? fringe_hi : (from_top < fl - 1.0 ? fringe_mid : fringe_dark);
		} else if (from_top < fl + 1.0) {
			col *= 0.62;                                   // 잔디 밑 그늘 한 줄
		}
		// 밑동 그늘 (단면 뚜껑·지도 끝 벽은 아래로 갈수록 땅속처럼 어둡게)
		if (wall_span.x > -2.5) {
			col = mix(col, wall_shadow, (1.0 - smoothstep(0.0, 5.0, from_bot)) * 0.55);
		} else {
			col = mix(col, wall_shadow, clamp(-wpos.y / 7.0, 0.0, 0.75));
		}
	}
	float lane_d = lane_plane.w - dot(lane_plane.xyz, wpos);
	col = mix(col, atmo_col.rgb, clamp((lane_d - 2.0) / 26.0, 0.0, 0.6) * side_amt);
	ALBEDO = col;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
	NORMAL = normalize((VIEW_MATRIX * vec4(nw, 0.0)).xyz);
}
