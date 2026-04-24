using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

using View3D.view;

namespace OpenGL3DViewerNET10.Draw
{
    internal class BackgroundDraw
    { 
        int shader;
        int dummyVao;   // add this
        // Vertex shader
        private const string VertSrc = @"
                                #version 330 core

                                const vec2 verts[3] = vec2[](
                                    vec2(-1.0, -1.0),
                                    vec2( 3.0, -1.0),
                                    vec2(-1.0,  3.0)
                                );

                                out vec2 uv;

                                void main()
                                {
                                    vec2 pos = verts[gl_VertexID];
                                    gl_Position = vec4(pos, 0.0, 1.0);

                                    uv = pos * 0.5 + 0.5;
                                }
";

        // Fragment shader
        private const string FragSrc = @"
                                #version 330 core

                                in vec2 uv;
                                out vec4 FragColor;

                                uniform vec4 topColor;
                                uniform vec4 bottomColor;

                                void main()
                                {
                                    FragColor = mix(bottomColor, topColor, uv.y);
                                }
";

        // Call once during load / whenever PrintAreaWidth or PrintAreaDepth changes
        public void Init()
        {
            shader = createShaderProgram();

            dummyVao = GL.GenVertexArray();  // add this

#if false   // Mono background clear color was too dark; using shader gradient instead
            GL.ClearColor(0.2f, 0.3f, 0.4f, 1f);
#endif
        }

        int createShaderProgram()
        {
            // create the shader program
            int shaderProgram = GL.CreateProgram();

            // create the vertex shader
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, VertSrc);
            GL.CompileShader(vertexShader);

            // Same as vertex shader
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, FragSrc);
            GL.CompileShader(fragmentShader);

            // Attach the shaders to the shader program
            GL.AttachShader(shaderProgram, vertexShader);
            GL.AttachShader(shaderProgram, fragmentShader);

            // Link the program to OpenGL
            GL.LinkProgram(shaderProgram);

            // delete the shaders
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            return shaderProgram;
        }

        // Call each frame in place of the original GL.Begin/End block
        public void Draw()
        {
            GL.Disable(EnableCap.DepthTest);
            GL.UseProgram(shader);

            // bind empty VAO to satisfy Core Profile requirement
            GL.BindVertexArray(dummyVao);

            float r = (SettingsService.Instance.Settings.BackgroundTopColor >> 16 & 0xFF) / 255f;
            float g = (SettingsService.Instance.Settings.BackgroundTopColor >> 8 & 0xFF) / 255f;
            float b = (SettingsService.Instance.Settings.BackgroundTopColor & 0xFF) / 255f;
            float a = (SettingsService.Instance.Settings.BackgroundTopColor >> 24 & 0xFF) / 255f;
            Vector4 topColor = new Vector4(r, g, b, a);

            r = (SettingsService.Instance.Settings.BackgroundBottomColor >> 16 & 0xFF) / 255f;
            g = (SettingsService.Instance.Settings.BackgroundBottomColor >> 8 & 0xFF) / 255f;
            b = (SettingsService.Instance.Settings.BackgroundBottomColor & 0xFF) / 255f;
            a = (SettingsService.Instance.Settings.BackgroundBottomColor >> 24 & 0xFF) / 255f;
            Vector4 bottomColor = new Vector4(r, g, b, a);

            GL.Uniform4(GL.GetUniformLocation(shader, "topColor"), topColor);
            GL.Uniform4(GL.GetUniformLocation(shader, "bottomColor"), bottomColor);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            GL.BindVertexArray(0);  // unbind after draw
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(dummyVao); 
            GL.DeleteProgram(shader);
        }
    }
}
