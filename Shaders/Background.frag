#version 330 core

in vec2 uv;
out vec4 FragColor;

uniform vec4 topColor;
uniform vec4 bottomColor;

void main()
{
    FragColor = mix(bottomColor, topColor, uv.y);
}