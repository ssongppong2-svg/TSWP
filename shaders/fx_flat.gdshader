shader_type spatial;
// 땅에 눕힌 효과 판 (베기 궤적): 빛 영향 없이 밝게, 도트 가위 투명, 단면 시점에서도 걷어 내지 않음
render_mode unshaded, cull_disabled, depth_draw_opaque, shadows_disabled;

uniform sampler2D tex : source_color, filter_nearest, repeat_disable;
uniform vec4 region = vec4(0.0, 0.0, 1.0, 1.0);

void fragment() {
	vec4 c = texture(tex, region.xy + UV * region.zw);
	if (c.a < 0.5) {
		discard;
	}
	ALBEDO = c.rgb;
}
