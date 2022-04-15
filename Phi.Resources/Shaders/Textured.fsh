#version 450
 
layout(set = 1, binding = 0) uniform texture2D Input;
layout(set = 1, binding = 1) uniform sampler Sampler;

layout(set = 1, binding = 2) uniform Tint
{
    vec3 RGBTintColor;
    float TintFactor;
    float FinalAlpha;
};

layout(set = 1, binding = 3) uniform texture2D ClipMap;
layout(set = 1, binding = 4) uniform sampler ClipMapSampler;

layout(set = 1, binding = 5) uniform Filters
{
    float BlurRadius;
};

layout(location = 0) in vec2 fsin_uv;
layout(location = 1) in vec2 fsin_pos;
layout(location = 2) in vec2 fsin_res;

layout(location = 0) out vec4 fsout_color;

vec4 lerp(vec4 a, vec4 b, float t)
{
    return a + (b - a) * t;
}

// Source: https://www.shadertoy.com/view/Xltfzj
vec4 blur(vec4 fragColor) {
    float Pi = 6.28318530718; // Pi*2

    // GAUSSIAN BLUR SETTINGS {{{
    float Directions = 16.; // BLUR DIRECTIONS (Default 16.0 - More is better but slower)
    float Quality = 4.0; // BLUR QUALITY (Default 4.0 - More is better but slower)
    float Size = BlurRadius; // BLUR SIZE (Radius)
    // GAUSSIAN BLUR SETTINGS }}}

    vec2 Radius = Size / fsin_res;

    // Normalized pixel coordinates (from 0 to 1)
    vec2 uv = fsin_uv;
    // Pixel colour
    vec4 Color = texture(sampler2D(Input, Sampler), uv);

    // Blur calculations
    for( float d=0.0; d<Pi; d+=Pi/Directions)
    {
        for(float i=1.0/Quality; i<=1.0; i+=1.0/Quality)
        {
            Color += texture(sampler2D(Input, Sampler), uv+vec2(cos(d),sin(d))*Radius*i);
        }
    }

    // Output to screen
    Color /= Quality * Directions - 15.0;
    return Color;
}

void main()
{
    vec2 texCoords = fsin_uv;
    vec4 inputColor = texture(sampler2D(Input, Sampler), texCoords);
    vec4 clipColor = texture(sampler2D(ClipMap, ClipMapSampler), fsin_pos);
   
    if (clipColor.x == 0 && clipColor.y == 0 && clipColor.z == 0) discard;
    
    vec4 tintedColor = vec4(inputColor.xyz * RGBTintColor, inputColor.w);
    tintedColor = lerp(tintedColor, vec4(RGBTintColor, TintFactor), TintFactor);
    tintedColor.w *= FinalAlpha;
    
    if (BlurRadius > 1) 
        tintedColor = blur(tintedColor);
    
    fsout_color = tintedColor;
}