using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using View3D.model;
using View3D.ModelObjectTool;
using View3D.view.utils;

namespace View3D.view
{
    /// <summary>
    /// OpenTK GameWindow replacing the WinForms UserControl + RHOpenGL child.
    /// Rendering, input, and camera logic are unchanged; only the hosting mechanism differs.
    /// </summary>
    public class ThreeDControl : GameWindow
    {
        // background shader & geometry
        int backgroundShader;

        // Printer bed shader & geometry
        int plateShader;

        int plateModelLoc, plateViewLoc, plateProjLoc;

        int plateColorLoc;
        int gridColorLoc;
        int gridSpacingLoc;

        int plateVao;
        int plateVbo;

        // STL model
        int stlShader;
        int stlVao;
        int stlVbo;
        int stlModelLoc, stlViewLoc, stlProjLoc;
        List<float> stlVertices = new List<float>();

        // Bounding Box
        int bboxShader;
        int bboxVao;
        int bboxVbo;
        int bboxModelLoc, bboxViewLoc, bboxProjLoc;


        // ── Public static / shared ────────────────────────────────────────────
        public static double GLversion;

        bool loaded = false;
        float xDown, yDown;
        float xPos, yPos;
        float speedX, speedY;
        float lastX, lastY;
        readonly Stopwatch fpsTimer = new Stopwatch();
        int mode = 0;
        public float zoom = 1.0f;

        int keyX = -1;
        int keyY = -1;

        STLComposer stlComp = null;
        ThreeDCamera threeDCam = null;

        // Geometry helpers (pick ray)
        public Geom3DLine pickLine = null;
        public Geom3DVector pickPoint = new Geom3DVector(0, 0, 0);

        // Object-move tracking
        Geom3DPlane movePlane = new Geom3DPlane(new Geom3DVector(0, 0, 0), new Geom3DVector(0, 0, 1));
        Geom3DVector moveStart = new Geom3DVector(0, 0, 0);
        Geom3DVector moveLast = new Geom3DVector(0, 0, 0);
        Geom3DVector movePos = new Geom3DVector(0, 0, 0);

        // ── Constructor ───────────────────────────────────────────────────────
        /// <summary>
        /// Creates the GameWindow with an OpenGL 2.x compatibility context.
        /// Pass width/height matching your panel or leave as defaults; the window
        /// is later embedded via WindowsFormsHost in MainWindow.xaml.
        /// </summary>
        /// 
        private const int MinWidth = 830;
        private const int MinHeight = 700;
        public ThreeDControl(int width = MinWidth, int height = MinHeight)
            : base(
                GameWindowSettings.Default,
                new NativeWindowSettings
                {
                    ClientSize = new Vector2i(width, height),
                    Title = "OpenGL 3D Viewer (OpenGL 4.9.4 + .NET 10.0)",
                    API = ContextAPI.OpenGL,
                    APIVersion = new Version(4, 0),
                    RedBits = 8,
                    GreenBits = 8,
                    BlueBits = 8,
                    AlphaBits = 8,
                    DepthBits = 24,
                    StencilBits = 8,
                    NumberOfSamples = 4,
                    Flags = ContextFlags.Default
                })
        {
            VSync = VSyncMode.Off;  // CHANGED: VSync is now a property on the window, not an enum field



            // Language hook
            MainWindow.main.languageChanged += translate;
        }

        // ── Public wiring ─────────────────────────────────────────────────────
        public void SetComp(STLComposer comp) => stlComp = comp;
        public void SetCamera(ThreeDCamera cam) => threeDCam = cam;

        private volatile bool _isDirty = true;
        private void Invalidate() => _isDirty = true;

        public void UpdateChanges() => Invalidate();

        public void SetObjectSelected(bool sel)
        {
            MainWindow.main.setbuttonVisable(stlComp.listObjects.SelectedItems.Count == 1 && sel);
        }

        // ── Translations ──────────────────────────────────────────────────────
        private void translate()
        {
            // These string keys mirror the original WinForms menu items.
            // Apply them to the WPF ContextMenu items exposed by ui if needed.
        }


        private const int WM_GETMINMAXINFO = 0x0024;
        private const int GWLP_WNDPROC = -4;

        private delegate IntPtr WndProcDelegate(
            IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // Keep a reference — prevents the delegate from being GC'd
        private WndProcDelegate _wndProcDelegate;
        private IntPtr _originalWndProc;

        [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);
        [DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved, ptMaxSize, ptMaxPosition,
                         ptMinTrackSize, ptMaxTrackSize;
        }

        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(
                    lParam, typeof(MINMAXINFO));

                mmi.ptMinTrackSize.x = MinWidth;
                mmi.ptMinTrackSize.y = MinHeight;

                Marshal.StructureToPtr(mmi, lParam, false);
                return IntPtr.Zero;
            }

            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }


        // ── GameWindow overrides ──────────────────────────────────────────────
        protected override void OnLoad()
        {
            base.OnLoad();

            // Subclass the native window to intercept Win32 messages
            _wndProcDelegate = CustomWndProc;
            IntPtr hwnd;
            unsafe { hwnd = GLFW.GetWin32Window(WindowPtr); }
            _originalWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

            MainWindow.main.Dispatcher.InvokeAsync(() =>
            {
                WindowInteropHelper helper = new WindowInteropHelper(MainWindow.main);
                helper.Owner = hwnd;
                MainWindow.main.Show();

                // CHANGED: Location / Size are now Vector2i in OpenTK 4
                MainWindow.main.UpdateLocation(Location.X, Location.Y);
                MainWindow.main.UpdateSize(ClientSize.X, ClientSize.Y);
            });

            // Detect OpenGL version & capabilities (runs once)
            try
            {
                string sv = GL.GetString(StringName.Version).Trim();
                int p = sv.IndexOf(' ');
                if (p > 0) sv = sv.Substring(0, p);
                p = sv.IndexOf('.');
                if (p > 0)
                {
                    p = sv.IndexOf('.', p + 1);
                    if (p > 0) sv = sv.Substring(0, p);
                    GLversion = Convert.ToDouble(sv, CultureInfo.InvariantCulture);
                }
                try
                {
                    float val;
                    float.TryParse(sv, NumberStyles.Float, GCode.format, out val);
                    MainWindow.main.threeDSettings.openGLVersion = val;
                }
                catch { MainWindow.main.threeDSettings.openGLVersion = 1.1f; }

                MainWindow.main.threeDSettings.useVBOs =
                    GL.GetString(StringName.Extensions).Contains("GL_ARB_vertex_buffer_object");
            }
            catch { }

            // Background
            backgroundShader = CreateShaderProgram("Background");

#if false   // Mono background clear color was too dark; using shader gradient instead
            GL.ClearColor(0.2f, 0.3f, 0.4f, 1f);
#endif

            // Printer bed
            plateShader = CreateShaderProgram("Printerbed");
            GetUniformLocationsPrinterbed();
            CreateBuildPlatePrinterbed();

            // STL model
            stlShader = CreateShaderProgram("StlModel");
            UploadMeshToGPU();
            stlModelLoc = GL.GetUniformLocation(stlShader, "model");
            stlViewLoc = GL.GetUniformLocation(stlShader, "view");
            stlProjLoc = GL.GetUniformLocation(stlShader, "projection");

            // Bounding Box
            bboxShader = CreateShaderProgram("BoundingBox");   
            SetupBBox();
            bboxModelLoc = GL.GetUniformLocation(bboxShader, "model");
            bboxViewLoc = GL.GetUniformLocation(bboxShader, "view");
            bboxProjLoc = GL.GetUniformLocation(bboxShader, "projection");

            loaded = true;
        }

        int CreateShaderProgram(string shaderFilename) 
        {
            // create the shader program
            int shaderProgram = GL.CreateProgram();

            // create the vertex shader
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, LoadShaderSource(shaderFilename + ".vert"));
            GL.CompileShader(vertexShader);

            // Same as vertex shader
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, LoadShaderSource(shaderFilename + ".frag"));
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

        void DrawBackground()
        {
            GL.Disable(EnableCap.DepthTest);
            GL.UseProgram(backgroundShader);

            Color color;
            color = MainWindow.main.threeDSettings.BackgroundTopBackgroundColor();
            Vector4 topColor = new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            color = MainWindow.main.threeDSettings.BackgroundBottomBackgroundColor();
            Vector4 bottomColor = new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

            GL.Uniform4(GL.GetUniformLocation(backgroundShader, "topColor"), topColor);
            GL.Uniform4(GL.GetUniformLocation(backgroundShader, "bottomColor"), bottomColor);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        void GetUniformLocationsPrinterbed()
        {
            GL.UseProgram(plateShader);

            plateModelLoc = GL.GetUniformLocation(plateShader, "model");
            plateViewLoc = GL.GetUniformLocation(plateShader, "view");
            plateProjLoc = GL.GetUniformLocation(plateShader, "projection");

            plateColorLoc = GL.GetUniformLocation(plateShader, "plateColor");
            gridColorLoc = GL.GetUniformLocation(plateShader, "gridColor");
            gridSpacingLoc = GL.GetUniformLocation(plateShader, "gridSpacing");
        }

        void CreateBuildPlatePrinterbed()
        {
            float[] vertices =
            {
                0f,   0f,   0f,
                256f, 0f,   0f,
                256f, 256f, 0f,
                0f,   256f, 0f
            };

            plateVao = GL.GenVertexArray();
            plateVbo = GL.GenBuffer();

            GL.BindVertexArray(plateVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, plateVbo);
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
        }

        private void computeModelViewProj(ref Matrix4 model, ref Matrix4 view, ref Matrix4 proj)
        {
            model = Matrix4.Identity;

#if true
            view = Matrix4.LookAt(threeDCam.CameraPosition, threeDCam.viewCenter, Vector3.UnitZ);
#else       // Fixed camera position for testing
            view = Matrix4.LookAt(
                new Vector3(300, 300, 300),
                new Vector3(128, 128, 0),
                Vector3.UnitZ);
#endif
            float bedRadius = (float)(1.5 * Math.Sqrt(
                 (MainWindow.main.PrintAreaDepth * MainWindow.main.PrintAreaDepth +
                  MainWindow.main.PrintAreaHeight * MainWindow.main.PrintAreaHeight +
                  MainWindow.main.PrintAreaWidth * MainWindow.main.PrintAreaWidth) * 0.25));

            float dist = (float)threeDCam.distance;
            float nearDist = Math.Max(1, dist - bedRadius);
            float farDist = Math.Max(bedRadius * 2, dist + bedRadius);
            proj = Matrix4.CreatePerspectiveFieldOfView(
                            (float)threeDCam.angle * 2.0f,
                            Size.X / (float)Size.Y,
                            nearDist,
                            farDist);
        }

        private void DrawPrintbedBase()
        {
            if (MainWindow.main.threeDSettings.IsPrintbed() != true) return;

            Matrix4 model = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 proj = Matrix4.Identity;

            computeModelViewProj(ref model, ref view, ref proj);

            GL.UseProgram(plateShader);

            GL.UniformMatrix4(plateModelLoc, false, ref model);
            GL.UniformMatrix4(plateViewLoc, false, ref view);
            GL.UniformMatrix4(plateProjLoc, false, ref proj);

            GL.Uniform4(plateColorLoc, new Vector4(0.8f, 0.8f, 0.8f, 1f));
            GL.Uniform4(gridColorLoc, new Vector4(0f, 0f, 0f, 0.5f));
            GL.Uniform1(gridSpacingLoc, 10f);   // 10 mm grid

            GL.BindVertexArray(plateVao);

            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
        }

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr hWnd);

        public void FocusGameWindow()
        {
            IntPtr hwnd;
            unsafe { hwnd = GLFW.GetWin32Window(WindowPtr); }
            SetForegroundWindow(hwnd);
            SetFocus(hwnd);
        }

        protected override void OnMove(WindowPositionEventArgs e)
        {
            base.OnMove(e);

            // CHANGED: Position is now Vector2i
            int newX = e.Position.X;
            int newY = e.Position.Y;

            MainWindow.main.Dispatcher.Invoke(() =>
            {
                if (MainWindow.main != null)
                    MainWindow.main.UpdateLocation(newX, newY);
            });
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            if (!loaded) return;

            int newWidth = e.Width;
            int newHeight = e.Height;

            MainWindow.main.Dispatcher.Invoke(() =>
            {
                if (MainWindow.main != null)
                    MainWindow.main.UpdateSize(newWidth, newHeight);
            });

            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            Invalidate();
        }

        protected override void OnUnload()
        {
            base.OnUnload();

            MainWindow.main.Dispatcher.InvokeAsync(() =>
            {
                MainWindow.main.Visibility = Visibility.Hidden;
                System.Windows.Application.Current.Shutdown();
            });
        }

        // Thread-safe queue for GL objects that need to be deleted on the GL thread
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> glActions = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        /// <summary>
        /// Schedules GL resource deletion to run safely on the GL thread.
        /// Call this from ANY thread instead of calling GL.Delete* directly.
        /// </summary>
        public void InvokeGL(Action action) => glActions.Enqueue(action);

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            while (glActions.TryDequeue(out Action action))
                action();

            if (!_isDirty) return;
            _isDirty = false;
            gl_Paint();
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            Application_Idle();
        }

        // ── Mouse input ───────────────────────────────────────────────────────
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            var pos = MouseState.Position;          // CHANGED: e.X / e.Y are gone; use MouseState.Position
            keyX = (int)pos.X; keyY = (int)pos.Y;
            threeDCam.PreparePanZoomRot();
            lastX = xDown = (int)pos.X;
            lastY = yDown = (int)pos.Y;
            movePlane = new Geom3DPlane(new Geom3DVector(0, 0, 0), new Geom3DVector(0, 0, 1));
            moveStart = moveLast = new Geom3DVector(0, 0, 0);
            UpdatePickLine((int)pos.X, (int)pos.Y);
            movePlane.intersectLine(pickLine, moveStart);
            Invalidate();
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);

            var kb = KeyboardState;
            if (kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt))
                return;

            var mouse = MouseState;
            bool anyButton = mouse.IsButtonDown(MouseButton.Left) ||
                             mouse.IsButtonDown(MouseButton.Right) ||
                             mouse.IsButtonDown(MouseButton.Middle);

            if (!anyButton)
            {
                speedX = speedY = 0;
                Invalidate();
                return;
            }

            xPos = e.X;
            yPos = e.Y;
            UpdatePickLine((int)e.X, (int)e.Y);
            movePos = new Geom3DVector(0, 0, 0);
            movePlane.intersectLine(pickLine, movePos);
            float d = Math.Min(ClientSize.X, ClientSize.Y) / 3f;
            speedX = Math.Max(-1, Math.Min(1, (xPos - xDown) / d));
            speedY = Math.Max(-1, Math.Min(1, (yPos - yDown) / d));
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            ThreeDModel sel = null;

            var pos = MouseState.Position;

            if (e.Button == MouseButton.Left)
            {
                sel = Picktest((int)pos.X, (int)pos.Y);
                if (sel != null)
                {
                    movePlane = new Geom3DPlane(pickPoint, new Geom3DVector(0, 0, 1));
                    moveStart = moveLast = new Geom3DVector(pickPoint);
         
                    MainWindow.main.Dispatcher.InvokeAsync(() =>
                    {
                        stlComp.ObjectSelected(sel);
                    });
                }
                else if (keyX == (int)pos.X && keyY == (int)pos.Y)
                {
                    MainWindow.main.Dispatcher.InvokeAsync(() =>
                    {
                        stlComp.listObjects.SelectedItems.Clear();
                    });
                }
            }

            if (e.Button == MouseButton.Right)
            {
                sel = Picktest((int)pos.X, (int)pos.Y);
                if (sel != null)
                {
                    movePlane = new Geom3DPlane(pickPoint, new Geom3DVector(0, 0, 1));
                    moveStart = moveLast = new Geom3DVector(pickPoint);
           
                    MainWindow.main.Dispatcher.InvokeAsync(() =>
                    {
                        stlComp.ObjectSelected(sel);
                        MainWindow.main.ShowContextMenu(stlComp.listObjects.SelectedItems.Count > 0);
                    });
                }
                else if (keyX == (int)pos.X && keyY == (int)pos.Y)
                {
                    MainWindow.main.Dispatcher.InvokeAsync(() =>
                    {
                        MainWindow.main.ShowContextMenu(stlComp.listObjects.SelectedItems.Count > 0);
                    });
                }
            }
            speedX = speedY = 0;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.OffsetY != 0)            // CHANGED: e.Delta → e.OffsetY
            {
                threeDCam.PreparePanZoomRot();
                threeDCam.Zoom(1f - e.OffsetY / 60f);
                zoom *= 1f - e.OffsetY / 20f;
                if (zoom < 0.002f) zoom = 0.002f;
                if (zoom > 5.9f) zoom = 5.9f;
                Invalidate();
            }
        }

        // ── Keyboard input ────────────────────────────────────────────────────
        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);
            ThreeDControl_KeyDown(e);
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            ThreeDControl_KeyPress(e);
        }

        private void ThreeDControl_KeyDown(KeyboardKeyEventArgs e)
        {
            if (e.Key == Keys.Delete)
                button_remove_Click(null, null);
        }

        private void ThreeDControl_KeyPress(TextInputEventArgs e)
        {
            if (e.AsString == "-")
                button_zoomOut_Click(null, null);

            if (e.AsString == "+")
                button_zoomIn_Click(null, null);
        }

        // Context-menu action handlers
        public void ContextMenu_LandObject() => MainWindow.main.UI_move.button_land_Click(null, null);

        public void ContextMenu_ResetObject()
        {
            MainWindow.main.UI_resize_advance.button_Reset_Click(null, null);
            MainWindow.main.UI_rotate.button_rotate_reset_Click(null, null);
            MainWindow.main.UI_move.button_move_reset_Click(null, null);
        }

        public void ContextMenu_RemoveObject() => MainWindow.main.remove_toggleButton_Click(null, null);

        public void ContextMenu_MmToInch()
        {
            PrintModel m = stlComp.SingleSelectedModel;
            if (m != null) stlComp.DoInchOrScale(m, true);
        }

        public void ContextMenu_InchToMm()
        {
            PrintModel m = stlComp.SingleSelectedModel;
            if (m != null) stlComp.DoInchtomm(m);
        }

        public void ContextMenu_Clone() => stlComp.CloneObject();

        // ── Rendering ─────────────────────────────────────────────────────────
        private void gl_Paint()
        {
            if (!loaded) return;
            try
            {
                fpsTimer.Reset();
                fpsTimer.Start();

                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                DrawBackground();

                DrawPrintbedBase();

                DrawModels();

                DrawBBoxLines();

                // swap the buffers
                SwapBuffers();

                fpsTimer.Stop();
                double fps = 1.0 / fpsTimer.Elapsed.TotalSeconds;
            }
            catch { }
        }


        // ── Lights ────────────────────────────────────────────────────────────
        private void AddLights()
        {
            //GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0.2f, 0.2f, 0.2f, 1f });
            //GL.Light(LightName.Light0, LightParameter.Diffuse, new float[] { 0, 0, 0, 0 });
            //GL.Light(LightName.Light0, LightParameter.Specular, new float[] { 0, 0, 0, 0 });
            //GL.Enable(EnableCap.Light0);

            var s = MainWindow.main.threeDSettings;

            SetLight(LightName.Light1, s.EnableLight1(), s.Ambient1(), s.Diffuse1(), s.Specular1(), s.Dir1(), false);
            SetLight(LightName.Light2, s.EnableLight2(), s.Ambient2(), s.Diffuse2(), s.Specular2(), s.Dir2(), true);
            SetLight(LightName.Light3, s.EnableLight3(), s.Ambient3(), s.Diffuse3(), s.Specular3(), s.Dir3(), true);
            SetLight(LightName.Light4, s.EnableLight4(), s.Ambient4(), s.Diffuse4(), s.Specular4(), s.Dir4(), true);
            //GL.Enable(EnableCap.Lighting);
        }

        private void SetLight(LightName name,
                              bool enable,
                              float[] amb, float[] diff, float[] spec, float[] pos,
                              bool setExponent)
        {
            if (enable)
            {
                ////GL.Light(name, LightParameter.Ambient, amb);
                ////GL.Light(name, LightParameter.Diffuse, diff);
                ////GL.Light(name, LightParameter.Specular, spec);
                ////GL.Light(name, LightParameter.Position, pos);
                //if (setExponent)
                //    GL.Light(name, LightParameter.SpotExponent, new float[] { 1f, 1f, 1f, 1f });
                //GL.Enable((EnableCap)name);
            }
            else GL.Disable((EnableCap)name);
        }

        // ── Draw helpers ──────────────────────────────────────────────────────

        public void UploadMeshToGPU()
        {
            if (stlComp.models.Count == 0)
                return;

            stlVertices.Clear();
  
            foreach (var m in stlComp.models)
            {
                m.Paint();

                for (int i = 0; i < m.submesh.glVertices.Length; i+=3)
                {   // [x y z nx ny nz]
                    stlVertices.Add(m.submesh.glVertices[i]);
                    stlVertices.Add(m.submesh.glVertices[i+1]);
                    stlVertices.Add(m.submesh.glVertices[i+2]);
                    stlVertices.Add(m.submesh.glNormals[i]);
                    stlVertices.Add(m.submesh.glNormals[i]+1);
                    stlVertices.Add(m.submesh.glNormals[i]+2);
                }
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

        private void DrawModels()
        {
            if (stlVertices.Count == 0) return;

            GL.Enable(EnableCap.PolygonSmooth);
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);

            Matrix4 model = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 proj = Matrix4.Identity;

            computeModelViewProj(ref model, ref view, ref proj);

            GL.UseProgram(stlShader);

#if true
            GL.UniformMatrix4(stlViewLoc, false, ref view);
            GL.UniformMatrix4(stlProjLoc, false, ref proj);

            if (stlComp.models.Count > 0)
            {
                GL.UniformMatrix4(stlModelLoc, false, ref stlComp.models[0].trans);
                GL.BindVertexArray(stlVao);
                GL.DrawArrays(PrimitiveType.Triangles, 0, stlVertices.Count / 6);
            }
#else
            GL.UniformMatrix4(stlModelLoc, false, ref model);
            GL.UniformMatrix4(stlViewLoc, false, ref view);
            GL.UniformMatrix4(stlProjLoc, false, ref proj);
            GL.BindVertexArray(stlVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, stlVertices.Count / 6);
#endif
        }

        void SetupBBox()
        {
            bboxVao = GL.GenVertexArray();
            bboxVbo = GL.GenBuffer();

            GL.BindVertexArray(bboxVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, bboxVbo);

            // Pre-allocate space for 24 vertices (X, Y, Z)
            // We pass IntPtr.Zero to just reserve the space on the GPU
            GL.BufferData(BufferTarget.ArrayBuffer, 24 * 3 * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);

            // Define the layout (assuming your shader uses location 0 for position)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        }


        private void DrawBBoxLines()
        {
            if (stlComp.models.Count == 0) return;

            PrintModel m = stlComp.models[0];
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
            GL.BindBuffer(BufferTarget.ArrayBuffer, bboxVbo);

            // 2. Upload the new data to the START of the buffer (offset 0)
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, verticesBbox.Length * sizeof(float), verticesBbox);

            Matrix4 model = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 proj = Matrix4.Identity;

            computeModelViewProj(ref model, ref view, ref proj);

            GL.UseProgram(bboxShader);

            GL.UniformMatrix4(bboxModelLoc, false, ref model);
            GL.UniformMatrix4(bboxViewLoc, false, ref view);
            GL.UniformMatrix4(bboxProjLoc, false, ref proj);

            // 3. Bind the BBox VAO and Draw
            GL.BindVertexArray(bboxVao);

            // 4. Set your BBox color (as discussed previously)
            int colorLoc = GL.GetUniformLocation(bboxShader, "ourColor");
            GL.Uniform4(colorLoc, Color4.LimeGreen);

            GL.DrawArrays(PrimitiveType.Lines, 0, 24);
        }

        // ── Pick / ray-cast ───────────────────────────────────────────────────
        public void UpdatePickLine(int x, int y)
        {
            float bedRadius = (float)(1.5 * Math.Sqrt(
                 (MainWindow.main.PrintAreaDepth * MainWindow.main.PrintAreaDepth +
                  MainWindow.main.PrintAreaHeight * MainWindow.main.PrintAreaHeight +
                  MainWindow.main.PrintAreaWidth * MainWindow.main.PrintAreaWidth) * 0.25));
            
            float dist = (float)threeDCam.distance;
            float nearDist = Math.Max(1, dist - bedRadius);
            float midHeight = 2.0f * (float)Math.Tan(threeDCam.angle) * dist;
            float nearHeight = 2.0f * (float)Math.Tan(threeDCam.angle) * nearDist;
            float aspectRatio = (float)ClientSize.X / (float)ClientSize.Y;

            int window_y = (ClientSize.Y - y) - ClientSize.Y / 2;            // CHANGED: Width/Height → ClientSize.X / ClientSize.Y
            double norm_y = (double)window_y / (ClientSize.Y / 2.0);
            int window_x = x - ClientSize.X / 2;
            double norm_x = (double)window_x / (ClientSize.X / 2.0);
            float fpy = (float)(nearHeight * 0.5 * norm_y);
            float fpx = (float)(nearHeight * 0.5 * aspectRatio * norm_x);

            Vector4 dirN = new Vector4(fpx, fpy, -nearDist, 0);
            Vector3 camPos = threeDCam.CameraPosition;
            Matrix4 ntrans = Matrix4.LookAt(camPos.X, camPos.Y, camPos.Z,
                threeDCam.viewCenter.X, threeDCam.viewCenter.Y, threeDCam.viewCenter.Z, 0, 0, 1.0f);
            ntrans = Matrix4.Invert(ntrans);
            Vector4 frontPoint = ntrans.Row3;
            Vector4 dirVec = dirN * ntrans;
            pickLine = new Geom3DLine(
                new Geom3DVector(frontPoint.X / frontPoint.W, frontPoint.Y / frontPoint.W, frontPoint.Z / frontPoint.W),
                new Geom3DVector(dirVec.X, dirVec.Y, dirVec.Z), true);
            pickLine.dir.normalize();
        }

        private ThreeDModel Picktest(int x, int y)
        {
            Vector3 near, far;

            Matrix4 modelMatrix = Matrix4.Identity;
            Matrix4 view = Matrix4.Identity;
            Matrix4 proj = Matrix4.Identity;
            Vector2i windowSize = ClientSize;
            computeModelViewProj(ref modelMatrix, ref view, ref proj);
            Ray ray = RayCasting.GenerateRay(x, y, view, proj, windowSize, out near, out far);

            float length = float.MaxValue;
            ThreeDModel nearestModel = null;

            foreach (PrintModel model in stlComp.models)
            {
                if (!RayCasting.RaycastAABB(ray, model)) continue;
                float[] rayPos = { ray.Position.X, ray.Position.Y, ray.Position.Z };
                float[] rayNor = { ray.Normal.X, ray.Normal.Y, ray.Normal.Z };
                ModelMatrix mtx = ModelObjectToolHelper.ToModelMatrix(model.trans);

                int id; float output;
                if (ModelObjectToolWrapper.Instance.Tool.RayIntersectTriangle(
                        mtx, model.submesh.glVertices, rayPos, rayNor, out id, out output))
                {
                    Vector3 hitP = ray.Position + ray.Normal * output;
                    float lineLen = new Line(near, hitP).Length;
                    if (lineLen <= length)
                    {
                        length = lineLen;
                        nearestModel = model;
                    }
                }
                GC.Collect();
            }

            // TEST
            Debug.WriteLine($"Picktest: nearest model is {(nearestModel != null ? "Hit" : "null")}, length = {length}");    
          
            return nearestModel;
        }

        // ── Idle / animation update ───────────────────────────────────────────
        private void Application_Idle()
        {
            if (!loaded || (speedX == 0 && speedY == 0)) return;

            // CHANGED: OpenTK.Input.Keyboard/Mouse → KeyboardState / MouseState properties
            var kb = KeyboardState;
            var mouse = MouseState;

            int emode = mode;
            if (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift) ||
                mouse.IsButtonDown(MouseButton.Middle)) emode = 2;
            if (kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl)) emode = 0;
            if (kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt)) emode = 4;

            float d = Math.Min(ClientSize.X, ClientSize.Y) / 3f;

            switch (emode)
            {
                case 0: // Rotate
                    speedX = (xPos - xDown) / d;
                    speedY = (yPos - yDown) / d;
                    threeDCam.Rotate(-speedX * 0.9, speedY * 0.9);
                    Invalidate();
                    break;

                case 1: // Pan (slow)
                case 2: // Pan (fast)
                    {
                        speedX = (xPos - xDown) / ClientSize.X;
                        speedY = (yPos - yDown) / ClientSize.Y;
                        Vector3 planeVec = Vector3.Subtract(
                            new Vector3(moveStart.x, moveStart.y, moveStart.z), threeDCam.CameraPosition);
                        float dot = Vector3.Dot(planeVec, threeDCam.ViewDirection());
                        double len = dot > 0 ? planeVec.Length : -1;
                        float scale = emode == 1 ? 200f : 1f;
                        threeDCam.Pan(speedX * scale * (emode == 2 ? -1 : 1),
                                speedY * scale * (emode == 2 ? -1 : 1), len);
                        Invalidate();
                        break;
                    }

                case 3: // Zoom
                    threeDCam.Zoom(1 - speedY / 3f);
                    Invalidate();
                    break;

                case 4: // Move objects
                    {
                        Geom3DVector diff = movePos.sub(moveLast);
                        moveLast = movePos;
                        speedX = (xPos - lastX) * 200 * zoom / ClientSize.X;
                        speedY = (yPos - lastY) * 200 * zoom / ClientSize.Y;

                        var selModels = new List<PrintModel>();
                        var prevX = new List<float>();
                        var prevY = new List<float>();

                        foreach (PrintModel stl in stlComp.ListObjects(true))
                        {
                            selModels.Add(stl);
                            prevX.Add(stl.Position.x);
                            prevY.Add(stl.Position.y);
                        }
                        stlComp.ObjectMoved(diff.x, diff.y);
               
                        lastX = xPos; lastY = yPos;
                        Invalidate();
                        break;
                    }
            }
        }



        // ── Zoom button handlers ──────────────────────────────────────────────
        public void button_zoomIn_Click(object sender, EventArgs e)
        {
            threeDCam.PreparePanZoomRot(); threeDCam.Zoom(0.9);
            zoom = Math.Max(0.002f, Math.Min(5.9f, zoom));
            Invalidate();
        }

        public void button_zoomOut_Click(object sender, EventArgs e)
        {
            threeDCam.PreparePanZoomRot(); threeDCam.Zoom(1.1);
            zoom = Math.Max(0.002f, Math.Min(5.9f, zoom));
            Invalidate();
        }

        public void button_remove_Click(object sender, EventArgs e)
        {
            MainWindow.main.Dispatcher.Invoke(() =>
            {
                stlComp.buttonRemoveSTL_Click(null, null);
                stlComp.updateSTLState(null);
            });
            Invalidate();
        }

        // Function to load a text file and return its contents as a string
        public string LoadShaderSource(string filePath)
        {
            string shaderSource = "";

            try
            {
                using (StreamReader reader = new StreamReader("../../../Shaders/" + filePath))
                {
                    shaderSource = reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Failed to load shader source file: " + e.Message);
            }

            return shaderSource;
        }
    }
}