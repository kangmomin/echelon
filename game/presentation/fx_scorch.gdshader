shader_type spatial;
// 포탄 자국(땅 위 평면). 가운데가 검고 가장자리가 흩어진 얼룩. 인스턴스 색 알파 = 남은 진하기.
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

varying vec4 s_color;
varying float s_seed;

float h2(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float n2(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(h2(i), h2(i + vec2(1.0, 0.0)), u.x), mix(h2(i + vec2(0.0, 1.0)), h2(i + vec2(1.0, 1.0)), u.x), u.y);
}

void vertex() {
	s_color = COLOR;
	s_seed = INSTANCE_CUSTOM.y;
}

void fragment() {
	vec2 q = UV * 2.0 - 1.0;
	float n = n2(q * 3.0 + s_seed * 41.0) * 0.6 + n2(q * 7.0 - s_seed * 13.0) * 0.4;
	float r = length(q) + (n - 0.5) * 0.6;
	float a = 1.0 - smoothstep(0.2, 0.9, r);
	ALBEDO = mix(vec3(0.05, 0.045, 0.04), vec3(0.22, 0.19, 0.15), smoothstep(0.1, 0.8, r));
	ALPHA = s_color.a * a * 0.85;
}
