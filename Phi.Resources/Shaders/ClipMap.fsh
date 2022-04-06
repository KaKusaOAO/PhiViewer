#version 450

layout(location = 0) in vec2 fsin_uv;
layout(location = 1) in vec2 fsin_pos;

layout(location = 0) out vec4 fsout_color;


void main()
{
    fsout_color = vec4(1, 1, 1, 1);
}