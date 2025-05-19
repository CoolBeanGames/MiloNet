#version 330 core
out vec4 FragColor;

in vec4 v_vertexColor;
in vec2 v_texCoords;   // Receive UVs

uniform sampler2D u_albedoTexture; // Sampler uniform

void main()
{
    vec4 texColor = texture(u_albedoTexture, v_texCoords);
    FragColor = texColor * v_vertexColor; // Modulate with vertex color
    // Or, for testing pure texture: FragColor = texColor;
    // Or, for PS1 opaque style: FragColor = vec4(texColor.rgb, 1.0);
}