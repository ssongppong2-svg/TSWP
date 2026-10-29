shader_type spatial;
// 서 있는 도트 그림 (나무·풀·캐릭터·소품)
//  · 세로는 그대로 세운 채 카메라 쪽으로만 돈다 (Y 빌보드)
//  · 카메라 기울기만큼 세로로 늘려(bb_stretch) 화면에선 원래 도트 크기 그대로 보이게
//  · 알파 가위(0.5)로 또렷한 테두리 · 그림자도 실루엣대로 드리운다
//  · 바람 흔들림(sway) · 지나가는 사람에 밀림(push_*) · 맞으면 하얗게(flash)
render_mode cull_disabled, depth_draw_opaque;

#include "res://shaders/inc/toon.gdshaderinc"

global uniform float bb_stretch;
global uniform vec3 cam_fwd;
global uniform sampler2D shade_map : filter_nearest;   // 땅에 구운 그늘 (Terrain) — 그늘 속 풀·사람은 어둡게
global uniform vec4 shade_area;                        // (x0, z0, 폭, 깊이) m
global uniform vec4 cut_plane;      // 측면 시점: 이 평면(n·p > w)보다 카메라 쪽은 걷어 낸다 (단면)
global uniform float side_amt;
global uniform vec4 lane_plane;     // 측면 구간 선 (n, 깊이) — 멀리 있는 것일수록 공기 색      // 0 탑다운 … 1 측면
global uniform vec4 atmo_col;       // 측면에서 멀리 있는 것을 섞을 공기 색          // 화면 카메라가 보는 방향 — 이것과 다르면 그림자(해) 패스
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
uniform float up_normal = 0.85;                    // 빛 받는 방향: 1 = 땅처럼 위, 0 = 카메라 쪽
uniform float emission = 0.0;
uniform float shadow_h = 0.5;                      // 그림자 키 (1 = 실제 키)
uniform bool receive_shade = true;                 // 나무·덤불처럼 그늘을 만드는 쪽은 끈다
uniform bool use_instance = false;                 // 멀티메시: INSTANCE_CUSTOM = (region.x, region.y, region.w, region.h)

varying vec4 v_region;
varying float v_flip;
varying float v_shade;
varying vec3 v_nrm;
varying float v_cut;                               // 단면까지 거리 (+ 면 걷어 낼 쪽)
varying float v_lane;                              // 구간 선에서 뒤로 얼마나 먼가 (m)                                // 세계 기준 빛 받는 면 (뒷면 뒤집힘 없이 조각에서 다시 넣는다)

void vertex() {
	vec4 reg = region;
	v_flip = flip;
	if (use_instance) {
		reg = INSTANCE_CUSTOM;
		v_flip = (COLOR.a < 0.5) ? 1.0 : 0.0;
	}
	v_region = reg;
	v_shade = 0.0;
	v_cut = dot(cut_plane.xyz, MODEL_MATRIX[3].xyz) - cut_plane.w;
	v_lane = lane_plane.w - dot(lane_plane.xyz, MODEL_MATRIX[3].xyz);
	if (!use_instance && MODEL_MATRIX[0].x < 0.0) {
		v_flip = 1.0 - v_flip;               // 노드를 좌우로 뒤집어(scale.x < 0) 두면 그림도 뒤집기
	}
	vec3 origin = MODEL_MATRIX[3].xyz;
	float sx = length(MODEL_MATRIX[0].xyz);
	float sy = length(MODEL_MATRIX[1].xyz);
	vec3 right = normalize(vec3(INV_VIEW_MATRIX[0].x, 0.0, INV_VIEW_MATRIX[0].z));
	vec3 fwd = normalize(cross(right, vec3(0.0, 1.0, 0.0)));
	float h01 = clamp(UV.y, 0.0, 1.0);
	float top = 1.0 - h01;                   // 0 바닥 … 1 꼭대기
	// 그림자 패스: 해를 향해 서되 키를 줄여서 그림자가 밑동 가까이 뭉치게 (레퍼런스처럼 나무 밑 그늘)
	vec3 view_fwd = -INV_VIEW_MATRIX[2].xyz;
	bool shadow_pass = dot(view_fwd, cam_fwd) < 0.995;
	vec3 local = VERTEX;
	local.x *= sx;
	local.y *= sy * (shadow_pass ? shadow_h : bb_stretch);
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
	if (receive_shade) {
		vec2 suv = (origin.xz - shade_area.xy) / shade_area.zw;
		if (suv.x > 0.0 && suv.y > 0.0 && suv.x < 1.0 && suv.y < 1.0) {
			v_shade = textureLod(shade_map, suv, 0.0).r;
		}
	}
	// 빛 받는 면: 위쪽 + 카메라 쪽(fwd = 화면 앞) 섞기. 그림에 이미 명암이 있으니 대부분 위를 향하게
	v_nrm = normalize(mix(fwd, vec3(0.0, 1.0, 0.0), up_normal));
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
	// 측면 단면: 앞쪽 물건은 디더로 사라진다
	ivec2 bp = ivec2(mod(FRAGCOORD.xy, 4.0));
	float b4[16] = float[](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	float th = (b4[bp.y * 4 + bp.x] + 0.5) / 16.0;
	if (v_cut > th * mix(1.2, 0.05, side_amt)) {
		discard;
	}
	// 멀티메시만 인스턴스 색을 쓴다 (일반 메시는 정점 색이 없어서 COLOR 가 엉뚱한 값일 수 있다)
	vec3 col = c.rgb * tint.rgb * (use_instance ? COLOR.rgb : vec3(1.0));
	// 땅 그늘 두 단계 (지형과 같은 색)
	if (v_shade > 0.62) {
		col *= vec3(0.36, 0.50, 0.56);
	} else if (v_shade > 0.22) {
		col *= vec3(0.50, 0.66, 0.70);
	}
	// 측면: 단면 뒤로 멀수록 공기 색으로 (깊이감)
	float far_k = clamp((v_lane - 2.0) / 26.0, 0.0, 0.6) * side_amt;
	col = mix(col, atmo_col.rgb, far_k);
	ALBEDO = mix(col, vec3(1.0), flash);
	EMISSION = col * emission + vec3(flash) * 0.6;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
	NORMAL = normalize((VIEW_MATRIX * vec4(v_nrm, 0.0)).xyz);
}
