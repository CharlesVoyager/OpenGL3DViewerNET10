using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing;
using View3D;

namespace OpenGL3DViewerNET10.Draw
{
    internal class Background
    { 
        int shader;

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

            Color color;
            color = MainWindow.main.threeDSettings.BackgroundTopBackgroundColor();
            Vector4 topColor = new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            color = MainWindow.main.threeDSettings.BackgroundBottomBackgroundColor();
            Vector4 bottomColor = new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

            GL.Uniform4(GL.GetUniformLocation(shader, "topColor"), topColor);
            GL.Uniform4(GL.GetUniformLocation(shader, "bottomColor"), bottomColor);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        // Clean up when done
        public void Dispose()
        {
        }
    }

}
