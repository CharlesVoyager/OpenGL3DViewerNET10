using OpenGL3DViewerNET10.Draw;
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
        BackgroundDraw backgroundDraw = null;
        PrinterbedDraw printerbedDraw = null;
        BoundingBoxDraw boundingBoxDraw = null;
        RedBorderDraw redBorderDraw = null;

        bool loaded = false;
        float xDown, yDown;
        float xPos, yPos;
        float speedX, speedY;
        float lastX, lastY;
        readonly Stopwatch fpsTimer = new Stopwatch();
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
                    Title = "OpenGL 3D Viewer (OpenTK 4.9.4 + .NET 10.0)",
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
 
        // ── Translations ──────────────────────────────────────────────────────
        private void translate()
        {
            // These string keys mirror the original WinForms menu items.
            // Apply them to the WPF ContextMenu items exposed by ui if needed.
        }

        #region Set minimum window size via Win32 subclassing (WM_GETMINMAXINFO)
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
        #endregion

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

            #region // Detect OpenGL version & capabilities (runs once)
            try
            {
                string sv = GL.GetString(StringName.Version).Trim();    // EX: 4.0.0 NVIDIA 591.74
                int p = sv.IndexOf(' ');
                if (p > 0) sv = sv.Substring(0, p);                     // 4.0.0
                p = sv.IndexOf('.');
                if (p > 0)
                {
                    p = sv.IndexOf('.', p + 1);
                    if (p > 0) sv = sv.Substring(0, p);
                    MainWindow.main.threeDSettings.OpenGLVersion = Convert.ToSingle(sv, CultureInfo.InvariantCulture);
                }
                else
                {
                    try
                    {
                        float val;
                        float.TryParse(sv, out val);
                        MainWindow.main.threeDSettings.OpenGLVersion = val;
                    }
                    catch 
                    { 
                        MainWindow.main.threeDSettings.OpenGLVersion = 1.1f; 
                    }
                }
                MainWindow.main.threeDSettings.UseVBOs = GL.GetString(StringName.Extensions).Contains("GL_ARB_vertex_buffer_object");
            }
            catch { }
            #endregion

            // Background
            backgroundDraw = new BackgroundDraw();
            backgroundDraw.Init(); 

            // Printer bed
            printerbedDraw = new PrinterbedDraw();
            printerbedDraw.Init();
         
            // Red Border
            redBorderDraw = new RedBorderDraw();
            redBorderDraw.Init();

            // Bounding Box
            boundingBoxDraw = new BoundingBoxDraw();
            boundingBoxDraw.Init();

            // STL Model
            foreach(var m in stlComp.models)
                m.Drawer.Init();

            loaded = true;
        }

        protected override void OnMove(WindowPositionEventArgs e)
        {
            base.OnMove(e);

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

            backgroundDraw.Dispose();
            printerbedDraw.Dispose();
            redBorderDraw.Dispose();
            boundingBoxDraw.Dispose();

            foreach (var m in stlComp.models)
                m.Drawer.Dispose();

            MainWindow.main.Dispatcher.Invoke(() =>
            {
                MainWindow.main.Visibility = Visibility.Hidden;
                Application.Current.Shutdown();
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
            inputHandling();
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
            if (e.Key == Keys.Delete)
            {
                MainWindow.main.Dispatcher.Invoke(() =>
                {
                    stlComp.buttonRemoveSTL_Click(null, null);
                });
                Invalidate();
            }
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            if (e.AsString == "-")
                ZoomOutKeyHandling(null, null);

            if (e.AsString == "+")
                ZoomInKeyHandling(null, null);
        }

        public void ZoomOutKeyHandling(object sender, EventArgs e)
        {
            threeDCam.PreparePanZoomRot(); threeDCam.Zoom(1.1);
            zoom = Math.Max(0.002f, Math.Min(5.9f, zoom));
            Invalidate();
        }

        public void ZoomInKeyHandling(object sender, EventArgs e)
        {
            threeDCam.PreparePanZoomRot(); threeDCam.Zoom(0.9);
            zoom = Math.Max(0.002f, Math.Min(5.9f, zoom));
            Invalidate();
        }

        // ── Rendering ─────────────────────────────────────────────────────────
        private void gl_Paint()
        {
            if (!loaded) return;
            try
            {
                fpsTimer.Reset();
                fpsTimer.Start();

                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                backgroundDraw.Draw();

                printerbedDraw.Draw();

                boundingBoxDraw.Draw();

                redBorderDraw.Draw();
            
                foreach(var m in stlComp.models)
                    m.Drawer.Draw();

                SwapBuffers();

                fpsTimer.Stop();
                double fps = 1.0 / fpsTimer.Elapsed.TotalSeconds;
            }
            catch { }
        }


        // ── Pick / ray-cast ───────────────────────────────────────────────────
        public void UpdatePickLine(int x, int y)
        {
            float bedRadius = (float)(1.5 * Math.Sqrt(
                 (MainWindow.main.threeDSettings.PrintAreaDepth * MainWindow.main.threeDSettings.PrintAreaDepth +
                  MainWindow.main.threeDSettings.PrintAreaHeight * MainWindow.main.threeDSettings.PrintAreaHeight +
                  MainWindow.main.threeDSettings.PrintAreaWidth * MainWindow.main.threeDSettings.PrintAreaWidth) * 0.25));
            
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
            threeDCam.GetModelViewProj(ref modelMatrix, ref view, ref proj);
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

            // Debug
            //Debug.WriteLine($"Picktest: nearest model is {(nearestModel != null ? "Hit" : "null")}, length = {length}");    
          
            return nearestModel;
        }

        private void inputHandling()
        {
            if (!loaded || (speedX == 0 && speedY == 0)) return;

            var kb = KeyboardState;
            var mouse = MouseState;

            int emode = 0;
            if (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift) || mouse.IsButtonDown(MouseButton.Middle)) 
                emode = 2;
            if (kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl)) 
                emode = 0;
            if (kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt)) 
                emode = 4;

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
                    threeDCam.Pan(  speedX * scale * (emode == 2 ? -1 : 1),
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

                    MainWindow.main.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (PrintModel stl in stlComp.ListObjects(true))
                        {
                            selModels.Add(stl);
                            prevX.Add(stl.Position.x);
                            prevY.Add(stl.Position.y);
                        }
                        stlComp.ObjectMoved(diff.x, diff.y);
                    });
               
                    lastX = xPos; lastY = yPos;
                    Invalidate();
                    break;
                }
            }
        }
    }
}