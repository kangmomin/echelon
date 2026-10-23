// 전투 효과 입자 공통: 멀티메시 인스턴스 = 입자 하나. 카메라를 향한 사각형(빌보드)으로 편다.
// 인스턴스 변환: 원점 = 중심, 대각 = 크기(m). INSTANCE_CUSTOM: x = 회전, y = 씨앗, z = 나이 비율(0~1).

varying vec4 p_color;
varying vec3 p_custom;

float fx_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float fx_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(fx_hash(i), fx_hash(i + vec2(1.0, 0.0)), u.x),
			mix(fx_hash(i + vec2(0.0, 1.0)), fx_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void vertex() {
	vec3 center = MODEL_MATRIX[3].xyz;
	float size = length(MODEL_MATRIX[0].xyz);
	float c = cos(INSTANCE_CUSTOM.x);
	float s = sin(INSTANCE_CUSTOM.x);
	vec2 v = vec2(c * VERTEX.x - s * VERTEX.y, s * VERTEX.x + c * VERTEX.y) * size;
	vec3 world = center + INV_VIEW_MATRIX[0].xyz * v.x + INV_VIEW_MATRIX[1].xyz * v.y;
	POSITION = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(world, 1.0));
	p_color = COLOR;
	p_custom = INSTANCE_CUSTOM.xyz;
}
