using OpenGL3DViewerNET10.ModelLib.model;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using View3D;

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
        int stlColorVbo;                        // Separate VBO for per-vertex colors from glColors
        int stlModelLoc, stlViewLoc, stlProjLoc;

        // Add these fields
        int lightDirLoc, lightColorLoc, viewPosLoc;
        int objectColorLoc, ambientLoc, specularLoc, shininessLoc;
        int normalMatrixLoc;
        int useVertexColorLoc;                  // Uniform: 1 = use glColors, 0 = use objectColor uniform

        // Vertex shader
        private const string VertSrc = @"
                                #version 330 core

                                layout(location=0) in vec3 aPosition;
                                layout(location=1) in vec3 aNormal;
                                layout(location=2) in vec3 aColor;      // Per-vertex color from glColors

                                uniform mat4 model;
                                uniform mat4 view;
                                uniform mat4 projection;
                                uniform mat3 normalMatrix; 

                                out vec3 FragPos;       // world-space position
                                out vec3 Normal;        // world-space normal
                                out vec3 VertexColor;   // passed through to fragment shader

                                void main()
                                {
                                    vec4 worldPos   = model * vec4(aPosition, 1.0);
                                    FragPos         = worldPos.xyz;
                                    Normal          = normalize(normalMatrix * aNormal);
                                    VertexColor     = aColor;
                                    gl_Position     = projection * view * worldPos;
                                }
";

        // Fragment shader
        private const string FragSrc = @"
                                #version 330 core

                                in  vec3 FragPos;
                                in  vec3 Normal;
                                in  vec3 VertexColor;   // interpolated per-vertex color
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

                                // Color source switch: 1 = use per-vertex color, 0 = use objectColor uniform
                                uniform int useVertexColor;
           
                                void main()
                                {
                                    // Flip normal for back faces so lighting computes correctly
                                    vec3 norm = gl_FrontFacing ? normalize(Normal) : -normalize(Normal);

                                    // Select base color: per-vertex glColors or the uniform objectColor
                                    vec3 baseColor = (useVertexColor == 1) ? VertexColor : objectColor;

                                    // Use a different color tint for inner (back) faces
                                    vec3 matColor = gl_FrontFacing ? baseColor : baseColor * vec3(1.3, 0.7, 0.6);

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

        // Call once after a file is loaded and ready.
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
            useVertexColorLoc = GL.GetUniformLocation(shader, "useVertexColor");
        }

        private void uploadMeshToGPU()
        {
            stlVao = GL.GenVertexArray();
            stlVbo = GL.GenBuffer();

            GL.BindVertexArray(stlVao);

            // --- VBO 0: positions + normals (layout location 0 and 1) ---
            GL.BindBuffer(BufferTarget.ArrayBuffer, stlVbo);
            GL.BufferData(
                BufferTarget.ArrayBuffer,
                printModel.Mesh.glVertices.Length * sizeof(float),
                printModel.Mesh.glVertices,
                BufferUsageHint.StaticDraw);

            // aPosition: location 0, 3 floats, stride 6 floats, offset 0
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // aNormal: location 1, 3 floats, stride 6 floats, offset 3 floats
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // --- VBO 1: per-vertex colors from glColors (layout location 2) ---
            // glColors is expected to be RGB floats: 3 floats per vertex, tightly packed.
            // If glColors is null or empty we still bind a VBO but leave it empty;
            // the useVertexColor uniform will be 0, so the GPU never reads it.
            bool hasColors = printModel.Mesh.glColors != null && printModel.Mesh.glColors.Length > 0;
            if (hasColors)
            {            
                stlColorVbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, stlColorVbo);

                GL.BufferData(
                    BufferTarget.ArrayBuffer,
                    printModel.Mesh.glColors.Length * sizeof(float),
                    printModel.Mesh.glColors,
                    BufferUsageHint.StaticDraw);


                // aColor: location 2, 3 floats (RGB), tightly packed (stride 0 = tightly packed)
                GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
                GL.EnableVertexAttribArray(2);
            }

            // Unbind VAO last to capture the state
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
            if (printModel.Mesh.glVertices == null) return;

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

            GL.Uniform3(objectColorLoc, ModelColor); // Fallback uniform color when no glColors present
            GL.Uniform1(ambientLoc, MainWindow.main.threeDSettings.GetAmbientIntensity());      // Ambient intensity
            GL.Uniform1(specularLoc, MainWindow.main.threeDSettings.GetSpecularIntensity());    // Specular intensity
            GL.Uniform1(shininessLoc, MainWindow.main.threeDSettings.GetShininess());           // Shininess exponent

            // Tell the shader whether to sample per-vertex colors or fall back to objectColor
            bool hasColors = printModel.Mesh.glColors != null && printModel.Mesh.glColors.Length > 0;
            GL.Uniform1(useVertexColorLoc, hasColors ? 1 : 0);

            GL.UniformMatrix4(stlModelLoc, false, ref printModel.trans); // set model matrix once, here
            GL.BindVertexArray(stlVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, printModel.Mesh.glVertices.Length / 6);
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
            GL.DeleteBuffer(stlColorVbo);   // Clean up the color VBO
            GL.DeleteProgram(shader);
        }
    }
}