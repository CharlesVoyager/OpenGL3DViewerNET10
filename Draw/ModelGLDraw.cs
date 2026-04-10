using OpenGL3DViewerNET10.ModelLib.model;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing;
using View3D;
using View3D.model.geom;

namespace OpenGL3DViewerNET10.Draw
{
    /*
    Load GLB File (CPU)
    ↓
    Parse Geometry + PBR Materials (vertices, normals, UVs, tangents, 5 texture maps)
    ↓
    Center the model
    ↓
    Fill Mesh (Submesh)
    ↓
    Upload to GPU (VBO / VAO — 5 separate VBOs at locations 0-4)
    ↓
    Render Loop — Cook-Torrance GGX PBR fragment shader
    */

    public class ModelGLDraw
    {
        PrintModel printModel;

        // ---- GPU object handles ----
        int shader;
        int vao;
        int vbo;                // location 0: positions
        int normalVbo;          // location 1: normals
        int colorVbo;           // location 2: per-vertex colors (glColors)
        int texCoordVbo;        // location 3: UV coords
        int tangentVbo;         // location 4: tangent vectors (vec4, w = handedness)

        // ---- Uniform locations ----
        int modelLoc, viewLoc, projLoc;
        int normalMatrixLoc;
        int viewPosLoc;
        int objectColorLoc;
        int useVertexColorLoc;

        // PBR texture samplers
        int baseColorTexLoc;
        int metallicRoughnessTexLoc;
        int normalMapLoc;
        int occlusionTexLoc;
        int emissiveTexLoc;

        // PBR scalar / flag uniforms
        int useBaseColorTexLoc;
        int useMetallicRoughnessTexLoc;
        int useNormalMapLoc;
        int useOcclusionTexLoc;
        int useEmissiveTexLoc;
        int metallicFactorLoc;
        int roughnessFactorLoc;
        int emissiveFactorLoc;
        int baseColorFactorLoc;

        // ---- OpenGL texture IDs (0 = not loaded) ----
        int baseColorTexId           = 0;
        int metallicRoughnessTexId   = 0;
        int normalMapId              = 0;
        int occlusionTexId           = 0;
        int emissiveTexId            = 0;

        // ---- PBR scalar factors (fallback when textures absent) ----
        float   metallicFactor       = 1f;
        float   roughnessFactor      = 1f;
        float[] emissiveFactor       = { 0f, 0f, 0f };
        float[] baseColorFactor      = { 1f, 1f, 1f, 1f };

        // =========================================================================
        // Vertex shader
        // Outputs:
        //   FragPos   — world-space position
        //   Normal    — world-space geometric normal
        //   VertexColor
        //   TexCoord
        //   TBN       — tangent-space → world-space matrix (for normal mapping)
        // =========================================================================
        private const string VertSrc = @"
#version 330 core

layout(location=0) in vec3 aPosition;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec3 aColor;
layout(location=3) in vec2 aTexCoord;
layout(location=4) in vec4 aTangent;   // xyz = tangent direction, w = handedness (±1)

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform mat3 normalMatrix;

out vec3 FragPos;
out vec3 Normal;
out vec3 VertexColor;
out vec2 TexCoord;
out mat3 TBN;

void main()
{
    vec4 worldPos = model * vec4(aPosition, 1.0);
    FragPos       = worldPos.xyz;
    Normal        = normalize(normalMatrix * aNormal);
    VertexColor   = aColor;
    TexCoord      = aTexCoord;

    // Build TBN matrix for tangent-space normal mapping
    vec3 T = normalize(normalMatrix * aTangent.xyz);
    vec3 N = Normal;
    T = normalize(T - dot(T, N) * N);          // re-orthogonalize (Gram-Schmidt)
    vec3 B = cross(N, T) * aTangent.w;          // w = handedness
    TBN = mat3(T, B, N);

    gl_Position = projection * view * worldPos;
}
";

        // =========================================================================
        // Fragment shader — Cook-Torrance GGX PBR
        //
        // Lighting rig (same three-point world-space directions as before):
        //   Key  : upper-front-left,  warm white
        //   Fill : lower-front-right, cool tint
        //   Back : behind-below,      rim
        //   + hemisphere ambient + optional ambient occlusion
        //
        // PBR maps (all optional — scalar factors are used as fallback):
        //   Texture0  baseColorTexture          (sRGB)
        //   Texture1  metallicRoughnessTexture  (linear: G=roughness, B=metallic)
        //   Texture2  normalMap                 (tangent-space, linear)
        //   Texture3  occlusionTexture          (linear: R=occlusion)
        //   Texture4  emissiveTexture           (sRGB)
        //
        // Output is gamma-corrected (linear -> sRGB, pow 1/2.2).
        // =========================================================================
        private const string FragSrc = @"
#version 330 core

in vec3 FragPos;
in vec3 Normal;
in vec3 VertexColor;
in vec2 TexCoord;
in mat3 TBN;

out vec4 FragColor;

// --- Camera ---
uniform vec3 viewPos;

// --- Fallback / non-PBR path ---
uniform vec3  objectColor;
uniform int   useVertexColor;   // 1 = VertexColor, 0 = objectColor

// --- PBR texture samplers ---
uniform sampler2D baseColorTexture;
uniform sampler2D metallicRoughnessTexture;
uniform sampler2D normalMap;
uniform sampler2D occlusionTexture;
uniform sampler2D emissiveTexture;

// --- PBR texture enable flags ---
uniform int useBaseColorTex;
uniform int useMetallicRoughnessTex;
uniform int useNormalMap;
uniform int useOcclusionTex;
uniform int useEmissiveTex;

// --- PBR scalar factors ---
uniform float metallicFactor;
uniform float roughnessFactor;
uniform vec3  emissiveFactor;
uniform vec4  baseColorFactor;

// --- Three-point studio rig ---
const vec3  keyDir   = normalize(vec3(-0.6,  1.0,  0.8));
const vec3  keyColor = vec3(1.00, 0.98, 0.95);
const float keyStr   = 1.8;

const vec3  fillDir   = normalize(vec3( 0.8,  0.3,  0.5));
const vec3  fillColor = vec3(0.80, 0.88, 1.00);
const float fillStr   = 1.2;

const vec3  backDir   = normalize(vec3( 0.1, -0.5, -1.0));
const vec3  backColor = vec3(0.90, 0.92, 1.00);
const float backStr   = 0.9;

// --- Hemisphere ambient ---
const vec3  skyColor    = vec3(0.60, 0.70, 0.90);
const vec3  groundColor = vec3(0.25, 0.20, 0.18);
const float ambientStr  = 0.22;

const float PI = 3.14159265358979;

// --- GGX Distribution (Trowbridge-Reitz) ---
float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a  = roughness * roughness;
    float a2 = a * a;
    float d  = max(dot(N, H), 0.0);
    float d2 = d * d;
    float denom = d2 * (a2 - 1.0) + 1.0;
    return a2 / (PI * denom * denom);
}

// --- Schlick-GGX Geometry sub-term ---
float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    return NdotV / (NdotV * (1.0 - k) + k);
}

// --- Smith combined geometry term ---
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    return GeometrySchlickGGX(NdotV, roughness) * GeometrySchlickGGX(NdotL, roughness);
}

// --- Fresnel-Schlick approximation ---
vec3 FresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// --- Cook-Torrance BRDF for one directional light ---
vec3 PbrDirectionalLight(
    vec3 N, vec3 V,
    vec3 lightDir, vec3 lightColor, float lightStrength,
    vec3 albedo, float metallic, float roughness, vec3 F0)
{
    vec3  L      = normalize(lightDir);
    vec3  H      = normalize(V + L);
    float NdotL  = max(dot(N, L), 0.0);
    if (NdotL <= 0.0) return vec3(0.0);

    float radiance = lightStrength;

    // Specular BRDF
    float D  = DistributionGGX(N, H, roughness);
    float G  = GeometrySmith(N, V, L, roughness);
    vec3  F  = FresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 numerator   = D * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * NdotL + 0.0001;
    vec3 specular    = numerator / denominator;

    // Diffuse (energy-conserving: metals have no diffuse)
    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - metallic);
    vec3 diffuse = kD * albedo / PI;

    return (diffuse + specular) * lightColor * radiance * NdotL;
}

void main()
{
    // --- Normal ---
    vec3 N;
    if (useNormalMap == 1)
    {
        vec3 tn = texture(normalMap, TexCoord).rgb * 2.0 - 1.0;
        N = normalize(TBN * tn);
    }
    else
    {
        N = gl_FrontFacing ? normalize(Normal) : -normalize(Normal);
    }

    // --- Albedo (base color) ---
    vec3 albedo;
    if (useBaseColorTex == 1)
    {
        // sRGB texture -> linear
        vec3 srgb = texture(baseColorTexture, TexCoord).rgb;
        albedo = pow(srgb, vec3(2.2)) * baseColorFactor.rgb;
    }
    else if (useVertexColor == 1)
    {
        albedo = VertexColor;
    }
    else
    {
        albedo = objectColor * baseColorFactor.rgb;
    }

    // --- Metallic & Roughness ---
    float metallic, roughness;
    if (useMetallicRoughnessTex == 1)
    {
        vec2 mr = texture(metallicRoughnessTexture, TexCoord).bg; // B=metallic, G=roughness
        metallic  = mr.x * metallicFactor;
        roughness = mr.y * roughnessFactor;
    }
    else
    {
        metallic  = metallicFactor;
        roughness = roughnessFactor;
    }
    roughness = clamp(roughness, 0.04, 1.0);
    metallic  = clamp(metallic,  0.0,  1.0);

    // --- Ambient Occlusion ---
    float ao = 1.0;
    if (useOcclusionTex == 1)
        ao = texture(occlusionTexture, TexCoord).r;

    // --- Emissive ---
    vec3 emissive = vec3(0.0);
    if (useEmissiveTex == 1)
        emissive = pow(texture(emissiveTexture, TexCoord).rgb, vec3(2.2)) * emissiveFactor;
    else
        emissive = emissiveFactor;

    // F0: surface reflectance at zero incidence 
    // Dielectrics: ~0.04; metals: tinted by albedo
    vec3 F0 = mix(vec3(0.04), albedo, metallic);

    vec3 V = normalize(viewPos - FragPos);

    // --- Three-point PBR lighting ---
    vec3 Lo = vec3(0.0);
    Lo += PbrDirectionalLight(N, V, keyDir,  keyColor,  keyStr,  albedo, metallic, roughness, F0);
    Lo += PbrDirectionalLight(N, V, fillDir, fillColor, fillStr, albedo, metallic, roughness, F0);
    Lo += PbrDirectionalLight(N, V, backDir, backColor, backStr, albedo, metallic, roughness, F0);

    // --- Hemisphere ambient (approximates image-based lighting) ---
    float hemi    = 0.5 + 0.5 * N.y;
    vec3  ambient = mix(groundColor, skyColor, hemi) * ambientStr * albedo * ao;

    // --- Back-face tint (kept subtle) ---
    if (!gl_FrontFacing)
        ambient *= vec3(0.60, 0.70, 0.80);

    // --- Combine ---
    vec3 color = ambient + Lo + emissive;

    // --- Gamma correction (linear -> sRGB) ---
    color = pow(clamp(color, 0.0, 1.0), vec3(1.0 / 2.2));

    FragColor = vec4(color, 1.0);
}
";

        public ModelGLDraw(PrintModel model)
        {
            printModel = model;
        }

        // -------------------------------------------------------------------------
        // Init — call once after the file is loaded and the GL context is current.
        // -------------------------------------------------------------------------
        public void Init()
        {
            shader = CreateShaderProgram();
            UploadMeshToGPU();
            CacheUniformLocations();
        }

        private void CacheUniformLocations()
        {
            modelLoc        = GL.GetUniformLocation(shader, "model");
            viewLoc         = GL.GetUniformLocation(shader, "view");
            projLoc         = GL.GetUniformLocation(shader, "projection");
            normalMatrixLoc = GL.GetUniformLocation(shader, "normalMatrix");
            viewPosLoc      = GL.GetUniformLocation(shader, "viewPos");
            objectColorLoc  = GL.GetUniformLocation(shader, "objectColor");
            useVertexColorLoc = GL.GetUniformLocation(shader, "useVertexColor");

            // PBR sampler slots
            baseColorTexLoc          = GL.GetUniformLocation(shader, "baseColorTexture");
            metallicRoughnessTexLoc  = GL.GetUniformLocation(shader, "metallicRoughnessTexture");
            normalMapLoc             = GL.GetUniformLocation(shader, "normalMap");
            occlusionTexLoc          = GL.GetUniformLocation(shader, "occlusionTexture");
            emissiveTexLoc           = GL.GetUniformLocation(shader, "emissiveTexture");

            // PBR enable flags
            useBaseColorTexLoc          = GL.GetUniformLocation(shader, "useBaseColorTex");
            useMetallicRoughnessTexLoc  = GL.GetUniformLocation(shader, "useMetallicRoughnessTex");
            useNormalMapLoc             = GL.GetUniformLocation(shader, "useNormalMap");
            useOcclusionTexLoc          = GL.GetUniformLocation(shader, "useOcclusionTex");
            useEmissiveTexLoc           = GL.GetUniformLocation(shader, "useEmissiveTex");

            // PBR scalar uniforms
            metallicFactorLoc   = GL.GetUniformLocation(shader, "metallicFactor");
            roughnessFactorLoc  = GL.GetUniformLocation(shader, "roughnessFactor");
            emissiveFactorLoc   = GL.GetUniformLocation(shader, "emissiveFactor");
            baseColorFactorLoc  = GL.GetUniformLocation(shader, "baseColorFactor");
        }

        // -------------------------------------------------------------------------
        // Upload all vertex data and PBR textures to the GPU.
        // -------------------------------------------------------------------------
        private void UploadMeshToGPU()
        {
            vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);

            // --- VBO 0: positions (location 0) ---
            vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer,
                printModel.Mesh.glVertices.Length * sizeof(float),
                printModel.Mesh.glVertices,
                BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // --- VBO 1: normals (location 1) ---
            normalVbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, normalVbo);
            GL.BufferData(BufferTarget.ArrayBuffer,
                printModel.Mesh.glNormals.Length * sizeof(float),
                printModel.Mesh.glNormals,
                BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);

            // --- VBO 2: per-vertex colors (location 2, optional) ---
            bool hasColors = printModel.Mesh.glColors != null && printModel.Mesh.glColors.Length > 0;
            if (hasColors)
            {
                colorVbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, colorVbo);
                GL.BufferData(BufferTarget.ArrayBuffer,
                    printModel.Mesh.glColors.Length * sizeof(float),
                    printModel.Mesh.glColors,
                    BufferUsageHint.StaticDraw);
                GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
                GL.EnableVertexAttribArray(2);
            }

            // --- VBO 3: UV texture coordinates (location 3, optional) ---
            bool hasUV = printModel.Model.texCoords.Count > 0;
            if (hasUV)
            {
                float[] uvArray = printModel.Model.texCoords.ToArray();
                texCoordVbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, texCoordVbo);
                GL.BufferData(BufferTarget.ArrayBuffer,
                    uvArray.Length * sizeof(float),
                    uvArray,
                    BufferUsageHint.StaticDraw);
                GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
                GL.EnableVertexAttribArray(3);
            }

            // --- VBO 4: tangents (location 4, optional — vec4, w = handedness) ---
            bool hasTangents = printModel.Model.tangents.Count > 0;
            if (hasTangents)
            {
                float[] tanArray = printModel.Model.tangents.ToArray();
                tangentVbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, tangentVbo);
                GL.BufferData(BufferTarget.ArrayBuffer,
                    tanArray.Length * sizeof(float),
                    tanArray,
                    BufferUsageHint.StaticDraw);
                GL.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
                GL.EnableVertexAttribArray(4);
            }

            GL.BindVertexArray(0);

            // --- Load PBR textures and scalar factors from the first material ---
            if (printModel.Model.materials.Count > 0)
            {
                var mat = printModel.Model.materials[0];

                baseColorTexId          = LoadTexture(mat.BaseColorTexture);
                metallicRoughnessTexId  = LoadTexture(mat.MetallicRoughnessTexture);
                normalMapId             = LoadTexture(mat.NormalTexture);
                occlusionTexId          = LoadTexture(mat.OcclusionTexture);
                emissiveTexId           = LoadTexture(mat.EmissiveTexture);

                metallicFactor   = mat.MetallicFactor;
                roughnessFactor  = mat.RoughnessFactor;
                emissiveFactor   = mat.EmissiveFactor;
                baseColorFactor  = mat.BaseColorFactor;
            }
        }

        // -------------------------------------------------------------------------
        // Draw
        // -------------------------------------------------------------------------
        public void Draw()
        {
            if (printModel.Mesh.glVertices == null) return;

            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace); // render both faces

            Matrix4 model = Matrix4.Identity;
            Matrix4 view  = Matrix4.Identity;
            Matrix4 proj  = Matrix4.Identity;
            MainWindow.main.threeDCamera.GetModelViewProj(ref model, ref view, ref proj);

            Matrix3 normalMatrix = new Matrix3(Matrix4.Transpose(Matrix4.Invert(printModel.trans)));

            GL.UseProgram(shader);

            // ---- Transforms ----
            GL.UniformMatrix4(viewLoc,         false, ref view);
            GL.UniformMatrix4(projLoc,         false, ref proj);
            GL.UniformMatrix4(modelLoc,        false, ref printModel.trans);
            GL.UniformMatrix3(normalMatrixLoc, false, ref normalMatrix);

            // ---- Camera ----
            GL.Uniform3(viewPosLoc, MainWindow.main.threeDCamera.CameraPosition);

            // ---- Fallback color (STL grey or user setting) ----
            GL.Uniform3(objectColorLoc, ModelColor);

            // ---- Vertex color flag ----
            bool hasColors = printModel.Mesh.glColors != null && printModel.Mesh.glColors.Length > 0;
            GL.Uniform1(useVertexColorLoc, hasColors ? 1 : 0);

            // ---- PBR scalar factors ----
            GL.Uniform1(metallicFactorLoc,  metallicFactor);
            GL.Uniform1(roughnessFactorLoc, roughnessFactor);
            GL.Uniform3(emissiveFactorLoc,  emissiveFactor[0], emissiveFactor[1], emissiveFactor[2]);
            GL.Uniform4(baseColorFactorLoc, baseColorFactor[0], baseColorFactor[1],
                                            baseColorFactor[2], baseColorFactor[3]);

            // ---- Bind all 5 PBR texture units ----
            BindTexture(TextureUnit.Texture0, baseColorTexId,         baseColorTexLoc,         useBaseColorTexLoc);
            BindTexture(TextureUnit.Texture1, metallicRoughnessTexId, metallicRoughnessTexLoc, useMetallicRoughnessTexLoc);
            BindTexture(TextureUnit.Texture2, normalMapId,            normalMapLoc,            useNormalMapLoc);
            BindTexture(TextureUnit.Texture3, occlusionTexId,         occlusionTexLoc,         useOcclusionTexLoc);
            BindTexture(TextureUnit.Texture4, emissiveTexId,          emissiveTexLoc,          useEmissiveTexLoc);

            // ---- Draw ----
            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, printModel.Mesh.glVertices.Length / 3);
        }

        /// <summary>
        /// Activates <paramref name="unit"/>, binds the texture, and sets the
        /// sampler uniform + enable-flag uniform in one call.
        /// When texId == 0 the flag is set to 0 so the shader uses scalar fallbacks.
        /// </summary>
        private void BindTexture(TextureUnit unit, int texId, int samplerLoc, int enableFlagLoc)
        {
            GL.ActiveTexture(unit);
            int unitIndex = unit - TextureUnit.Texture0;

            if (texId != 0)
            {
                GL.BindTexture(TextureTarget.Texture2D, texId);
                GL.Uniform1(samplerLoc,    unitIndex);
                GL.Uniform1(enableFlagLoc, 1);
            }
            else
            {
                GL.BindTexture(TextureTarget.Texture2D, 0);
                GL.Uniform1(enableFlagLoc, 0);
            }
        }

        // -------------------------------------------------------------------------
        // Fallback model color (used when no PBR base-color texture is present)
        // -------------------------------------------------------------------------
        public Vector3 ModelColor
        {
            get
            {
                float[] c = MainWindow.main.threeDSettings.ModelColor();
                return new Vector3(c[0], c[1], c[2]);
            }
        }

        // -------------------------------------------------------------------------
        // Upload one Bitmap to an OpenGL texture.
        // Returns 0 if bitmap is null (caller can safely pass null for absent maps).
        // -------------------------------------------------------------------------
        private int LoadTexture(Bitmap? bitmap)
        {
            if (bitmap == null) return 0;

            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);

            var data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexImage2D(
                TextureTarget.Texture2D, 0,
                PixelInternalFormat.Rgba,
                bitmap.Width, bitmap.Height, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte,
                data.Scan0);

            bitmap.UnlockBits(data);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,     (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,     (int)TextureWrapMode.Repeat);

            return tex;
        }

        // -------------------------------------------------------------------------
        // Shader compilation
        // -------------------------------------------------------------------------
        private int CreateShaderProgram()
        {
            int vs = CompileShader(ShaderType.VertexShader,   VertSrc);
            int fs = CompileShader(ShaderType.FragmentShader, FragSrc);

            int prog = GL.CreateProgram();
            GL.AttachShader(prog, vs);
            GL.AttachShader(prog, fs);
            GL.LinkProgram(prog);
            GL.GetProgram(prog, GetProgramParameterName.LinkStatus, out int linkStatus);
            if (linkStatus == 0)
                throw new Exception("Shader link error: " + GL.GetProgramInfoLog(prog));

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
            return prog;
        }

        private static int CompileShader(ShaderType type, string src)
        {
            int s = GL.CreateShader(type);
            GL.ShaderSource(s, src);
            GL.CompileShader(s);
            GL.GetShader(s, ShaderParameter.CompileStatus, out int status);
            if (status == 0)
                throw new Exception($"{type} compile error: " + GL.GetShaderInfoLog(s));
            return s;
        }

        // -------------------------------------------------------------------------
        // Dispose — release every GPU resource (no leaks)
        // -------------------------------------------------------------------------
        public void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
            GL.DeleteBuffer(normalVbo);
            if (colorVbo    != 0) GL.DeleteBuffer(colorVbo);
            if (texCoordVbo != 0) GL.DeleteBuffer(texCoordVbo);
            if (tangentVbo  != 0) GL.DeleteBuffer(tangentVbo);

            DeleteTexture(baseColorTexId);
            DeleteTexture(metallicRoughnessTexId);
            DeleteTexture(normalMapId);
            DeleteTexture(occlusionTexId);
            DeleteTexture(emissiveTexId);

            GL.DeleteProgram(shader);
        }

        private static void DeleteTexture(int texId)
        {
            if (texId != 0) GL.DeleteTexture(texId);
        }
    }
}
