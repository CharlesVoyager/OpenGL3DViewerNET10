using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using View3D;
using View3D.model;

namespace OpenGL3DViewerNET10.Draw
{
    /*
Load STL File (CPU)
↓
Parse Geometry (vertices, normals)
↓
center the model
↓
Fill Mesh (Submesh)
↓
Upload to GPU (VBO / VAO)
↓
Render Loop (OpenGL draw calls)
     */

    public class ModelGLDraw
    {
        PrintModel printModel;

        // STL model
        int shader;
        int stlVao;
        int stlVbo;
        int stlModelLoc, stlViewLoc, stlProjLoc;
        List<float> stlVertices = new List<float>();

        // Add these fields
        int lightDirLoc, lightColorLoc, viewPosLoc;
        int objectColorLoc, ambientLoc, specularLoc, shininessLoc;
        int normalMatrixLoc;

        // Vertex shader
        private const string VertSrc = @"
                                #version 330 core

                                layout(location=0) in vec3 aPosition;
                                layout(location=1) in vec3 aNormal;

                                uniform mat4 model;
                                uniform mat4 view;
                                uniform mat4 projection;
                                uniform mat3 normalMatrix; 

                                out vec3 FragPos;    // world-space position
                                out vec3 Normal;     // world-space normal

                                void main()
                                {
                                    vec4 worldPos   = model * vec4(aPosition, 1.0);
                                    FragPos         = worldPos.xyz;
                                    Normal          = normalize(normalMatrix * aNormal);
                                    gl_Position     = projection * view * worldPos;
                                }
";

        // Fragment shader
        private const string FragSrc = @"
                                #version 330 core

                                in  vec3 FragPos;
                                in  vec3 Normal;
                                out vec4 FragColor;

                                // Light properties
                                uniform vec3 lightDir;
                                uniform vec3 lightColor;
                                uniform vec3 viewPos;        // camera position for specular

                                // Material properties
                                uniform vec3  objectColor;
                                uniform float ambientStrength;
                                uniform float specularStrength;
                                uniform float shininess;
           
                                void main()
                                {
                                    // Flip normal for back faces so lighting computes correctly
                                    vec3 norm = gl_FrontFacing ? normalize(Normal) : -normalize(Normal);

                                    // Use a different color tint for inner (back) faces
                                    vec3 matColor = gl_FrontFacing ? objectColor : objectColor * vec3(1.3, 0.7, 0.6);

                                    // Ambient
                                    vec3 ambient = ambientStrength * lightColor;

                                    // Diffuse
                                    float diff    = max(dot(norm, lightDir), 0.0);
                                    vec3 diffuse  = diff * lightColor;

                                    // Specular (Blinn-Phong)
                                    vec3 viewDir    = normalize(viewPos - FragPos);
                                    vec3 halfwayDir = normalize(lightDir + viewDir);
                                    float spec      = pow(max(dot(norm, halfwayDir), 0.0), shininess);
                                    vec3 specular   = specularStrength * spec * lightColor;

                                    vec3 result = (ambient + diffuse + specular) * matColor;
                                    FragColor   = vec4(result, 1.0);
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

            normalMatrixLoc = GL.GetUniformLocation(shader, "normalMatrix");
            lightColorLoc = GL.GetUniformLocation(shader, "lightColor");
            viewPosLoc = GL.GetUniformLocation(shader, "viewPos");
            objectColorLoc = GL.GetUniformLocation(shader, "objectColor");
            ambientLoc = GL.GetUniformLocation(shader, "ambientStrength");
            specularLoc = GL.GetUniformLocation(shader, "specularStrength");
            shininessLoc = GL.GetUniformLocation(shader, "shininess");
            lightDirLoc = GL.GetUniformLocation(shader, "lightDir");
        }

        private void uploadMeshToGPU()
        {
            stlVertices.Clear();

            for (int i = 0; i < printModel.Mesh.glVertices.Length; i += 3)
            {   // [x y z nx ny nz]
                stlVertices.Add(printModel.Mesh.glVertices[i]);
                stlVertices.Add(printModel.Mesh.glVertices[i + 1]);
                stlVertices.Add(printModel.Mesh.glVertices[i + 2]);
                stlVertices.Add(printModel.Mesh.glNormals[i]);
                stlVertices.Add(printModel.Mesh.glNormals[i + 1]);
                stlVertices.Add(printModel.Mesh.glNormals[i + 2]);
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
            GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vsStatus);
            if (vsStatus == 0)
                throw new Exception("Vertex shader compile error: " + GL.GetShaderInfoLog(vertexShader));

            // Same as vertex shader
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, FragSrc);
            GL.CompileShader(fragmentShader);
            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fsStatus);
            if (fsStatus == 0)
                throw new Exception("Fragment shader compile error: " + GL.GetShaderInfoLog(fragmentShader));

            // Attach the shaders to the shader program
            GL.AttachShader(shaderProgram, vertexShader);
            GL.AttachShader(shaderProgram, fragmentShader);

            // Link the program to OpenGL
            GL.LinkProgram(shaderProgram);
            GL.GetProgram(shaderProgram, GetProgramParameterName.LinkStatus, out int linkStatus);
            if (linkStatus == 0)
                throw new Exception("Shader link error: " + GL.GetProgramInfoLog(shaderProgram));

            // delete the shaders
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            return shaderProgram;
        }
        public void Draw()
        {
            if (stlVertices.Count == 0) return;

            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace); // Draw both front and back faces
            Matrix4 model = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 proj = Matrix4.Identity;
            MainWindow.main.threeDCamera.GetModelViewProj(ref model, ref view, ref proj);
            Matrix3 normalMatrix = new Matrix3(Matrix4.Transpose(Matrix4.Invert(printModel.trans)));

            GL.UseProgram(shader);

            GL.UniformMatrix4(stlViewLoc, false, ref view);
            GL.UniformMatrix4(stlProjLoc, false, ref proj);
            GL.UniformMatrix3(normalMatrixLoc, false, ref normalMatrix);

            // --- Customizable light values ---
            Vector3 dir = Vector3.Normalize(LightDirection); // direction, not position, so scale doesn't matter.
            GL.Uniform3(lightDirLoc, dir);
            GL.Uniform3(lightColorLoc, LightColor); // Light Color
            GL.Uniform3(viewPosLoc, MainWindow.main.threeDCamera.CameraPosition); // camera pos

            GL.Uniform3(objectColorLoc, ModelColor); // Model Color
            GL.Uniform1(ambientLoc, MainWindow.main.threeDSettings.GetAmbientIntensity());      // Ambient intensity
            GL.Uniform1(specularLoc, MainWindow.main.threeDSettings.GetSpecularIntensity());    // Specular intensity
            GL.Uniform1(shininessLoc, MainWindow.main.threeDSettings.GetShininess());           // Shininess exponent

            GL.UniformMatrix4(stlModelLoc, false, ref printModel.trans); // set model matrix once, here
            GL.BindVertexArray(stlVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, stlVertices.Count / 6);
        }

        public Vector3 LightDirection
        {
            get
            {
                float[] dir = MainWindow.main.threeDSettings.LightDirection();
                Vector3 output = new Vector3(dir[0], dir[1], dir[2]);
                // Guard against zero vector — normalize would produce NaN
                return output.LengthSquared > 0f ? output : new Vector3(1f, 2f, 1f);
            }
        }
        public Vector3 LightColor
        {
            get
            {
                float[] colors = MainWindow.main.threeDSettings.LightColor();
                return new Vector3(colors[0], colors[1], colors[2]);
            }
        }
        public Vector3 ModelColor
        {
            get
            {
                float[] colors = MainWindow.main.threeDSettings.ModelColor();
                return new Vector3(colors[0], colors[1], colors[2]);
            }
        }
        public void Dispose()
        {
            GL.DeleteVertexArray(stlVao);
            GL.DeleteBuffer(stlVbo);
            GL.DeleteProgram(shader);
        }
    }
}