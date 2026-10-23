shader_type spatial;
// 연기·먼지·흙(알파 혼합). 가장자리를 노이즈로 흐트러뜨린 둥근 덩어리, 위쪽을 조금 밝게(가짜 햇빛).
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

#include "res://game/presentation/fx_particle_common.gdshaderinc"

void fragment() {
	vec2 q = UV * 2.0 - 1.0;
	float seed = p_custom.y * 37.0;
	float n = fx_noise(q * 2.2 + seed) * 0.6 + fx_noise(q * 5.0 - seed) * 0.4;
	float r = length(q) + (n - 0.5) * 0.55;
	float a = 1.0 - smoothstep(0.35, 0.95, r);
	float fade_in = smoothstep(0.0, 0.06, p_custom.z);
	vec3 col = p_color.rgb * (0.82 + 0.32 * (1.0 - UV.y)) * (0.9 + 0.2 * n);
	ALBEDO = col;
	ALPHA = clamp(p_color.a * a * fade_in, 0.0, 1.0);
}
