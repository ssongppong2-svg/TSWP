shader_type canvas_item;
// 물결 파동 한 걸음 (GPU 핑퐁): R = 지금 높이, G = 이전 높이 (0.5 = 0)
//  새 높이 = (이웃 넷 평균 × 2 - 이전) × 감쇠  — 2D 파동 방정식
//  drops: 이번 걸음에 떨어진 물방울 (uv.xy, 반지름, 세기)
render_mode unshaded, blend_disabled;

uniform sampler2D prev : filter_nearest, repeat_disable;
uniform vec2 texel = vec2(0.0104167, 0.015625);
uniform vec4 drops[4];
uniform float damping = 0.985;

void fragment() {
	vec2 uv = UV;
	vec2 c = texture(prev, uv).rg - 0.5;
	float l = texture(prev, uv - vec2(texel.x, 0.0)).r - 0.5;
	float r = texture(prev, uv + vec2(texel.x, 0.0)).r - 0.5;
	float u = texture(prev, uv - vec2(0.0, texel.y)).r - 0.5;
	float d = texture(prev, uv + vec2(0.0, texel.y)).r - 0.5;
	float h = ((l + r + u + d) * 0.5 - c.g) * damping;
	for (int i = 0; i < 4; i++) {
		vec4 dp = drops[i];
		if (dp.w != 0.0) {
			float dd = length((uv - dp.xy) / texel);
			if (dd < dp.z) {
				h += dp.w * (1.0 - dd / dp.z);
			}
		}
	}
	h = clamp(h, -0.49, 0.49);
	COLOR = vec4(h + 0.5, c.r + 0.5, 0.0, 1.0);
}
