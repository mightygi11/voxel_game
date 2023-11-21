// NOTE: Shader automatically converted from Godot Engine 4.1.3.stable.mono's StandardMaterial3D.

shader_type spatial;
render_mode blend_mul,depth_draw_opaque,cull_disabled,diffuse_burley,specular_schlick_ggx,shadows_disabled;
uniform vec4 albedo : source_color;
uniform sampler2D texture_albedo : source_color,filter_nearest,repeat_enable;
uniform float point_size : hint_range(0,128);
uniform float roughness : hint_range(0,1);
uniform sampler2D texture_metallic : hint_default_white,filter_nearest,repeat_enable;
uniform vec4 metallic_texture_channel;
uniform sampler2D texture_roughness : hint_roughness_r,filter_nearest,repeat_enable;
uniform float specular;
uniform float metallic;
uniform sampler2D texture_emission : source_color, hint_default_black,filter_nearest,repeat_enable;
uniform vec4 emission : source_color;
uniform float emission_energy;
uniform float rim : hint_range(0,1);
uniform float rim_tint : hint_range(0,1);
uniform sampler2D texture_rim : hint_default_white,filter_nearest,repeat_enable;
uniform float clearcoat : hint_range(0,1);
uniform float clearcoat_roughness : hint_range(0,1);
uniform sampler2D texture_clearcoat : hint_default_white,filter_nearest,repeat_enable;
uniform vec3 uv1_scale;
uniform vec3 uv1_offset;
uniform vec3 uv2_scale;
uniform vec3 uv2_offset;
uniform sampler2D noise;
uniform sampler2D normalmap;
uniform float height_scale = 200;

varying vec2 pos;

float height(vec2 position, float time) {
  vec2 pos2 = vec2(position.x + time / 50.0, position.y + time / 50.0);
  return texture(noise, pos2).x;
}

void vertex() {
  pos = VERTEX.xz / 16.0;
  float k = height(pos, TIME);
  NORMAL = normalize(vec3(k - height(pos + vec2(0.1, 0.0), TIME), 0.1, k - height(pos + vec2(0.0, 0.1), TIME)));
  VERTEX.y += (k * height_scale) - 0.6;
}


void fragment() {
	vec2 base_uv = UV;
	vec4 albedo_tex = texture(texture_albedo,base_uv);
	ALBEDO = albedo.rgb * albedo_tex.rgb;
	float metallic_tex = dot(texture(texture_metallic,base_uv),metallic_texture_channel);
	METALLIC = metallic_tex * metallic;
	vec4 roughness_texture_channel = vec4(1.0,0.0,0.0,0.0);
	float roughness_tex = dot(texture(texture_roughness,base_uv),roughness_texture_channel);
	ROUGHNESS = roughness_tex * roughness;
	SPECULAR = specular;
	vec3 emission_tex = texture(texture_emission,base_uv).rgb;
	EMISSION = (emission.rgb+emission_tex)*emission_energy;
	vec2 rim_tex = texture(texture_rim,base_uv).xy;
	RIM = rim*rim_tex.x;	RIM_TINT = rim_tint*rim_tex.y;
	vec2 clearcoat_tex = texture(texture_clearcoat,base_uv).xy;
	CLEARCOAT = clearcoat*clearcoat_tex.x;	CLEARCOAT_ROUGHNESS = clearcoat_roughness*clearcoat_tex.y;
}
