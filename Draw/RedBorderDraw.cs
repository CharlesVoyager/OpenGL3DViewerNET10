using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using View3D;

namespace OpenGL3DViewerNET10.Draw
{
    internal class RedBorderDraw
    {
        // Red border around print area
        int vao;
        int vbo;
        int shader;
        int redBorderVertexCount;

        // Vertex shader
        private const string VertSrc = @"
                                #version 330 core
                                layout(location = 0) in vec3 aPos;
                                uniform mat4 uMVP;
                                void main() {
                                    gl_Position = uMVP * vec4(aPos, 1.0);
                                }
";

        // Fragment shader
        private const string FragSrc = @"
                                #version 330 core
                                out vec4 fragColor;
                                uniform vec4 uColor;
                                void main() {
                                    fragColor = uColor;
                                }
";

        // Call once during load / whenever PrintAreaWidth or PrintAreaDepth changes
        public void Init()
        {            
            shader = createShaderProgram();

            int pad = 2, tri = 10;
            float w = MainWindow.main.threeDSettings.PrintAreaWidth, d = MainWindow.main.threeDSettings.PrintAreaDepth;

            // Same vertex order as the original LineStrip
            var verts = new float[]
            {
                -pad,         d + pad,       -pad,
                -pad,        -pad,           -pad,
                 w + pad,    -pad,           -pad,
                 w + pad,     d + pad,       -pad,
                 w / 2f + tri, d + pad,     -pad,
                 w / 2f,       d + pad + tri,-pad,
                 w / 2f - tri, d + pad,     -pad,
                -pad,          d + pad,     -pad,   // close the strip
            };
            redBorderVertexCount = verts.Length / 3;

            // VAO
            vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);

            // VBO
            vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer,
                          verts.Length * sizeof(float),
                          verts,
                          BufferUsageHint.StaticDraw);

            // position attribute  (location = 0, 3 floats, no offset)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
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
            Matrix4 model = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 proj = Matrix4.Identity;

            MainWindow.main.threeDCamera.GetModelViewProj(ref model, ref view, ref proj);
            Matrix4 mvp = model * view * proj;

            GL.UseProgram(shader);

            // Pass the combined MVP matrix
            int mvpLoc = GL.GetUniformLocation(shader, "uMVP");
            int colorLoc = GL.GetUniformLocation(shader, "uColor");
            GL.UniformMatrix4(mvpLoc, false, ref mvp);
            GL.Uniform4(colorLoc, 1f, 0f, 0f, 0f); // Red.

            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.LineStrip, 0, redBorderVertexCount);
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
