#version 330 core

in vec3 Normal;
out vec4 FragColor;

uniform vec4 ourColor; // Add this line

void main()
{
    float lighting = dot(normalize(Normal), normalize(vec3(1,1,1)));
    lighting = max(lighting,0.2);

    FragColor = vec4(vec3(lighting),1.0);
}