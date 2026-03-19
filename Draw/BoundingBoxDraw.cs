using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using View3D;
using View3D.model;

namespace OpenGL3DViewerNET10.Draw
{
    internal class BoundingBoxDraw
    {
        int shader;

        int vao;
        int vbo;
        int modelLoc, viewLoc, projLoc;

        // Vertex shader
        private const string VertSrc = @"
                                #version 330 core

                                layout(location=0) in vec3 aPosition;

                                uniform mat4 model;
                                uniform mat4 view;
                                uniform mat4 projection;

                                out vec3 Normal;

                                void main()
                                {
                                    gl_Position = projection * view * model * vec4(aPosition,1.0);
                                }
";

        // Fragment shader
        private const string FragSrc = @"
                                #version 330 core

                                out vec4 FragColor;
                                uniform vec4 ourColor; 

                                void main()
                                {
                                    FragColor = ourColor;
                                }
";

        // Call once during load / whenever PrintAreaWidth or PrintAreaDepth changes
        public void Init()
        {
            shader = createShaderProgram();

            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();

            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            // Pre-allocate space for 24 vertices (X, Y, Z)
            // We pass IntPtr.Zero to just reserve the space on the GPU
            GL.BufferData(BufferTarget.ArrayBuffer, 24 * 3 * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);

            // Define the layout (assuming your shader uses location 0 for position)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            modelLoc = GL.GetUniformLocation(shader, "model");
            viewLoc = GL.GetUniformLocation(shader, "view");
            projLoc = GL.GetUniformLocation(shader, "projection");
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
            PrintModel m = null;

            MainWindow.main.Dispatcher.Invoke(() =>
            {
                m = MainWindow.main.stlComposer.SingleSelectedModel;
            });

            if (m == null) return;

            float[] verticesBbox = {
                m.xMin, m.yMin, m.zMin, m.xMax, m.yMin, m.zMin,
                m.xMin, m.yMin, m.zMin, m.xMin, m.yMax, m.zMin,
                m.xMin, m.yMin, m.zMin, m.xMin, m.yMin, m.zMax,
                m.xMax, m.yMax, m.zMax, m.xMin, m.yMax, m.zMax,
                m.xMax, m.yMax, m.zMax, m.xMax, m.yMin, m.zMax,
                m.xMax, m.yMax, m.zMax, m.xMax, m.yMax, m.zMin,
                m.xMin, m.yMax, m.zMax, m.xMin, m.yMax, m.zMin,
                m.xMin, m.yMax, m.zMax, m.xMin, m.yMin, m.zMax,
                m.xMax, m.yMax, m.zMin, m.xMax, m.yMin, m.zMin,
                m.xMax, m.yMax, m.zMin, m.xMin, m.yMax, m.zMin,
                m.xMax, m.yMin, m.zMax, m.xMin, m.yMin, m.zMax,
                m.xMax, m.yMin, m.zMax, m.xMax, m.yMin, m.zMin
            };

            GL.Enable(EnableCap.DepthTest);

            // 1. Bind the Bounding Box VBO
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            // 2. Upload the new data to the START of the buffer (offset 0)
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, verticesBbox.Length * sizeof(float), verticesBbox);

            Matrix4 model = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 proj = Matrix4.Identity;

            MainWindow.main.threeDCamera.GetModelViewProj(ref model, ref view, ref proj);

            GL.UseProgram(shader);

            GL.UniformMatrix4(modelLoc, false, ref model);
            GL.UniformMatrix4(viewLoc, false, ref view);
            GL.UniformMatrix4(projLoc, false, ref proj);

            // 3. Bind the BBox VAO and Draw
            GL.BindVertexArray(vao);

            // 4. Set your BBox color (as discussed previously)
            int colorLoc = GL.GetUniformLocation(shader, "ourColor");
            GL.Uniform4(colorLoc, Color4.LimeGreen);

            GL.DrawArrays(PrimitiveType.Lines, 0, 24);
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
