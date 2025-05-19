#version 330 core
layout (location = 0) in vec3 aPos;        // Vertex position
layout (location = 1) in vec3 aNormal;     // Vertex normal
layout (location = 2) in vec4 aColor;      // Vertex color
layout (location = 3) in vec2 aTexCoords;  // Texture coordinates

// Outputs to Fragment Shader
out vec3 v_worldPosition;
out vec3 v_worldNormal;
out vec4 v_vertexColor;
out vec2 v_texCoords;

// Uniforms
uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    vec4 worldPos4 = model * vec4(aPos, 1.0);
    v_worldPosition = vec3(worldPos4);
    v_worldNormal = normalize(mat3(transpose(inverse(model))) * aNormal);
    v_vertexColor = aColor;
    v_texCoords = aTexCoords;
    gl_Position = projection * view * worldPos4;
}