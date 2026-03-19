using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using View3D.model;
using View3D.model.geom;

// ──────────────────────────────────────────────────────────────────────────────
//  NOTE: The WinForms Designer file (STLComposer_Designer.cs) is entirely
//  replaced by STLComposer.xaml.  Delete that file from your project.
//
//  Key WinForms → WPF mapping notes
//  ─────────────────────────────────
//  • ListView (WF)        → ListView + GridView (WPF)
//  • ListViewItem.Tag     → ListViewItemModel.Model  (wrapper class below)
//  • ListViewItem.Selected→ ListView.SelectedItems / IsSelected
//  • Button (WF inline)   → DataTemplate Button in the Delete GridViewColumn
//  • Panel.Visible        → UIElement.Visibility
//  • ForeColor            → Foreground (Brush)
//  • ImageList            → ImageSource / BitmapImage loaded from resources
//  • ErrorProvider        → Validation + Border highlighting (Style in XAML)
//  • Control.ModifierKeys → Keyboard.Modifiers
//  • Keys.Shift/Control   → ModifierKeys.Shift / ModifierKeys.Control
//  • KeyEventArgs.KeyCode → KeyEventArgs.Key
//  • MessageBox (WF)      → System.Windows.MessageBox
// ──────────────────────────────────────────────────────────────────────────────

namespace View3D.view
{
    /// <summary>
    /// Wrapper that gives the WPF ListView an observable item to bind against.
    /// </summary>
    public class ListViewItemModel
    {
        public string         Name              { get; set; }
        public PrintModel     Model             { get; set; }
        public ImageSource    MeshStatusImage   { get; set; }
        public ImageSource    CollisionStatusImage { get; set; }
    }

    public partial class STLComposer : Window
    {
        public List<PrintModel> models     = new List<PrintModel>();

        // ── Private fields ────────────────────────────────────────────────────
        private IDraw   modelDrawer   = new ModelGLDraw();
        private List<PrintModel> cloneModels = new List<PrintModel>();

        // Image sources replacing WinForms ImageList (index → meaning):
        //   0 = unlock16   1 = lock16   2 = ok16   3 = bad16   4 = trash16
        private ImageSource[] _icons = null;

        // ── Constructor ───────────────────────────────────────────────────────
        public STLComposer()
        {
            InitializeComponent();
            _icons = LoadIcons();
            try
            {
                updateEnabled();

                modelDrawer.GetColorSetting = MainWindow.main.threeDSettings.GetColorSetting;

                if (MainWindow.main != null)
                {
                    MainWindow.main.languageChanged += translate;
                }
            }
            catch { }
        }

        // ── Translate (stub kept for API compatibility) ───────────────────────
        public void translate() { }

        // =====================================================================
        //  ListView helpers
        // =====================================================================

        /// <summary>Gets all ListViewItemModel entries in the list.</summary>
        private IEnumerable<ListViewItemModel> AllRows()
            => listObjects.Items.Cast<ListViewItemModel>();

        /// <summary>Gets selected ListViewItemModel entries.</summary>
        private IEnumerable<ListViewItemModel> SelectedRows()
            => listObjects.SelectedItems.Cast<ListViewItemModel>();

        private ListViewItemModel RowForModel(PrintModel model)
            => AllRows().FirstOrDefault(r => r.Model == model);

        // ── Add / remove rows ─────────────────────────────────────────────────
        private void AddObject(PrintModel model)
        {
            var row = BuildRow(model);
            listObjects.Items.Add(row);
            SetObjectSelected(model, true);
        }

        private ListViewItemModel BuildRow(PrintModel model)
        {
            return new ListViewItemModel
            {
                Name                 = model.name,
                Model                = model,
                MeshStatusImage      = _icons[model.Model.manifold ? 2 : 3],
                CollisionStatusImage = _icons[model.outside        ? 3 : 2],
            };
        }

        private void RefreshRow(ListViewItemModel row)
        {
            row.MeshStatusImage      = _icons[row.Model.Model.manifold ? 2 : 3];
            row.CollisionStatusImage = _icons[row.Model.outside        ? 3 : 2];
            // Force ListView to re-render the row
            int idx = listObjects.Items.IndexOf(row);
            if (idx >= 0)
            {
                listObjects.Items.RemoveAt(idx);
                listObjects.Items.Insert(idx, row);
            }
        }

        // =====================================================================
        //  Public API (unchanged signatures from WinForms version)
        // =====================================================================

        public LinkedList<PrintModel> ListObjects(bool selected)
        {
            var list = new LinkedList<PrintModel>();
            if (selected)
                foreach (var row in SelectedRows()) list.AddLast(row.Model);
            else
                foreach (var row in AllRows())      list.AddLast(row.Model);
            return list;
        }

        public PrintModel SingleSelectedModel
        {
            get
            {
                if (listObjects.SelectedItems.Count != 1) return null;
                return ((ListViewItemModel)listObjects.SelectedItems[0]).Model;
            }
        }

        public void UpdateAnalyserData()
        {
            PrintModel model = SingleSelectedModel;
            if (model == null) return;

            labelVertices.Text             = model.Model.vertices.Count.ToString();
            labelEdges.Text                = model.Model.edges.Count.ToString();
            labelFaces.Text                = model.Model.triangles.Count.ToString();
            labelShells.Text               = model.Model.shells.ToString();
            labelIntersectingTriangles.Text = model.Model.intersectingTriangles.Count.ToString();
            labelLoopEdges.Text            = model.Model.loopEdges.ToString();
            labelHighConnected.Text        = model.Model.manyShardEdges.ToString();

            // Colour: black when zero, red when non-zero
            var red   = new SolidColorBrush(Colors.Red);
            var black = new SolidColorBrush(Colors.Black);
            labelIntersectingTriangles.Foreground = model.Model.intersectingTriangles.Count == 0 ? black : red;
            labelLoopEdges.Foreground             = model.Model.loopEdges            == 0 ? black : red;
            labelHighConnected.Foreground         = model.Model.manyShardEdges       == 0 ? black : red;
        }

        public void SetObjectSelected(PrintModel model, bool select)
        {
            var row = RowForModel(model);
            if (row == null) return;
            if (select)
            {
                if (!listObjects.SelectedItems.Contains(row))
                    listObjects.SelectedItems.Add(row);
            }
            else
            {
                listObjects.SelectedItems.Remove(row);
            }
        }

        public void landModel(PrintModel model)
        {
            if (typeof(PrintModel) != model.GetType()) return;
            if (null == model.Model) return;
            model.LandUpdateBB();
        }

        public void RemoveAllObject()
        {
            foreach (var row in AllRows().ToList())
                if (row.Model.GetType() == typeof(PrintModel))
                    SetObjectSelected(row.Model, true);
            buttonRemoveSTL_Click(null, null);
        }

        public void RemoveLastModel()
        {
            if (0 == models.Count) return;
            int idx = models.Count - 1;
            while (idx >= 0)
            {
                if (typeof(PrintModel) == models[idx].GetType() && null != models[idx].Model)
                {
                    RemoveModel(models[idx]);
                    return;
                }
                idx--;
            }
        }

        public List<PrintModel> GetAllPrintModels()
        {
            var list = new List<PrintModel>();
            foreach (var m in models)
                if (IsValidPrintModel(m)) list.Add(m);
            return list;
        }

        public List<PrintModel> GetSelectedPrintModels()
        {
            var list = new List<PrintModel>();
            foreach (var m in models)
                if (IsValidPrintModel(m) && m.Selected) list.Add(m);
            return list;
        }

        // ── LockAspectRatio ───────────────────────────────────────────────────
        /// <summary>
        /// Mirrors the WinForms buttonLockAspect.ImageIndex == 1 pattern.
        /// The WPF ToggleButton.IsChecked property drives the lock state.
        /// </summary>
        public bool LockAspectRatio
        {
            get => buttonLockAspect.IsChecked == true;
            set
            {
                textScaleX.IsEnabled = value;
                textScaleY.IsEnabled = value;
                textScaleZ.IsEnabled = value;
            }
        }

        // =====================================================================
        //  openAndAddObject  (identical logic, WinForms dialogs replaced)
        // =====================================================================
        public static readonly ManualResetEventSlim _stlModelDataReady = new ManualResetEventSlim(true);
        public async void openAndAddObject(string file)
        {
            if (MainWindow.main == null) return;

            listObjects.SelectedItems.Clear();
            models.Add(new PrintModel());
            bool modelToLand = true;
            var  modelIO     = new ModelInOut();

            try
            {
                MainWindow.main.BusyWindow.EnableBusyWindow();
                _stlModelDataReady.Reset();
                // Offload heavy work to background thread — UI thread is free immediately
                await Task.Run(() =>
                {
                    modelIO.LoadWOCatch(file, models[models.Count - 1].Model);
                    _stlModelDataReady.Set();
                    Console.WriteLine("LoadWOCatch Done.");
                });

                MainWindow.main.BusyWindow.DisableBusyWindow();
                models[models.Count - 1].name = Path.GetFileName(file);
            }
            catch (OutOfMemoryException)
            {
                models[models.Count - 1].Clear();
                models.RemoveAt(models.Count - 1);
                MainWindow.main.BusyWindow.Visibility = Visibility.Hidden;
                GC.Collect();
                MessageBox.Show("Error(" + (short)Protocol.ErrorCode.LOAD_FILE_FAIL + "): " + Trans.T("M_LOAD_FILE_FAIL"));
                return;
            }
            catch
            {
                MessageBox.Show(Trans.T("M_LOAD_STL_FILE_ERROR"), Trans.T("W_LOAD_STL_FILE_ERROR"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (MainWindow.main.BusyWindow.killed)
            {
                models[models.Count - 1].Clear();
                models.RemoveAt(models.Count - 1);
                return;
            }

            int last = models.Count - 1;
            models[last].mid              = last;
            models[last].serNum           = last;
            models[last].ListviewGetModels += ListObjects;

            if (modelToLand)
            {
                models[last].Center(MainWindow.main.PrintAreaWidth / 2, MainWindow.main.PrintAreaDepth / 2);
                if (models[last].BoundingBox.Center.x != 0 ||
                    models[last].BoundingBox.Center.y != 0 ||
                    models[last].BoundingBox.Center.z != 0)
                    models[last].ResetVertexPosToBBox();
                models[last].Land();
            }
            else
            {
                models[last].ResetVertexPosToBBox();
                models[last].Position.x = (float)models[last].Model.boundingBox.Center.x;
                models[last].Position.y = (float)models[last].Model.boundingBox.Center.y;
                models[last].Position.z = (float)models[last].Model.boundingBox.Center.z;
                models[last].UpdateBoundingBox();
            }

            if (models[last].Model.triangles.Count > 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AddObject(models[last]);
                    if (modelToLand) Autoposition();
                    updateSTLState(models[last]);
                });
            }

            double xxx = models[last].BoundingBox.Size.x * models[last].BoundingBox.Size.y
                       * models[last].BoundingBox.Size.z * 0.001;

            if (xxx < 0.1)
            {
                var dlg = new ObjectResizeDialog(
                    models[last].BoundingBox.Size.x,
                    models[last].BoundingBox.Size.y,
                    models[last].BoundingBox.Size.z);
                if (MainWindow.main.Visibility == Visibility.Visible)
                    dlg.Owner = MainWindow.main;
                dlg.ShowDialog();
                if      (dlg.gIsScale) DoInchOrScale(models[last], false);
                else if (dlg.gIsInch)  DoInchScale(models[last]);
            }
            else if (models[last].BoundingBox.Size.x - 1e-4 > Convert.ToDouble(MainWindow.main.PrintAreaWidth)  ||
                     models[last].BoundingBox.Size.y - 1e-4 > Convert.ToDouble(MainWindow.main.PrintAreaDepth)  ||
                     Math.Floor(models[last].BoundingBox.Size.z * 1000) / 1000 > Convert.ToDouble(MainWindow.main.PrintAreaHeight))
            {
                double tXBound = models[last].BoundingBox.Size.x / Convert.ToDouble(MainWindow.main.PrintAreaWidth);
                double tYBound = models[last].BoundingBox.Size.y / Convert.ToDouble(MainWindow.main.PrintAreaDepth);
                double tZBound = models[last].BoundingBox.Size.z / Convert.ToDouble(MainWindow.main.PrintAreaHeight);
                double tMax    = Math.Max(Math.Max(tXBound, tYBound), Math.Max(tYBound, tZBound));
                float  scaleValue = 0;

                if      (tMax == tXBound) scaleValue = (float)(Convert.ToDouble(MainWindow.main.PrintAreaWidth)  / models[last].BoundingBox.Size.x) * 100;
                else if (tMax == tYBound) scaleValue = (float)(Convert.ToDouble(MainWindow.main.PrintAreaDepth)  / models[last].BoundingBox.Size.y) * 100;
                else if (tMax == tZBound) scaleValue = (float)(Convert.ToDouble(MainWindow.main.PrintAreaHeight) / models[last].BoundingBox.Size.z) * 100;

                var result = System.Windows.MessageBox.Show(
                    Trans.T("M_OBJ_SCALE_DOWN") + " " + (int)scaleValue + "%",
                    Trans.T("W_OBJ_TOO_LARGE"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        tXBound = models[last].BoundingBox.Size.x / Convert.ToDouble(MainWindow.main.PrintAreaWidth);
                        tYBound = models[last].BoundingBox.Size.y / Convert.ToDouble(MainWindow.main.PrintAreaDepth);
                        tZBound = models[last].BoundingBox.Size.z / Convert.ToDouble(MainWindow.main.PrintAreaHeight);
                        tMax    = Math.Max(Math.Max(tXBound, tYBound), Math.Max(tYBound, tZBound));

                        if      (tMax == tXBound)
                        { models[last].Scale.x = models[last].Scale.y = models[last].Scale.z = (float)(Convert.ToDouble(MainWindow.main.PrintAreaWidth)  / models[last].BoundingBox.Size.x); }
                        else if (tMax == tYBound)
                        { models[last].Scale.y = models[last].Scale.x = models[last].Scale.z = (float)(Convert.ToDouble(MainWindow.main.PrintAreaDepth)  / models[last].BoundingBox.Size.y); }
                        else if (tMax == tZBound)
                        { models[last].Scale.z = models[last].Scale.x = models[last].Scale.y = (float)(Convert.ToDouble(MainWindow.main.PrintAreaHeight) / models[last].BoundingBox.Size.z); }

                        MainWindow.main.UI_move.button_land_Click(null, null);
                        Autoposition();
                    }
                    catch { }
                }
            }

            // Remember initial positions for all models after Autoposition.
            for (int i = 0; i < models.Count; i++)
            {
                models[i].Position.inix = models[i].Position.x;
                models[i].Position.iniy = models[i].Position.y;
                models[i].Position.iniz = models[i].Position.z;
            }

            MainWindow.main.threeDControl.UpdateChanges();
        }

        // =====================================================================
        //  CloneObject
        // =====================================================================
        private bool CloneObject(PrintModel model)
        {
            PrintModel newModel = (PrintModel)model.cloneWithModel();

            newModel.UpdateBoundingBox();
            newModel.mid    = models.Count;
            newModel.serNum = models.Count;
            models.Add(newModel);

            listObjects.Items.Add(BuildRow(newModel));
            listObjects.SelectedItems.Clear();
            SetObjectSelected(newModel, true);
            updateSTLState(newModel);
            Autoposition();
            updateSTLState(newModel);
            return true;
        }

        public void CloneObject()
        {
            cloneModels.Clear();
            cloneModels = GetSelectedPrintModels();
            foreach (var pm in cloneModels) CloneObject(pm);
        }

        // =====================================================================
        //  STL state / out-of-bounds
        // =====================================================================
        public void updateSTLState(PrintModel stl2)
        {
            if (stl2 != null)
                stl2.UpdateBoundingBox();

            bool dataChanged = false;
            var testList  = ListObjects(false);
            foreach (var pm in testList) { pm.oldOutside = pm.outside; pm.outside = false; }

            bool allPointsInside = true;
            foreach (var stl in testList)
            {
                if (    !MainWindow.main.PointInside(stl.xMin, stl.yMin, stl.zMin) ||
                        !MainWindow.main.PointInside(stl.xMax, stl.yMin, stl.zMin) ||
                        !MainWindow.main.PointInside(stl.xMin, stl.yMax, stl.zMin) ||
                        !MainWindow.main.PointInside(stl.xMax, stl.yMax, stl.zMin) ||
                        !MainWindow.main.PointInside(stl.xMin, stl.yMin, stl.zMax) ||
                        !MainWindow.main.PointInside(stl.xMax, stl.yMin, stl.zMax) ||
                        !MainWindow.main.PointInside(stl.xMin, stl.yMax, stl.zMax) ||
                        !MainWindow.main.PointInside(stl.xMax, stl.yMax, stl.zMax))
                {
                    stl.outside = true;
                    allPointsInside = false;
                }
            }

            MainWindow.main.OutofBound.Visibility = allPointsInside ? Visibility.Collapsed : Visibility.Visible;

            foreach (var pm in testList)
            {
                if (pm.oldOutside != pm.outside)
                {
                    dataChanged = true;
                    pm.ForceViewRegeneration();
                }
            }

            if (dataChanged)
                RefreshAllRows();
        }

        private void RefreshAllRows()
        {
            foreach (var row in AllRows().ToList())
            {
                int idx = listObjects.Items.IndexOf(row);
                if (idx < 0) continue;
                row.CollisionStatusImage = _icons[row.Model.outside ? 3 : 2];
                row.MeshStatusImage      = _icons[row.Model.Model.manifold ? 2 : 3];
                listObjects.Items.RemoveAt(idx);
                listObjects.Items.Insert(idx, row);
            }
        }

        public void check_stl_size_too_small()
        {
            PrintModel model = SingleSelectedModel;
            if (model == null) return;
            double xxx = model.BoundingBox.Size.x * model.BoundingBox.Size.y
                       * model.BoundingBox.Size.z * 0.001;
            if (xxx < 0.1)
            {
                var dlg = new ObjectResizeDialog(
                    models[models.Count - 1].BoundingBox.Size.x,
                    models[models.Count - 1].BoundingBox.Size.y,
                    models[models.Count - 1].BoundingBox.Size.z);
                if (MainWindow.main.Visibility == Visibility.Visible)
                    dlg.Owner = MainWindow.main;
                dlg.ShowDialog();
                if      (dlg.gIsScale) DoInchOrScale(model, false);
                else if (dlg.gIsInch)  DoInchScale(model);
            }
        }

        // =====================================================================
        //  RemoveModel / RemoveAllSelectedModels
        // =====================================================================
        private void RemoveModel(PrintModel model)
        {
            var row = RowForModel(model);
            if (row != null) listObjects.Items.Remove(row);

            // PrintModel
            for (int i = 0; i < models.Count; i++)
                if (models[i] == model) { models.RemoveAt(i); break; }

            if (model.ListviewGetModels != null)
                model.ListviewGetModels -= ListObjects;

            model.Clear();
            GC.Collect();
        }

        private void RemoveAllSelectedModels()
        {
            foreach (var stl in ListObjects(true).ToList())
                RemoveModel(stl);
            if (models.Count == 0)
                MainWindow.main.OutofBound.Visibility = Visibility.Collapsed;

            MainWindow.main.threeDControl.UpdateChanges();
        }

        public void buttonRemoveSTL_Click(object sender, EventArgs e)
            => RemoveAllSelectedModels();

        private bool IsValidPrintModel(PrintModel model)
            => model.name != "Unknown" &&
               typeof(PrintModel) == model.GetType() &&
               model.Model != null;

        // =====================================================================
        //  updateEnabled
        // =====================================================================
        private void updateEnabled()
        {
            int n = listObjects.SelectedItems.Count;
            if (n != 1)
            {
                textRotX.IsEnabled          = false;
                textRotY.IsEnabled          = false;
                textRotZ.IsEnabled          = false;
                textScaleX.IsEnabled        = false;
                textScaleY.IsEnabled        = false;
                textScaleZ.IsEnabled        = false;
                buttonLockAspect.IsEnabled  = false;
                textTransX.IsEnabled        = false;
                textTransY.IsEnabled        = false;
                textTransZ.IsEnabled        = false;
                if (MainWindow.main.threeDControl != null)
                    MainWindow.main.threeDControl.SetObjectSelected(n > 0);
                panelAnalysis.Visibility = Visibility.Collapsed;
            }
            else
            {
                textRotX.IsEnabled         = true;
                textRotY.IsEnabled         = true;
                textRotZ.IsEnabled         = true;
                textScaleX.IsEnabled       = !LockAspectRatio;
                textScaleY.IsEnabled       = !LockAspectRatio;
                textScaleZ.IsEnabled       = !LockAspectRatio;
                buttonLockAspect.IsEnabled = true;
                textTransX.IsEnabled       = true;
                textTransY.IsEnabled       = true;
                textTransZ.IsEnabled       = true;
                if (MainWindow.main.threeDControl != null)
                    MainWindow.main.threeDControl.SetObjectSelected(true);
                panelAnalysis.Visibility = Visibility.Visible;
                UpdateAnalyserData();
            }
        }

        // =====================================================================
        //  Autoposition
        // =====================================================================
        public bool Autoposition(bool inputByClone = false)
        {
            if (listObjects.Items.Count == 1)
            {
                var row = (ListViewItemModel)listObjects.Items[0];
                row.Model.CenterWOLand(MainWindow.main.PrintAreaWidth / 2, MainWindow.main.PrintAreaDepth / 2);

                if (MainWindow.main.threeDControl != null)
                    MainWindow.main.threeDControl.UpdateChanges();
                return true;
            }

            var packer    = new RectPacker(1, 1);
            var outPacker = new OutRectPacker(1000);
            int border    = 1;
            float maxW = MainWindow.main.PrintAreaWidth, maxH = MainWindow.main.PrintAreaDepth;
            float xOff = 0, yOff = 0;
            outPacker.SetPlatformSize(maxW, maxH);
            bool autosizeFailed = false;

            foreach (var stl in ListObjects(false))
            {
                if (typeof(PrintModel) != stl.GetType()) continue;
                int w = 2 * border + (int)Math.Ceiling(stl.xMax - stl.xMin);
                int h = 2 * border + (int)Math.Ceiling(stl.yMax - stl.yMin);
                if (!packer.addAtEmptySpotAutoGrow(new PackerRect(0, 0, w, h, stl), (int)maxW, (int)maxH))
                {
                    autosizeFailed = true;
                    outPacker.addOutsideSpotAutoGrow(new PackerRect(0, 0, w, h, stl));
                }
            }

            if (autosizeFailed)
            {
                float xCenter   = (2000 - outPacker.w) / 2f;
                float yCenter   = (2000 - outPacker.h) / 2f;
                float xOrigPos  = xOff + xCenter + outPacker.vRects[0].x + border - 1000;
                float yOrigPos  = yOff + yCenter + outPacker.vRects[0].y + border - 1000;
                for (int i = 1; i < outPacker.vRects.Count; i++)
                {
                    var s = (PrintModel)outPacker.vRects[i].obj;
                    s.Position.x += xOff + xCenter + outPacker.vRects[i].x + border - 1000 - xOrigPos - s.xMin;
                    s.Position.y += yOff + yCenter + outPacker.vRects[i].y + border - 1000 - yOrigPos - s.yMin;
                    s.UpdateBoundingBox();
                }
                MessageBox.Show(Trans.T("M_PRINTER_BED_FULL_TEXT"),
                                Trans.T("W_PRINTER_BED_FULL"),
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return false;
            }

            float xAdd = (maxW - packer.w) / 2f;
            float yAdd = (maxH - packer.h) / 2f;
            foreach (PackerRect rect in packer.vRects)
            {
                var s = (PrintModel)rect.obj;
                s.Position.x += xOff + xAdd + rect.x + border - s.xMin;
                s.Position.y += yOff + yAdd + rect.y + border - s.yMin;
                s.UpdateBoundingBox();
            }
            MainWindow.main.threeDControl.UpdateChanges();
            return true;
        }

        // =====================================================================
        //  Event handlers – ListView
        // =====================================================================
        private void listObjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            updateEnabled();
            var list = ListObjects(false);
            var sellist = ListObjects(true);
            PrintModel stl = sellist.Count == 1 ? sellist.First.Value : null;
            foreach (var s in list)
                s.Selected = sellist.Contains(s);

            if (stl != null)
            {
                textRotX.TextChanged -= textRotX_TextChanged;
                textRotY.TextChanged -= textRotY_TextChanged;
                textRotZ.TextChanged -= textRotZ_TextChanged;

                textScaleX.TextChanged -= textScaleX_TextChanged;
                textScaleY.TextChanged -= textScaleY_TextChanged;
                textScaleZ.TextChanged -= textScaleZ_TextChanged;

                textTransX.TextChanged -= textTransX_TextChanged;
                textTransY.TextChanged -= textTransY_TextChanged;
                textTransZ.TextChanged -= textTransZ_TextChanged;

                textRotX.Text = stl.Rotation.x.ToString(GCode.format);
                textRotY.Text = stl.Rotation.y.ToString(GCode.format);
                textRotZ.Text = stl.Rotation.z.ToString(GCode.format);
                LockAspectRatio = stl.Scale.x == stl.Scale.y && stl.Scale.x == stl.Scale.z;
                textScaleX.Text = stl.Scale.x.ToString(GCode.format);
                textScaleY.Text = stl.Scale.y.ToString(GCode.format);
                textScaleZ.Text = stl.Scale.z.ToString(GCode.format);
                textTransX.Text = stl.Position.x.ToString(GCode.format);
                textTransY.Text = stl.Position.y.ToString(GCode.format);
                textTransZ.Text = stl.Position.z.ToString(GCode.format);

                textRotX.TextChanged += textRotX_TextChanged;
                textRotY.TextChanged += textRotY_TextChanged;
                textRotZ.TextChanged += textRotZ_TextChanged;

                textScaleX.TextChanged += textScaleX_TextChanged;
                textScaleY.TextChanged += textScaleY_TextChanged;
                textScaleZ.TextChanged += textScaleZ_TextChanged;

                textTransX.TextChanged += textTransX_TextChanged;
                textTransY.TextChanged += textTransY_TextChanged;
                textTransZ.TextChanged += textTransZ_TextChanged;

                MainWindow.main.UI_object_information.Analyse(stl);
            }

            if (MainWindow.main.threeDControl != null)
                MainWindow.main.threeDControl.UpdateChanges();
        }

        private void listObjects_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Dynamically resize the Name column to consume remaining width
            double usedWidth = columnMesh.Width + columnCollision.Width + columnDelete.Width + SystemParameters.VerticalScrollBarWidth + 6;
            double newWidth = listObjects.ActualWidth - usedWidth;
            if (newWidth > 0) columnName.Width = newWidth;
        }

        // =====================================================================
        //  Event handlers – keyboard
        // =====================================================================
        public void listObjects_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A)
            {
                e.Handled = true;
            }
            else if (e.Key == Key.Q || e.Key == Key.E)
            {
                if (listObjects.Items.Count != 0)
                {
                    if (listObjects.Items.Count == 1)
                    {
                        listObjects.SelectedItem = listObjects.Items[0];
                    }
                    else
                    {
                        if (ListObjects(true).Count > 1)
                            listObjects.SelectedItems.Clear();

                        if (e.Key == Key.Q)
                        {
                            if (ListObjects(true).Count == 0)
                                listObjects.SelectedItem = listObjects.Items[0];
                            else
                            {
                                var act = SingleSelectedModel;
                                if (act != null)
                                {
                                    for (int i = 0; i < listObjects.Items.Count; i++)
                                    {
                                        if (act == ((ListViewItemModel)listObjects.Items[i]).Model)
                                        {
                                            listObjects.SelectedItems.Clear();
                                            listObjects.SelectedItem = listObjects.Items[Math.Max(0, i - 1)];
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        else // Key.E
                        {
                            if (ListObjects(true).Count == 0)
                                listObjects.SelectedItem = listObjects.Items[listObjects.Items.Count - 1];
                            else
                            {
                                var act = SingleSelectedModel;
                                if (act != null)
                                {
                                    for (int i = 0; i < listObjects.Items.Count; i++)
                                    {
                                        if (act == ((ListViewItemModel)listObjects.Items[i]).Model)
                                        {
                                            listObjects.SelectedItems.Clear();
                                            listObjects.SelectedItem = listObjects.Items[Math.Min(listObjects.Items.Count - 1, i + 1)];
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.C)
            {
                cloneModels.Clear();
                cloneModels = GetSelectedPrintModels();
            }
            else if (e.Key == Key.V)
            {
                foreach (var pm in cloneModels) CloneObject(pm);
            }
            else if (e.Key == Key.Delete)
            {
                MainWindow.main.remove_toggleButton_Click(null, null);
                e.Handled = true;
            }
        }

        // =====================================================================
        //  Event handlers – text boxes (Trans / Scale / Rotate)
        // =====================================================================
        private bool _suppressTextEvents = false;

        private void textTransX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float old = stl.Position.x;
            float.TryParse(textTransX.Text, NumberStyles.Float, GCode.format, out stl.Position.x);
            if (Math.Abs(old - stl.Position.x) < 0.001f) return;
            if (typeof(PrintModel) == stl.GetType()) updateSTLState(stl);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textTransY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float old = stl.Position.y;
            float.TryParse(textTransY.Text, NumberStyles.Float, GCode.format, out stl.Position.y);
            if (Math.Abs(old - stl.Position.y) < 0.001f) return;
            if (typeof(PrintModel) == stl.GetType()) updateSTLState(stl);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textTransZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float old = stl.Position.z;
            float.TryParse(textTransZ.Text, NumberStyles.Float, GCode.format, out stl.Position.z);
            if (Math.Abs(old - stl.Position.z) < 0.001f) return;
            updateSTLState(stl);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textScaleX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float.TryParse(textScaleX.Text, NumberStyles.Float, GCode.format, out stl.Scale.x);
            if (LockAspectRatio) { stl.Scale.y = stl.Scale.z = stl.Scale.x; SyncScaleFields(stl); }
            updateSTLState(stl);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textScaleY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float.TryParse(textScaleY.Text, NumberStyles.Float, GCode.format, out stl.Scale.y);
            updateSTLState(stl);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textScaleZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float old = stl.Scale.z;
            float.TryParse(textScaleZ.Text, NumberStyles.Float, GCode.format, out stl.Scale.z);
            stl.UpdateBoundingBox();
            if (old != stl.Scale.z) stl.LandUpdateBBNoPreUpdate();
            updateSTLState(stl);

            MainWindow.main.threeDControl.UpdateChanges();
        }

        public void textRotX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float old = stl.Rotation.x;
            float.TryParse(textRotX.Text, NumberStyles.Float, GCode.format, out stl.Rotation.x);
            stl.ForceViewRegeneration();
            if (Math.Abs(old - stl.Rotation.x) < 0.001f) return;
            updateSTLState(stl);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textRotY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float old = stl.Rotation.y;
            float.TryParse(textRotY.Text, NumberStyles.Float, GCode.format, out stl.Rotation.y);
            stl.ForceViewRegeneration();
            if (Math.Abs(old - stl.Rotation.y) < 0.001f) return;
            updateSTLState(stl);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textRotZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float old = stl.Rotation.z;
            float.TryParse(textRotZ.Text, NumberStyles.Float, GCode.format, out stl.Rotation.z);
            stl.ForceViewRegeneration();
            if (Math.Abs(old - stl.Rotation.z) < 0.001f) return;
            if (typeof(PrintModel) == stl.GetType()) updateSTLState(stl);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        /// <summary>
        /// Helper: update Scale text fields without triggering cascade TextChanged events.
        /// </summary>
        private void SyncScaleFields(PrintModel stl)
        {
            _suppressTextEvents = true;
            textScaleY.Text = stl.Scale.y.ToString(GCode.format);
            textScaleZ.Text = stl.Scale.z.ToString(GCode.format);
            _suppressTextEvents = false;
        }

        // =====================================================================
        //  Event handlers – buttons
        // =====================================================================
        private void buttonLockAspect_Click(object sender, RoutedEventArgs e)
        {
            LockAspectRatio = !LockAspectRatio;
            if (LockAspectRatio) textScaleX_TextChanged(null, null);
        }

        private void buttonRemoveObject_Click(object sender, RoutedEventArgs e)
        {
            var btn   = (System.Windows.Controls.Button)sender;
            var model = (PrintModel)btn.Tag;
            RemoveModel(model);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        // =====================================================================
        //  objectMoved / objectSelected  (called from ThreeDControl)
        // =====================================================================
        public void ObjectMoved(float dx, float dy)
        {
            foreach (var stl in ListObjects(true))
            {
                if (stl.Position.x + dx < MainWindow.main.PrintAreaWidth * 1.2f && stl.Position.x + dx > -MainWindow.main.PrintAreaWidth * 0.2f) stl.Position.x += dx;
                if (stl.Position.y + dy < MainWindow.main.PrintAreaDepth * 1.2f && stl.Position.y + dy > -MainWindow.main.PrintAreaDepth * 0.2f) stl.Position.y += dy;
                if (listObjects.SelectedItems.Count == 1)
                {
                    _suppressTextEvents = true;
                    textTransX.Text = stl.Position.x.ToString(GCode.format);
                    textTransY.Text = stl.Position.y.ToString(GCode.format);
                    _suppressTextEvents = false;
                }
                updateSTLState(stl);
            }
            MainWindow.main.threeDControl.UpdateChanges();
        }

        public void ObjectSelected(ThreeDModel sel)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                if (!sel.Selected) SetObjectSelected((PrintModel)sel, true);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                SetObjectSelected((PrintModel)sel, !sel.Selected);
            }
            else
            {
                listObjects.SelectedItems.Clear();
                SetObjectSelected((PrintModel)sel, true);
            }
        }

        // =====================================================================
        //  Scale helpers (DoInchScale / DoInchOrScale / DoInchtomm / SetSliderBar)
        //  Logic unchanged – only MessageBox calls converted to WPF API.
        // =====================================================================
        public void DoInchScale(PrintModel stl)
        {
            try
            {
                var ui = MainWindow.main.UI_resize_advance;
                ui.button_mmtoinch.IsEnabled = false;
                ui.button_inchtomm.IsEnabled = true;
                ui.chk_Uniform.IsChecked     = true;
                var bbox = stl.BoundingBoxWOSupport;
                ui.bboxnow = bbox.Size.x / Convert.ToDouble(textScaleX.Text);
                ui.bboynow = bbox.Size.y / Convert.ToDouble(textScaleY.Text);
                ui.bboznow = bbox.Size.z / Convert.ToDouble(textScaleZ.Text);
                Double tempX = ObjectResizeDialog.scaleInchx;
                Double tempY = ObjectResizeDialog.scaleInchy;
                Double tempZ = ObjectResizeDialog.scaleInchz;
                textScaleX.Text = (tempX / ui.bboxnow).ToString("0.000");
                textScaleY.Text = (tempY / ui.bboynow).ToString("0.000");
                textScaleZ.Text = (tempZ / ui.bboznow).ToString("0.000");
                ui.gIsShow = true;
                ui.dimX    = bbox.Size.x; ui.updateTxt(Enums.Axis.X);
                ui.dimY    = bbox.Size.y; ui.updateTxt(Enums.Axis.Y);
                ui.dimZ    = bbox.Size.z; ui.updateTxt(Enums.Axis.Z);
                ui.chk_Uniform_Checked(null, null);
                ui.gIsShow = false;
                updateSTLState(stl);
                stl.LandToZ(0);
                MainWindow.main.threeDControl.UpdateChanges();
            }
            catch { }
        }

        public void SetSliderBar(PrintModel stl)
        {
            try
            {
                var ui   = MainWindow.main.UI_resize_advance;
                var bbox = stl.BoundingBoxWOSupport;
                ui.bboxnow = bbox.Size.x / Convert.ToDouble(textScaleX.Text);
                ui.bboynow = bbox.Size.y / Convert.ToDouble(textScaleY.Text);
                ui.bboznow = bbox.Size.z / Convert.ToDouble(textScaleZ.Text);
                ui.chk_Uniform_Checked(null, null);
            }
            catch { }
        }

        private bool AskUserToChangeUnit()
        {
            var sb = new StringBuilder(Trans.T("M_RESIZE_MODEL_TOO_BIG")).AppendLine()
                                                                         .Append(Trans.T("M_RESIZE_ASK_TO_SCALE_UP"));
            return System.Windows.MessageBox.Show(sb.ToString(),
                                   Trans.T("M_RESIZE_SCALE_UP_TITLE"),
                                   MessageBoxButton.YesNo,
                                   MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        public void DoInchOrScale(PrintModel stl, bool pIsInch)
        {
            try
            {
                var ui   = MainWindow.main.UI_resize_advance;
                var bbox = stl.BoundingBoxWOSupport;
                ui.chk_Uniform.IsChecked = true;
                ui.bboxnow = bbox.Size.x / Convert.ToDouble(textScaleX.Text);
                ui.bboynow = bbox.Size.y / Convert.ToDouble(textScaleY.Text);
                ui.bboznow = bbox.Size.z / Convert.ToDouble(textScaleZ.Text);

                string tDecision =
                    (stl.BoundingBox.Size.x >= stl.BoundingBox.Size.y && stl.BoundingBox.Size.x >= stl.BoundingBox.Size.z) ? "x" :
                    (stl.BoundingBox.Size.y >= stl.BoundingBox.Size.x && stl.BoundingBox.Size.y >= stl.BoundingBox.Size.z) ? "y" : "z";

                if (!pIsInch)
                {
                    ui.button_mmtoinch.IsEnabled = true;
                    ui.button_inchtomm.IsEnabled = false;
                    ui.gIsShow = true;
                    ui.dimX    = 24; ui.updateTxt(Enums.Axis.X);
                    ui.dimY    = 24; ui.updateTxt(Enums.Axis.Y);
                    ui.dimZ    = 24; ui.updateTxt(Enums.Axis.Z);
                    ui.gIsShow = false;
                    switch (tDecision)
                    {
                        case "x": ui.dimX = ObjectResizeDialog.scaleMMx; ui.updateTxt(Enums.Axis.X); break;
                        case "y": ui.dimY = ObjectResizeDialog.scaleMMy; ui.updateTxt(Enums.Axis.Y); break;
                        case "z": ui.dimZ = ObjectResizeDialog.scaleMMz; ui.updateTxt(Enums.Axis.Z); break;
                    }
                    SetSliderBar(stl);
                }
                else
                {
                    ui.chk_Uniform.IsChecked = true;
                    double tempX = bbox.Size.x * 25.4, tempY = bbox.Size.y * 25.4, tempZ = bbox.Size.z * 25.4;
                    if (tempX > MainWindow.main.PrintAreaWidth || tempY > MainWindow.main.PrintAreaDepth || tempZ > MainWindow.main.PrintAreaHeight)
                    {
                        if (!AskUserToChangeUnit())
                        {
                            ui.button_mmtoinch.IsEnabled = true;
                            ui.button_inchtomm.IsEnabled = false;
                            return;
                        }
                    }
                    ui.gIsShow = true;
                    ui.dimX    = bbox.Size.x; ui.updateTxt(Enums.Axis.X, false);
                    ui.dimY    = bbox.Size.y; ui.updateTxt(Enums.Axis.Y, false);
                    ui.dimZ    = bbox.Size.z; ui.updateTxt(Enums.Axis.Z, false);
                    ui.chk_Uniform_Checked(null, null);
                    ui.gIsShow = false;
                    textScaleX.Text = (tempX / ui.bboxnow).ToString("0.000");
                    textScaleY.Text = (tempY / ui.bboynow).ToString("0.000");
                    textScaleZ.Text = (tempZ / ui.bboznow).ToString("0.000");
                    updateSTLState(stl);
                    stl.LandUpdateBBNoPreUpdate();
                    MainWindow.main.threeDControl.UpdateChanges();
                }
            }
            catch { }
        }

        public void DoInchtomm(PrintModel stl)
        {
            try
            {
                var ui   = MainWindow.main.UI_resize_advance;
                var bbox = stl.BoundingBoxWOSupport;
                ui.chk_Uniform.IsChecked = true;
                double tempX = bbox.Size.x / 25.4, tempY = bbox.Size.y / 25.4, tempZ = bbox.Size.z / 25.4;
                ui.gIsShow = true;
                ui.dimX    = tempX; ui.updateTxt(Enums.Axis.X, false);
                ui.dimY    = tempY; ui.updateTxt(Enums.Axis.Y, false);
                ui.dimZ    = tempZ; ui.updateTxt(Enums.Axis.Z, false);
                ui.chk_Uniform_Checked(null, null);
                ui.gIsShow = false;
                textScaleX.Text = (tempX / ui.bboxnow).ToString("0.000");
                textScaleY.Text = (tempY / ui.bboynow).ToString("0.000");
                textScaleZ.Text = (tempZ / ui.bboznow).ToString("0.000");
                updateSTLState(stl);
                stl.LandUpdateBBNoPreUpdate();
                MainWindow.main.threeDControl.UpdateChanges();
            }
            catch { }
        }

        // =====================================================================
        //  Static icon loader
        // =====================================================================
        private ImageSource[] LoadIcons()
        {
            // Load embedded resource icons.
            // Adjust the pack URIs to match your project's resource paths.
            string[] names = { "unlock16.png", "lock16.png", "ok16.png", "bad16.png", "trash16.png" };
            var images = new ImageSource[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    var uri = new Uri($"pack://application:,,,/OpenGL3DViewerNET10;component/Resources/{names[i]}");
                    images[i] = new System.Windows.Media.Imaging.BitmapImage(uri);
                }
                catch
                {
                    images[i] = null; // graceful fallback if resource is missing
                }
            }
            return images;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true; // Prevent the window from actually closing
            this.Hide();
        }
    }
}
