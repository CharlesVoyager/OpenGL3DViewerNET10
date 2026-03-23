using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using View3D;
using View3D.model;

namespace OpenGL3DViewerNET10.Draw
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

        // Add these fields
        int lightPosLoc, lightColorLoc, viewPosLoc;
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
                                uniform vec3  lightPos;       // world-space position
                                uniform vec3  lightColor;
                                uniform vec3  viewPos;        // camera position for specular

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
                                    vec3 lightDir = normalize(lightPos - FragPos);
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
            lightPosLoc = GL.GetUniformLocation(shader, "lightPos");
            lightColorLoc = GL.GetUniformLocation(shader, "lightColor");
            viewPosLoc = GL.GetUniformLocation(shader, "viewPos");
            objectColorLoc = GL.GetUniformLocation(shader, "objectColor");
            ambientLoc = GL.GetUniformLocation(shader, "ambientStrength");
            specularLoc = GL.GetUniformLocation(shader, "specularStrength");
            shininessLoc = GL.GetUniformLocation(shader, "shininess");
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
                stlVertices.Add(printModel.submesh.glNormals[i + 1]);
                stlVertices.Add(printModel.submesh.glNormals[i + 2]);
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

            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace); // Draw both front and back faces

            Matrix4 model = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 proj = Matrix4.Identity;
            MainWindow.main.threeDCamera.GetModelViewProj(ref model, ref view, ref proj);

            // Normal matrix = transpose of inverse of model's 3x3
            Matrix3 normalMatrix = new Matrix3(Matrix4.Transpose(Matrix4.Invert(model)));

            GL.UseProgram(shader);

            GL.UniformMatrix4(stlModelLoc, false, ref model);
            GL.UniformMatrix4(stlViewLoc, false, ref view);
            GL.UniformMatrix4(stlProjLoc, false, ref proj);
            GL.UniformMatrix3(normalMatrixLoc, false, ref normalMatrix);

            // --- Customizable light values ---
            GL.Uniform3(lightPosLoc, new Vector3(100f, 200f, 300f)); // light position
            GL.Uniform3(lightColorLoc, LightColor); // Light Color, Default: 1.0f, 1.0f, 1.0f
            GL.Uniform3(viewPosLoc, MainWindow.main.threeDCamera.CameraPosition); // camera pos

            GL.Uniform3(objectColorLoc, ModelColor); // Model Color: Default: 0.6f, 0.7f, 0.8f
            GL.Uniform1(ambientLoc, 0.15f);     // ambient intensity
            GL.Uniform1(specularLoc, 0.5f);     // specular intensity
            GL.Uniform1(shininessLoc, 32.0f);   // shininess exponent

            GL.UniformMatrix4(stlModelLoc, false, ref printModel.trans);
            GL.BindVertexArray(stlVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, stlVertices.Count / 6);
        }

        // In your settings class, add properties like:
        public Vector3 LightPosition { get; set; } = new Vector3(100, 200, 300);
        public Vector3 LightColor
        {
            get
            {
                float[] colors = MainWindow.main.threeDSettings.LightColor();
                Vector3 outputColor = new Vector3();
                outputColor.X = colors[0];
                outputColor.Y = colors[1];
                outputColor.Z = colors[2];

                return outputColor;
            }
        } 
        public Vector3 ModelColor
        {
            get
            {
                float[] colors = MainWindow.main.threeDSettings.ModelColor();
                Vector3 outputColor = new Vector3();
                outputColor.X = colors[0];
                outputColor.Y = colors[1];
                outputColor.Z = colors[2];

                return outputColor;
            }
        }
        public float AmbientStrength { get; set; } = 0.15f;

        public void Dispose()
        {
            GL.DeleteVertexArray(stlVao);
            GL.DeleteBuffer(stlVbo);
            GL.DeleteProgram(shader);
        }
    }
}