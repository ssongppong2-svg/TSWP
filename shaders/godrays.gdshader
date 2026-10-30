shader_type canvas_item;
// 갓 레이 (화면 효과, 저해상도 화면 안에서 그려 도트 그대로):
//  · 왼쪽 위에서 비스듬히 내리꽂는 빛줄기 — 세계에 붙어 있어 카메라가 움직여도 미끄러지지 않는다 (cam_px)
//  · 줄기 굵기·세기는 천천히 일렁이고, 3단계로 끊어 디더 (도트 느낌)
//  · 화면이 밝은 곳(햇빛 받는 땅)에서 진하고, 그늘에선 옅다
//  · 떠다니는 먼지 점이 빛줄기 안에서만 반짝인다
render_mode blend_add, unshaded;

uniform sampler2D screen : hint_screen_texture, filter_nearest;
uniform vec4 ray_col : source_color = vec4(1.0, 0.9, 0.7, 1.0);
uniform float strength = 0.5;
uniform vec2 cam_px = vec2(0.0);
uniform float time = 0.0;
uniform vec2 dir = vec2(0.55, 0.83);           // 빛이 내려가는 화면 방향

float hash(float n) { return fract(sin(n) * 43758.5453); }
float n1(float x) {
	float i = floor(x);
	float f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(hash(i), hash(i + 1.0), f);
}
float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

void fragment() {
	vec2 px = floor(FRAGCOORD.xy);
	vec2 w = px + vec2(cam_px.x, -cam_px.y);             // 세계에 붙은 픽셀 좌표
	vec2 perp = vec2(dir.y, -dir.x);
	float u = dot(w, perp);                               // 빛줄기를 가로지르는 좌표
	float v = dot(w, dir);
	float bands = n1(u / 30.0 + time * 0.05) * 0.75 + n1(u / 11.0 - time * 0.08) * 0.25;
	bands = smoothstep(0.66, 0.82, bands);
	// 줄기마다 길이 방향으로 천천히 끊기며 숨쉬기
	bands *= 0.6 + 0.4 * n1(v / 90.0 + u / 300.0 + time * 0.1);
	vec3 sc = texture(screen, SCREEN_UV).rgb;
	float lum = dot(sc, vec3(0.3, 0.59, 0.11));
	float k = bands * strength * mix(0.35, 1.0, smoothstep(0.25, 0.6, lum));
	// 3단계 + 디더
	ivec2 bi = ivec2(mod(px, 4.0));
	float b4[16] = float[](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	float th = (b4[bi.y * 4 + bi.x] + 0.5) / 16.0;
	float q = floor(k * 3.0 + th) / 3.0;
	vec3 add = ray_col.rgb * q * 0.3;
	// 먼지 점
	vec2 dc = floor((w + vec2(time * 3.0, time * 7.0)) / 3.0);
	if (hash2(dc) > 0.9975 && bands > 0.3) {
		add += ray_col.rgb * 0.5 * strength;
	}
	COLOR = vec4(add, 1.0);
}
