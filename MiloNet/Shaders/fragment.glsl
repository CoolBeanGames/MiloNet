#version 330 core
out vec4 FragColor;

// Inputs (not all used for this specific test, but keep them for shader structure)
in vec3 v_worldPosition;
// in vec3 v_worldNormal; 
// in vec4 v_vertexColor;
// in vec2 v_texCoords;

// uniform sampler2D u_albedoTexture; // Not used

uniform int u_lightType; 
// uniform vec3 u_lightPosition_world;  
uniform vec3 u_lightDirection_world; // THE VECTOR WE ARE TESTING
// ... other light uniforms (not used but must be declared if Render.cs sets them)


void main()
{
    if (u_lightType == 3) // Your SpotLight type
    {
        // Visualize the spotlight's main pointing direction vector (u_lightDirection_world)
        // This should be a constant color across the entire object if the uniform is received correctly.
        // And it should NOT change when the camera moves.
        // It SHOULD change if you re-export the GLB with the light pointing in a different WORLD direction.
        FragColor = vec4(normalize(u_lightDirection_world) * 0.5 + 0.5, 1.0);
    }
    else
    {
        FragColor = vec4(0.0, 0.0, 0.0, 1.0); // Black if not a spotlight
    }
}