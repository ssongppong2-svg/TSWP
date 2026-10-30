shader_type spatial;
// 원경 (멀리 있는 산·숲 실루엣 · 하늘): 지도 북쪽 뒤에 세운 큰 판. 픽셀 단위로 그린다.
//  kind 0 = 하늘 (위→아래 그라데이션 + 흘러가는 도트 구름)
//  kind 1 = 능선 (노이즈 산등성이, 위쪽 가장자리에 나무 꼭대기 같은 둥근 돌기)
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_opaque;

global uniform float wind_time;

uniform int kind = 1;
uniform vec2 size_px = vec2(4000.0, 640.0);
uniform float scroll = 0.0;                 // 패럴랙스로 미끄러진 양 (픽셀)
uniform vec4 col_top : source_color = vec4(0.4, 0.55, 0.55, 1.0);
uniform vec4 col_bot : source_color = vec4(0.25, 0.38, 0.40, 1.0);
uniform vec4 col_hi : source_color = vec4(0.6, 0.72, 0.68, 1.0);
uniform float base = 0.3;                   // 능선 기본 높이 (판 높이 비율)
uniform float amp = 0.25;
uniform float freq = 0.004;
uniform float bumps = 1.0;                  // 나무 돌기 크기 (0 = 매끈한 산)
uniform float seed = 0.0;

float hash(float n) { return fract(sin(n * 12.9898 + seed) * 43758.5453); }
float n1(float x) {
	float i = floor(x);
	float f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(hash(i), hash(i + 1.0), f);
}
float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7)) + seed) * 43758.5453); }

void fragment() {
	vec2 px = floor(UV * size_px);
	float x = px.x + scroll;
	float y = size_px.y - px.y;                      // 아래에서 위로
	ivec2 bi = ivec2(mod(px, 4.0));
	float b4[16] = float[](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	float th = (b4[bi.y * 4 + bi.x] + 0.5) / 16.0;
	if (kind == 0) {
		float t = y / size_px.y;
		float q = floor(t * 6.0 + th) / 6.0;
		vec3 c = mix(col_bot.rgb, col_top.rgb, q);
		// 구름: 가로로 긴 덩어리, 천천히
		vec2 cp = vec2((x + wind_time * 6.0) / 90.0, y / 26.0);
		float cl = n1(cp.x + floor(cp.y) * 17.0) * n1(cp.x * 2.3 + 5.0 + floor(cp.y) * 7.0);
		if (cl > 0.42 + (1.0 - t) * 0.2 && t > 0.35) {
			c = mix(c, col_hi.rgb, cl > 0.55 ? 1.0 : 0.55);
		}
		ALBEDO = c;
	} else {
		float h = base + (n1(x * freq) * 0.65 + n1(x * freq * 3.1 + 4.0) * 0.35) * amp;
		h *= size_px.y;
		// 나무 꼭대기 돌기: 일정 간격마다 둥근 혹
		float cellw = 9.0 + bumps * 6.0;
		float cx = floor(x / cellw);
		float fx = (x - cx * cellw) / cellw - 0.5;
		float bh = bumps * (5.0 + hash(cx) * 8.0) * sqrt(max(0.0, 0.25 - fx * fx)) * 2.0;
		float top = h + bh;
		if (y > top) {
			discard;
		}
		float d = (top - y) / size_px.y;
		vec3 c = mix(col_top.rgb, col_bot.rgb, clamp(floor(d * 8.0 + th) / 4.0, 0.0, 1.0));
		if (top - y < 2.0) {
			c = col_hi.rgb;                         // 실루엣 윗선 밝게 (역광)
		}
		ALBEDO = c;
	}
}
