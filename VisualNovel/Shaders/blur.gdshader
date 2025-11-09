shader_type canvas_item;

// 模糊强度，值越大越模糊，也越消耗性能
uniform float blur_amount : hint_range(0.0, 5.0) = 1.0;

void fragment() {
    // 使用简单的盒状模糊 (Box Blur)
    vec2 tex_pixel_size = 1.0 / vec2(textureSize(TEXTURE, 0));
    vec4 blurred_color = vec4(0.0);
    int sample_count = 0;

    // 定义模糊采样的半径。半径越大，模糊效果越强，但性能开销也越大。
    const int kernel_radius = 5;

    for (int x = -kernel_radius; x <= kernel_radius; x++) {
        for (int y = -kernel_radius; y <= kernel_radius; y++) {
            blurred_color += texture(TEXTURE, UV + vec2(float(x), float(y)) * tex_pixel_size * blur_amount);
            sample_count++;
        }
    }

    COLOR = blurred_color / float(sample_count);
}