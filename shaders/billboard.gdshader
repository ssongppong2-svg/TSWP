shader_type spatial;
// 서 있는 도트 그림 (나무·풀·캐릭터·소품)
//  · 세로는 그대로 세운 채 카메라 쪽으로만 돈다 (Y 빌보드)
//  · 카메라 기울기만큼 세로로 늘려(bb_stretch) 화면에선 원래 도트 크기 그대로 보이게
//  · 알파 가위(0.5)로 또렷한 테두리 · 그림자도 실루엣대로 드리운다
//  · 바람 흔들림(sway) · 지나가는 사람에 밀림(push_*) · 맞으면 하얗게(flash)
render_mode cull_disabled, depth_draw_opaque;

#include "res://shaders/inc/toon.gdshaderinc"

global uniform float bb_stretch;
global uniform float wind_time;
global uniform vec4 push_a;
global uniform vec4 push_b;
global uniform vec4 push_c;
global uniform vec4 push_d;

uniform sampler2D tex : source_color, filter_nearest, repeat_disable;
uniform vec4 region = vec4(0.0, 0.0, 1.0, 1.0);   // 아틀라스 안 사각형 (uv)
uniform float sway = 0.0;                          // 바람에 흔들리는 정도 (꼭대기 기준 m)
uniform float push_k = 0.0;                        // 사람에 밀리는 정도
uniform float flash = 0.0;
uniform vec4 tint : source_color = vec4(1.0);
uniform float flip = 0.0;                          // 1 = 좌우 뒤집기
uniform float up_normal = 0.65;                    // 빛 받는 방향: 1 = 땅처럼 위, 0 = 카메라 쪽
uniform float emission = 0.0;
uniform bool use_instance = false;                 // 멀티메시: INSTANCE_CUSTOM = (region.x, region.y, region.w, region.h)

varying vec4 v_region;
varying float v_flip;

void vertex() {
	vec4 reg = region;
	v_flip = flip;
	if (use_instance) {
		reg = INSTANCE_CUSTOM;
		v_flip = (COLOR.a < 0.5) ? 1.0 : 0.0;
	}
	v_region = reg;
	vec3 origin = MODEL_MATRIX[3].xyz;
	float sx = length(MODEL_MATRIX[0].xyz);
	float sy = length(MODEL_MATRIX[1].xyz);
	vec3 right = normalize(vec3(INV_VIEW_MATRIX[0].x, 0.0, INV_VIEW_MATRIX[0].z));
	vec3 fwd = normalize(cross(right, vec3(0.0, 1.0, 0.0)));
	float h01 = clamp(UV.y, 0.0, 1.0);
	float top = 1.0 - h01;                   // 0 바닥 … 1 꼭대기
	vec3 local = VERTEX;
	local.x *= sx;
	local.y *= sy * bb_stretch;
	// 바람: 꼭대기일수록, 자리마다 다른 박자로
	float ph = origin.x * 0.37 + origin.z * 0.61;
	float w = sin(wind_time * 1.7 + ph) * 0.6 + sin(wind_time * 3.1 + ph * 1.7) * 0.25;
	vec3 offs = vec3(0.0);
	offs.x += w * sway * top * top;
	// 밀림: 가까운 사람 쪽에서 밀어낸다 (push_* = xyz 위치, w 세기)
	if (push_k > 0.0) {
		vec4 ps[4] = vec4[](push_a, push_b, push_c, push_d);
		for (int i = 0; i < 4; i++) {
			vec3 d = origin - ps[i].xyz;
			d.y = 0.0;
			float dist = length(d);
			float k = (1.0 - smoothstep(0.2, 0.9, dist)) * ps[i].w;
			if (dist > 0.001) {
				offs += normalize(d) * k * push_k * top;
			}
		}
	}
	vec3 world = origin + right * local.x + vec3(0.0, local.y, 0.0) + right * dot(offs, right) + fwd * dot(offs, fwd) * 0.3;
	VERTEX = (inverse(MODEL_MATRIX) * vec4(world, 1.0)).xyz;
	// 빛 받는 면: 위쪽 + 카메라 쪽 섞기
	vec3 n = normalize(mix(-fwd, vec3(0.0, 1.0, 0.0), up_normal));
	NORMAL = (inverse(MODEL_MATRIX) * vec4(n, 0.0)).xyz;
	if (use_instance) {
		COLOR.a = 1.0;
	}
}

void fragment() {
	vec2 uv = UV;
	if (v_flip > 0.5) {
		uv.x = 1.0 - uv.x;
	}
	vec4 c = texture(tex, v_region.xy + uv * v_region.zw);
	if (c.a < 0.5) {
		discard;
	}
	vec3 col = c.rgb * tint.rgb * COLOR.rgb;
	ALBEDO = mix(col, vec3(1.0), flash);
	EMISSION = col * emission + vec3(flash) * 0.6;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
