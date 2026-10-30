shader_type spatial;
// 연못 물 (땅에 눕힌 판): 도트 단위 물결 줄무늬 + 가장자리 거품 + 하늘 반사 밝은 점
render_mode unshaded, cull_disabled, shadows_disabled;

global uniform float wind_time;

uniform vec2 size_px = vec2(96.0, 64.0);
uniform vec4 deep : source_color = vec4(0.18, 0.36, 0.42, 1.0);
uniform vec4 mid : source_color = vec4(0.27, 0.52, 0.56, 1.0);
uniform vec4 light : source_color = vec4(0.55, 0.80, 0.78, 1.0);
uniform vec4 foam : source_color = vec4(0.88, 0.96, 0.90, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
	vec2 cell = floor(UV * size_px);
	vec2 p = (cell + 0.5) / size_px * 2.0 - 1.0;
	// 가장자리를 살짝 울퉁불퉁하게
	float ang = atan(p.y, p.x);
	float r = 1.0 - 0.06 * sin(ang * 5.0) - 0.04 * sin(ang * 9.0 + 1.0);
	float d = length(p) / r;
	if (d > 1.0) {
		discard;
	}
	float t = wind_time;
	vec3 c = mix(mid.rgb, deep.rgb, smoothstep(0.2, 0.9, 1.0 - d));
	// 가로 물결 줄 (2px, 천천히 흐름)
	float wave = sin(cell.y * 0.9 + sin(cell.x * 0.15 + t * 1.3) * 2.0 + t * 0.8);
	if (wave > 0.93 && hash(floor(cell / vec2(3.0, 1.0))) > 0.35) {
		c = light.rgb;
	}
	// 반짝 (드물게)
	if (hash(cell + floor(t * 2.0)) > 0.996) {
		c = foam.rgb;
	}
	// 가장자리 거품 1px + 그 안쪽 밝은 띠
	float px = 2.0 / min(size_px.x, size_px.y);
	if (d > 1.0 - px * 1.2) {
		c = foam.rgb;
	} else if (d > 1.0 - px * 3.0) {
		c = light.rgb;
	}
	ALBEDO = c;
}
