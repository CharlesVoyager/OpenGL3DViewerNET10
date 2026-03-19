using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing;
using View3D.model.geom;

namespace View3D.model
{
    public class ModelGLDraw
    {
        PrintModel printModel;

        // STL model
        int shader;
        int stlVao;
        int stlVbo;
        int stlModelLoc, stlViewLoc, stlProjLoc;
        List<float> stlVertices = new List<float>();

        // Vertex shader
        private const string VertSrc = @"
                                #version 330 core

                                layout(location=0) in vec3 aPosition;
                                layout(location=1) in vec3 aNormal;

                                uniform mat4 model;
                                uniform mat4 view;
                                uniform mat4 projection;

                                out vec3 Normal;

                                void main()
                                {
                                    gl_Position = projection * view * model * vec4(aPosition,1.0);
                                    Normal = aNormal;
                                }
";

        // Fragment shader
        private const string FragSrc = @"
                                #version 330 core

                                in vec3 Normal;
                                out vec4 FragColor;

                                void main()
                                {
                                    float lighting = dot(normalize(Normal), normalize(vec3(1,1,1)));
                                    lighting = max(lighting,0.2);

                                    FragColor = vec4(vec3(lighting),1.0);
                                }
";

        public ModelGLDraw(PrintModel model)
        {
            printModel = model;
        }

        // Call once during load / whenever PrintAreaWidth or PrintAreaDepth changes
        public void Init()
        {
            shader = createShaderProgram();
        
            uploadMeshToGPU();

            stlModelLoc = GL.GetUniformLocation(shader, "model");
            stlViewLoc = GL.GetUniformLocation(shader, "view");
            stlProjLoc = GL.GetUniformLocation(shader, "projection");
        }

        private void uploadMeshToGPU()
        {
            stlVertices.Clear();
    
            printModel.Paint();

            for (int i = 0; i < printModel.submesh.glVertices.Length; i += 3)
            {   // [x y z nx ny nz]
                stlVertices.Add(printModel.submesh.glVertices[i]);
                stlVertices.Add(printModel.submesh.glVertices[i + 1]);
                stlVertices.Add(printModel.submesh.glVertices[i + 2]);
                stlVertices.Add(printModel.submesh.glNormals[i]);
                stlVertices.Add(printModel.submesh.glNormals[i] + 1);
                stlVertices.Add(printModel.submesh.glNormals[i] + 2);
            }

            stlVao = GL.GenVertexArray();
            stlVbo = GL.GenBuffer();

            GL.BindVertexArray(stlVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, stlVbo);

            GL.BufferData(
                BufferTarget.ArrayBuffer,
                stlVertices.Count * sizeof(float),
                stlVertices.ToArray(),
                BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
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
        public void Draw()
        {
            if (stlVertices.Count == 0) return;

            GL.Enable(EnableCap.PolygonSmooth);
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);

            Matrix4 model = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 proj = Matrix4.Identity;

            MainWindow.main.threeDCamera.GetModelViewProj(ref model, ref view, ref proj);

            GL.UseProgram(shader);

#if true
            GL.UniformMatrix4(stlViewLoc, false, ref view);
            GL.UniformMatrix4(stlProjLoc, false, ref proj);

            GL.UniformMatrix4(stlModelLoc, false, ref printModel.trans);
            GL.BindVertexArray(stlVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, stlVertices.Count / 6);
#else
            GL.UniformMatrix4(stlModelLoc, false, ref model);
            GL.UniformMatrix4(stlViewLoc, false, ref view);
            GL.UniformMatrix4(stlProjLoc, false, ref proj);
            GL.BindVertexArray(stlVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, stlVertices.Count / 6);
#endif
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(stlVao);
            GL.DeleteBuffer(stlVbo);
            GL.DeleteProgram(shader);
        }
    }
}
