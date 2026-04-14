using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using View3D;

namespace OpenGL3DViewerNET10.Draw
{
    /// <summary>
    /// Draws a wireframe box representing the full 3D printer build volume:
    /// 256 mm (X) × 256 mm (Y) × 200 mm (Z).
    ///
    /// The box sits on the printer bed (Z = 0) and its footprint matches
    /// PrinterbedDraw exactly (0,0,0) → (256,256,0).  The top face is at
    /// Z = 200.  Twelve edges are drawn as GL_LINES with a semi-transparent
    /// blue colour so the frame is clearly visible but does not obscure the
    /// model.
    /// </summary>
    internal class PrinterAreaFrameDraw
    {
        // ── GL handles ────────────────────────────────────────────────────────
        private int shader;
        private int vao;
        private int vbo;
        private int modelLoc, viewLoc, projLoc, colorLoc;

        // ── Shaders ───────────────────────────────────────────────────────────
        private const string VertSrc = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;

            uniform mat4 model;
            uniform mat4 view;
            uniform mat4 projection;

            void main()
            {
                gl_Position = projection * view * model * vec4(aPosition, 1.0);
            }
        ";

        private const string FragSrc = @"
            #version 330 core
            out vec4 FragColor;
            uniform vec4 ourColor;

            void main()
            {
                FragColor = ourColor;
            }
        ";

        // ── Init ──────────────────────────────────────────────────────────────
        /// <summary>Call once after the OpenGL context is created (from OnLoad).</summary>
        public void Init()
        {
            shader = CreateShaderProgram();

            GL.UseProgram(shader);
            modelLoc = GL.GetUniformLocation(shader, "model");
            viewLoc  = GL.GetUniformLocation(shader, "view");
            projLoc  = GL.GetUniformLocation(shader, "projection");
            colorLoc = GL.GetUniformLocation(shader, "ourColor");

            // 8 corners of the build volume
            float x0 = 0f,      y0 = 0f,      z0 = 0f;
            float x1 = MainWindow.main.threeDSettings.PrintAreaWidth;
            float y1 = MainWindow.main.threeDSettings.PrintAreaDepth;
            float z1 = MainWindow.main.threeDSettings.PrintAreaHeight;

            // 12 edges → 24 vertices (2 endpoints per edge)
            float[] vertices =
            {
                // ── Bottom face (Z = 0) ────────────────────────────────────
                x0, y0, z0,   x1, y0, z0,   // front-left  → front-right
                x1, y0, z0,   x1, y1, z0,   // front-right → back-right
                x1, y1, z0,   x0, y1, z0,   // back-right  → back-left
                x0, y1, z0,   x0, y0, z0,   // back-left   → front-left

                // ── Top face (Z = BedHeight) ───────────────────────────────
                x0, y0, z1,   x1, y0, z1,
                x1, y0, z1,   x1, y1, z1,
                x1, y1, z1,   x0, y1, z1,
                x0, y1, z1,   x0, y0, z1,

                // ── Vertical pillars ───────────────────────────────────────
                x0, y0, z0,   x0, y0, z1,   // front-left  pillar
                x1, y0, z0,   x1, y0, z1,   // front-right pillar
                x1, y1, z0,   x1, y1, z1,   // back-right  pillar
                x0, y1, z0,   x0, y1, z1,   // back-left   pillar
            };

            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();

            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(
                BufferTarget.ArrayBuffer,
                vertices.Length * sizeof(float),
                vertices,
                BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.BindVertexArray(0);
        }

        // ── Draw ──────────────────────────────────────────────────────────────
        /// <summary>Call every frame from gl_Paint, after PrinterbedDraw.Draw().</summary>
        public void Draw()
        {
            if (MainWindow.main.threeDSettings.IsPrintbed() != true) return;

            Matrix4 model = Matrix4.Identity;
            Matrix4 view = MainWindow.main.threeDCamera.GetViewMatrix();
            Matrix4 proj = MainWindow.main.threeDCamera.GetProjMatrix();

            GL.UseProgram(shader);
            GL.UniformMatrix4(modelLoc, false, ref model);
            GL.UniformMatrix4(viewLoc,  false, ref view);
            GL.UniformMatrix4(projLoc,  false, ref proj);

            // Semi-transparent blue — visible over the grey bed without being distracting
            GL.Uniform4(colorLoc, new Vector4(0f, 0f, 0f, 0.8f));

            GL.Enable(EnableCap.DepthTest);

            // Enable blending so the alpha value is respected
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Lines, 0, 24);   // 12 edges × 2 vertices
            GL.BindVertexArray(0);

            GL.Disable(EnableCap.Blend);
        }

        // ── Dispose ───────────────────────────────────────────────────────────
        /// <summary>Call from OnUnload to free GL resources.</summary>
        public void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
            GL.DeleteProgram(shader);
        }

        // ── Private helpers ───────────────────────────────────────────────────
        private int CreateShaderProgram()
        {
            int prog = GL.CreateProgram();

            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, VertSrc);
            GL.CompileShader(vs);

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, FragSrc);
            GL.CompileShader(fs);

            GL.AttachShader(prog, vs);
            GL.AttachShader(prog, fs);
            GL.LinkProgram(prog);

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            return prog;
        }
    }
}
