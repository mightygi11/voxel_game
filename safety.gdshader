shader_type spatial;

void fragment() {
	vec3 color = vec3(1.0, abs(sin(TIME*5.0 + UV.y)), 1.0);
	float alpha = abs(sin(TIME*2.0 + UV.y));
	ALPHA = clamp(alpha * alpha - ((UV.y-0.33) * -3.0), 0.0, 1.0);
	ALBEDO = color;
}
