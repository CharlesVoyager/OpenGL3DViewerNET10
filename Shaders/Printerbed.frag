#version 330 core

in vec2 worldXY;
out vec4 FragColor;

uniform vec4 plateColor;
uniform vec4 gridColor;
uniform float gridSpacing;

void main()
{
    vec2 coord = worldXY / gridSpacing;

    vec2 grid = abs(fract(coord - 0.5) - 0.5) / fwidth(coord);

    float line = min(grid.x, grid.y);

    float gridMask = 1.0 - min(line, 1.0);

    vec4 color = mix(plateColor, gridColor, gridMask);

    FragColor = color;
}