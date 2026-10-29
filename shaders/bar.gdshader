shader_type spatial;
// 머리 위 체력바 (빌보드). 도트 크기 고정: size_px 픽셀, 테두리 1px
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled;

global uniform float bb_stretch;

uniform vec2 size_px = vec2(16.0, 3.0);
uniform float ratio = 1.0;
uniform float ghost = 1.0;                       // 막 깎인 부분 (하얗게 남았다가 줄어든다)
uniform vec4 fill : source_color = vec4(0.9, 0.3, 0.3, 1.0);
uniform vec4 back : source_color = vec4(0.12, 0.1, 0.14, 1.0);

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	vec3 right = normalize(vec3(INV_VIEW_MATRIX[0].x, 0.0, INV_VIEW_MATRIX[0].z));
	vec3 world = origin + right * VERTEX.x + vec3(0.0, VERTEX.y * bb_stretch, 0.0);
	VERTEX = (inverse(MODEL_MATRIX) * vec4(world, 1.0)).xyz;
}

void fragment() {
	vec2 p = floor(UV * size_px);
	float inner_w = size_px.x - 2.0;
	float x = (p.x - 1.0) / inner_w;
	vec3 c = back.rgb;
	bool border = p.x < 1.0 || p.y < 1.0 || p.x >= size_px.x - 1.0 || p.y >= size_px.y - 1.0;
	if (!border) {
		if (x < ratio) {
			c = fill.rgb;
		} else if (x < ghost) {
			c = vec3(1.0, 0.95, 0.85);
		} else {
			c = vec3(0.25, 0.2, 0.26);
		}
	}
	ALBEDO = c;
}
