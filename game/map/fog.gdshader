// Fog of war overlay. The fog texture has explored in R and visible in G (0..1):
// unexplored -> solid dark, explored -> greyed, darkened map, visible -> map as is.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec4 unexplored_colour : source_color = vec4(0.02, 0.02, 0.03, 1.0);
uniform float memory_brightness : hint_range(0.0, 1.0) = 0.55;
uniform float memory_desaturation : hint_range(0.0, 1.0) = 0.85;

void fragment() {
	vec2 fog = texture(TEXTURE, UV).rg;
	vec3 scene = texture(screen_tex, SCREEN_UV).rgb;
	float grey = dot(scene, vec3(0.299, 0.587, 0.114));
	vec3 remembered = mix(scene, vec3(grey), memory_desaturation) * memory_brightness;
	vec3 colour = mix(unexplored_colour.rgb, remembered, fog.r);
	COLOR = vec4(mix(colour, scene, fog.g), 1.0);
}
