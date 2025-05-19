#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec4 aColor;
layout (location = 3) in vec2 aTexCoords; // Ensure location 3

out vec4 v_vertexColor;
out vec2 v_texCoords;   // Pass UVs

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    gl_Position = projection * view * model * vec4(aPos, 1.0);
    v_vertexColor = aColor;
    v_texCoords = aTexCoords; // Make sure this line is present
}