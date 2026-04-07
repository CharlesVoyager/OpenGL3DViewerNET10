using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using View3D;
using View3D.model;

namespace OpenGL3DViewerNET10.Draw
{
    internal class PrinterbedDraw
    {
        int shader;

        int vao;
        int vbo;
        int modelLoc, viewLoc, projLoc;

        int plateColorLoc;
        int gridColorLoc;
        int gridSpacingLoc;

        // Vertex shader
        private const string VertSrc = @"
                                #version 330 core

                                layout(location = 0) in vec3 aPosition;

                                uniform mat4 model;
                                uniform mat4 view;
                                uniform mat4 projection;

                                out vec2 worldXY;

                                void main()
                                {
                                    worldXY = aPosition.xy;
                                    gl_Position = projection * view * model * vec4(aPosition,1.0);
                                }
";

        // Fragment shader
        private const string FragSrc = @"
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
";

        // Call once during load / whenever PrintAreaWidth or PrintAreaDepth changes
        public void Init()
        {
            shader = createShaderProgram();

            GL.UseProgram(shader);

            modelLoc = GL.GetUniformLocation(shader, "model");
            viewLoc = GL.GetUniformLocation(shader, "view");
            projLoc = GL.GetUniformLocation(shader, "projection");

            plateColorLoc = GL.GetUniformLocation(shader, "plateColor");
            gridColorLoc = GL.GetUniformLocation(shader, "gridColor");
            gridSpacingLoc = GL.GetUniformLocation(shader, "gridSpacing");

            float x1 = MainWindow.main.threeDSettings.PrintAreaWidth;
            float y1 = MainWindow.main.threeDSettings.PrintAreaDepth;

            float[] vertices =
            {
                0f, 0f, 0f,
                x1, 0f, 0f,
                x1, y1, 0f,
                0f, y1, 0f
            };

            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();

            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer,
                          vertices.Length * sizeof(float),
                          vertices,
                          BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(
                0,
                3,
                VertexAttribPointerType.Float,
                false,
                3 * sizeof(float),
                0);
            GL.EnableVertexAttribArray(0);
            GL.Enable(EnableCap.DepthTest);
            GL.BindVertexArray(0);
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
            if (MainWindow.main.threeDSettings.IsPrintbed() != true) return;

            Matrix4 model = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 proj = Matrix4.Identity;

            MainWindow.main.threeDCamera.GetModelViewProj(ref model, ref view, ref proj);

            GL.UseProgram(shader);

            GL.UniformMatrix4(modelLoc, false, ref model);
            GL.UniformMatrix4(viewLoc, false, ref view);
            GL.UniformMatrix4(projLoc, false, ref proj);

            GL.Uniform4(plateColorLoc, new Vector4(0.8f, 0.8f, 0.8f, 1f));
            GL.Uniform4(gridColorLoc, new Vector4(0f, 0f, 0f, 0.5f));
            GL.Uniform1(gridSpacingLoc, 10f);   // 10 mm grid

            GL.BindVertexArray(vao);

            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
        }

        // Clean up when done
        public void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
            GL.DeleteProgram(shader);
        }
    }
}
