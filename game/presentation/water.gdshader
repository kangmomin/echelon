shader_type spatial;
// 강물 띠: UV.x = 가로(0~1), UV.y = 길이(m). 잔물결·물가 투명.
render_mode cull_disabled, depth_draw_opaque;

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), u.x), mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	vec2 p = vec2(UV.x * 30.0, UV.y);
	float r = vnoise(p * 0.3 + vec2(0.0, -TIME * 1.2)) * 0.6 + vnoise(p * 0.9 + vec2(TIME * 0.5, -TIME * 2.0)) * 0.4;
	float edge = min(UV.x, 1.0 - UV.x);
	vec3 deep = vec3(0.08, 0.20, 0.26);
	vec3 shallow = vec3(0.22, 0.36, 0.36);
	vec3 col = mix(shallow, deep, smoothstep(0.0, 0.35, edge));
	col += vec3(0.05, 0.06, 0.06) * r;
	// 물가 거품.
	col = mix(col, vec3(0.62, 0.66, 0.64), smoothstep(0.06, 0.0, edge) * 0.5);
	ALBEDO = col;
	ROUGHNESS = 0.12 + 0.1 * r;
	SPECULAR = 0.6;
	NORMAL_MAP = normalize(vec3(r - 0.5, 0.5 - vnoise(p * 1.7 + TIME), 2.0)) * 0.5 + 0.5;
	ALPHA = smoothstep(0.0, 0.08, edge) * 0.92;
}
