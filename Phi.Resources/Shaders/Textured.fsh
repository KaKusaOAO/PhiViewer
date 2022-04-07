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

layout(location = 0) in vec2 fsin_uv;
layout(location = 1) in vec2 fsin_pos;

layout(location = 0) out vec4 fsout_color;

vec4 lerp(vec4 a, vec4 b, float t)
{
    return a + (b - a) * t;
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
    
    // tintedColor = clipColor;
    
    fsout_color = tintedColor;
}