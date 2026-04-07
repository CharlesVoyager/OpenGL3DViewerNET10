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
        int vao;
        int vbo;
        int colorVbo;                        // Separate VBO for per-vertex colors from glColors
        int modelLoc, viewLoc, projLoc;

        // Lighting / material uniforms
        int viewPosLoc;
        int objectColorLoc;
        int normalMatrixLoc;
        int useVertexColorLoc;              // Uniform: 1 = use glColors, 0 = use objectColor uniform

        // -------------------------------------------------------------------------
        // Vertex shader
        // -------------------------------------------------------------------------
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
out vec3 Normal;        // world-space normal (already transformed)
out vec3 VertexColor;   // passed through to fragment shader

void main()
{
    vec4 worldPos = model * vec4(aPosition, 1.0);
    FragPos       = worldPos.xyz;
    Normal        = normalize(normalMatrix * aNormal);
    VertexColor   = aColor;
    gl_Position   = projection * view * worldPos;
}
";

        // -------------------------------------------------------------------------
        // Fragment shader — 3dviewer.net studio lighting
        //
        // Reproduces the visual look of online3dviewer.net:
        //   • Three-point directional rig  (key, fill, back/rim)
        //   • Hemisphere ambient           (warm sky, cool ground)
        //   • Blinn-Phong specular         (tight, low-intensity)
        //   • Cool inner-face tint         (subtle, not the harsh red tint)
        //   • sRGB gamma correction        (pow 1/2.2) at the very end
        // -------------------------------------------------------------------------
        private const string FragSrc = @"
#version 330 core

in  vec3 FragPos;
in  vec3 Normal;
in  vec3 VertexColor;
out vec4 FragColor;

uniform vec3 viewPos;       // camera world position
uniform vec3 objectColor;   // fallback color when no per-vertex colors
uniform int  useVertexColor; // 1 = sample VertexColor, 0 = use objectColor

// ---- Three-point studio rig (fixed, viewer-space directions) ----
// Directions are given in world space.
// Key light: upper-front-left
const vec3 keyDir   = normalize(vec3(-0.6,  1.0,  0.8));
const vec3 keyColor = vec3(1.00, 0.98, 0.95);   // warm white
const float keyStr  = 0.75;

// Fill light: lower-front-right (softer, slightly cool)
const vec3 fillDir   = normalize(vec3( 0.8,  0.3,  0.5));
const vec3 fillColor = vec3(0.80, 0.88, 1.00);  // cool tint
const float fillStr  = 0.35;

// Back / rim light: from behind-below (adds depth separation)
const vec3 backDir   = normalize(vec3( 0.1, -0.5, -1.0));
const vec3 backColor = vec3(0.90, 0.92, 1.00);
const float backStr  = 0.25;

// ---- Hemisphere ambient ----
const vec3 skyColor    = vec3(0.60, 0.70, 0.90);   // soft blue sky
const vec3 groundColor = vec3(0.25, 0.20, 0.18);   // warm brown ground
const float ambientStr = 0.30;

// ---- Specular ----
const float specularStr = 0.10;
const float shininess   = 64.0;

// Compute a single Blinn-Phong directional light contribution.
vec3 DirectionalLight(vec3 norm, vec3 viewDir,
                      vec3 lightDir, vec3 lightColor, float strength)
{
    float diff   = max(dot(norm, lightDir), 0.0);
    vec3 halfway = normalize(lightDir + viewDir);
    float spec   = pow(max(dot(norm, halfway), 0.0), shininess);

    vec3 diffuse  = diff  * lightColor * strength;
    vec3 specular = spec  * lightColor * specularStr;
    return diffuse + specular;
}

void main()
{
    // Flip normal for back faces so lighting works on both sides.
    vec3 norm = gl_FrontFacing ? normalize(Normal) : -normalize(Normal);

    // Base material color
    vec3 baseColor = (useVertexColor == 1) ? VertexColor : objectColor;

    // Slight tint for back faces
    vec3 matColor = gl_FrontFacing ? baseColor : baseColor * vec3(0.60, 0.70, 0.80);

    // Hemisphere ambient
    float hemi    = 0.5 + 0.5 * norm.y;
    vec3  ambient = mix(groundColor, skyColor, hemi) * ambientStr;

    // Lighting
    vec3 viewDir = normalize(viewPos - FragPos);
    vec3 lighting = vec3(0.0);
    lighting += DirectionalLight(norm, viewDir, keyDir,  keyColor,  keyStr);
    lighting += DirectionalLight(norm, viewDir, fillDir, fillColor, fillStr);
    lighting += DirectionalLight(norm, viewDir, backDir, backColor, backStr);

    // Combine
    vec3 linear = (ambient + lighting) * matColor;

    // Gamma correction (linear -> sRGB)
    vec3 gammaCorrected = pow(clamp(linear, 0.0, 1.0), vec3(1.0 / 2.2));

    FragColor = vec4(gammaCorrected, 1.0);
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

            modelLoc = GL.GetUniformLocation(shader, "model");
            viewLoc  = GL.GetUniformLocation(shader, "view");
            projLoc  = GL.GetUniformLocation(shader, "projection");

            normalMatrixLoc  = GL.GetUniformLocation(shader, "normalMatrix");
            viewPosLoc       = GL.GetUniformLocation(shader, "viewPos");
            objectColorLoc   = GL.GetUniformLocation(shader, "objectColor");
            useVertexColorLoc = GL.GetUniformLocation(shader, "useVertexColor");
        }

        private void uploadMeshToGPU()
        {
            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();

            GL.BindVertexArray(vao);

            // --- VBO 0: positions + normals (layout location 0 and 1) ---
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
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
                colorVbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, colorVbo);

                GL.BufferData(
                    BufferTarget.ArrayBuffer,
                    printModel.Mesh.glColors.Length * sizeof(float),
                    printModel.Mesh.glColors,
                    BufferUsageHint.StaticDraw);

                // aColor: location 2, 3 floats (RGB), tightly packed (stride 0 = tightly packed)
                GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
                GL.EnableVertexAttribArray(2);
            }
            GL.BindVertexArray(0);
        }

        int createShaderProgram()
        {
            int shaderProgram = GL.CreateProgram();

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, VertSrc);
            GL.CompileShader(vertexShader);
            GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vsStatus);
            if (vsStatus == 0)
                throw new Exception("Vertex shader compile error: " + GL.GetShaderInfoLog(vertexShader));

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, FragSrc);
            GL.CompileShader(fragmentShader);
            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fsStatus);
            if (fsStatus == 0)
                throw new Exception("Fragment shader compile error: " + GL.GetShaderInfoLog(fragmentShader));

            GL.AttachShader(shaderProgram, vertexShader);
            GL.AttachShader(shaderProgram, fragmentShader);

            GL.LinkProgram(shaderProgram);
            GL.GetProgram(shaderProgram, GetProgramParameterName.LinkStatus, out int linkStatus);
            if (linkStatus == 0)
                throw new Exception("Shader link error: " + GL.GetProgramInfoLog(shaderProgram));

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
            Matrix4 view  = Matrix4.Identity;
            Matrix4 proj  = Matrix4.Identity;
            MainWindow.main.threeDCamera.GetModelViewProj(ref model, ref view, ref proj);

            // Normal matrix: inverse-transpose of the model matrix (handles non-uniform scale)
            Matrix3 normalMatrix = new Matrix3(Matrix4.Transpose(Matrix4.Invert(printModel.trans)));

            GL.UseProgram(shader);

            GL.UniformMatrix4(viewLoc,  false, ref view);
            GL.UniformMatrix4(projLoc,  false, ref proj);
            GL.UniformMatrix3(normalMatrixLoc, false, ref normalMatrix);

            // Camera position (used for specular view vector)
            GL.Uniform3(viewPosLoc, MainWindow.main.threeDCamera.CameraPosition);

            // Material / object colour  (fallback when no per-vertex colours)
            GL.Uniform3(objectColorLoc, ModelColor);

            // Tell the shader whether to sample per-vertex colors or fall back to objectColor
            bool hasColors = printModel.Mesh.glColors != null && printModel.Mesh.glColors.Length > 0;
            GL.Uniform1(useVertexColorLoc, hasColors ? 1 : 0);

            GL.UniformMatrix4(modelLoc, false, ref printModel.trans);
            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, printModel.Mesh.glVertices.Length / 6);
        }

        // ModelColor is still used as the base surface colour (e.g., the STL grey).
        // The three-point rig and hemisphere ambient are baked into the shader constants,
        // matching the fixed studio environment used by online3dviewer.net.
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
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
            GL.DeleteBuffer(colorVbo);
            GL.DeleteProgram(shader);
        }
    }
}
