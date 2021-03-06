#version 450

layout(location = 0) in vec3 Position;
layout(location = 1) in vec2 TextureCoordinates;

layout(location = 0) out vec2 fsin_uv;
layout(location = 1) out vec2 fsin_pos;
layout(location = 2) out vec2 fsin_res;

layout(set = 0, binding = 2) uniform Transform {
    mat4 Matrix;
    vec2 Resolution;
};

void main()
{
    vec4 pos = vec4(Position, 1) * Matrix;
    
    pos /= vec4(Resolution, 1, 1);
    pos *= vec4(2, -2, 1, 1);
    
    pos -= vec4(1, -1, 0, 0);
    gl_Position = pos;
    
    fsin_uv = TextureCoordinates;
    fsin_pos = pos.xy;
    fsin_pos = (fsin_pos + vec2(1, 1)) / 2;
    fsin_res = Resolution;
}