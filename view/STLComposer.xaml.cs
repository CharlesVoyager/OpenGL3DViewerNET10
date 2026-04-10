using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenGL3DViewerNET10.ModelLib.model;
using OpenGL3DViewerNET10.ModelLib.Utils;
using OpenGL3DViewerNET10.MeshIOLib;

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
                if (MainWindow.main != null)
                    MainWindow.main.languageChanged += translate;
            }
            catch { }
        }

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
                Name                 = model.Name,
                Model                = model,
                //MeshStatusImage      = _icons[2],                                 // _icons[2]: V, _icons[3]: X
                //CollisionStatusImage = _icons[model.outside        ? 3 : 2],
            };
        }

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

        void updateAnalyserData()
        {
            PrintModel model = SingleSelectedModel;
            if (model == null) return;

            txtOriginalModelSize.Text = "(" + model.Model.boundingBox.Size.x.ToString("0.000") + ", " +
                                              model.Model.boundingBox.Size.y.ToString("0.000") + "," +
                                              model.Model.boundingBox.Size.z.ToString("0.000") + ")";
            labelVertices.Text             = "(To be implemented)";
            txtTriangles.Text            = model.Model.triangles.Count.ToString();

            // Colour: black when zero, red when non-zero
            var red   = new SolidColorBrush(Colors.Red);
            var black = new SolidColorBrush(Colors.Black);
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

        public static readonly ManualResetEventSlim _meshDataReady = new ManualResetEventSlim(true);
        public async void OpenAndAddObject(string file)
        {
            if (MainWindow.main == null) return;

            listObjects.SelectedItems.Clear();
            PrintModel newModel = new PrintModel();
            bool modelToLand    = true;
            var  modelIO        = new MeshIOWrapper();
            MainWindow.main.BusyWindow.EnableBusyWindow();
            _meshDataReady.Reset();
            // Offload heavy work to background thread — UI thread is free immediately
            await Task.Run(() =>
            {
                try
                {
                    modelIO.LoadWOCatch(file, newModel.Model);
                }
                catch (Exception)
                {
                    MessageBox.Show("Error: " + Trans.T("M_LOAD_FILE_FAIL"));
                    return;
                }

                // NOTES:
                // 1. Model (TopoModel): Original STL file triangles data.
                // 2. Mesh (Submesh): Centerized triangles data. 
                newModel.ModelToMesh();

                // NOTES:
                // 1. Auto position needs bounding box information.
                // 2. Current bounding box is for orignal STL data. 
                newModel.CopyTopoModelBoundingBoxToPrintModel();

                _meshDataReady.Set();
                Console.WriteLine("LoadWOCatch Done.");
            });
            MainWindow.main.BusyWindow.DisableBusyWindow();
            if (_meshDataReady.Wait(0) == false)// It means some expection happens when loading a STL file.
            {
                _meshDataReady.Set();
                return; 
            }
            newModel.Name = Path.GetFileName(file);

            if (MainWindow.main.BusyWindow.killed || newModel.Model.triangles.Count == 0)
            {
                return;
            }
            newModel.Position.z = (float)(newModel.BoundingBox.Size.z / 2);
            if (modelToLand)
            {
                // NOTE: Autoposition needs xMin and yMin data. Therefore, centering BoundingBox first to avoid some STL files with large xMin/yMin values being placed outside the printer bed.
                newModel.BoundingBox.Centerized();
                Autoposition(newModel);
            }
            else
            {
                newModel.Position.x = (float)newModel.BoundingBox.Center.x;
                newModel.Position.y = (float)newModel.BoundingBox.Center.y;
                newModel.UpdateBoundingBoxAndMatrix();
            }
            
            double xxx = newModel.BoundingBox.Size.x * newModel.BoundingBox.Size.y * 0.001; // Don't use z size here because some STL files may have very small z size but large x/y size, and they should not be considered as "too small".

            if (xxx < 0.1)  // the object is too small.
            {
                if (newModel.Name.Contains(".glb"))
                {
                    DoAutoScale(newModel);
                }
                else
                {
                    var dlg = new ObjectResizeDialog(
                        newModel.BoundingBox.Size.x,
                        newModel.BoundingBox.Size.y,
                        newModel.BoundingBox.Size.z);
                    if (MainWindow.main.Visibility == Visibility.Visible)
                        dlg.Owner = MainWindow.main;
                    dlg.ShowDialog();
                    if (dlg.gIsScale) DoAutoScale(newModel);
                    else if (dlg.gIsInch) DoMmToInch(newModel);
                }
            }
            else if (newModel.BoundingBox.Size.x - 1e-4 > Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaWidth)  ||
                     newModel.BoundingBox.Size.y - 1e-4 > Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaDepth)  ||
                     Math.Floor(newModel.BoundingBox.Size.z * 1000) / 1000 > Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaHeight))  // the object is too big.
            {
                double tXBound = newModel.BoundingBox.Size.x / Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaWidth);
                double tYBound = newModel.BoundingBox.Size.y / Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaDepth);
                double tZBound = newModel.BoundingBox.Size.z / Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaHeight);
                double tMax    = Math.Max(Math.Max(tXBound, tYBound), Math.Max(tYBound, tZBound));
                float  scaleValue = 0;

                if      (tMax == tXBound) scaleValue = (float)(Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaWidth) / newModel.BoundingBox.Size.x) * 100;
                else if (tMax == tYBound) scaleValue = (float)(Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaDepth) / newModel.BoundingBox.Size.y) * 100;
                else if (tMax == tZBound) scaleValue = (float)(Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaHeight) / newModel.BoundingBox.Size.z) * 100;

                var result = MessageBox.Show(
                    Trans.T("M_OBJ_SCALE_DOWN") + " " + (int)scaleValue + "%",
                    Trans.T("W_OBJ_TOO_LARGE"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        tXBound = newModel.BoundingBox.Size.x / Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaWidth);
                        tYBound = newModel.BoundingBox.Size.y / Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaDepth);
                        tZBound = newModel.BoundingBox.Size.z / Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaHeight);
                        tMax    = Math.Max(Math.Max(tXBound, tYBound), Math.Max(tYBound, tZBound));

                        if      (tMax == tXBound)
                        { newModel.Scale.x = newModel.Scale.y = newModel.Scale.z = (float)(Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaWidth) / newModel.BoundingBox.Size.x); }
                        else if (tMax == tYBound)
                        { newModel.Scale.y = newModel.Scale.x = newModel.Scale.z = (float)(Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaDepth) / newModel.BoundingBox.Size.y); }
                        else if (tMax == tZBound)
                        { newModel.Scale.z = newModel.Scale.x = newModel.Scale.y = (float)(Convert.ToDouble(MainWindow.main.threeDSettings.PrintAreaHeight) / newModel.BoundingBox.Size.z); }

                        MainWindow.main.UI_move.button_land_Click(null, null);
                        Autoposition(newModel);
                    }
                    catch { }
                }
            }

            // Remember initial positions for all models after Autoposition.
            foreach (var m in models)
            {
                m.Position.inix = m.Position.x;
                m.Position.iniy = m.Position.y;
                m.Position.iniz = m.Position.z;
            }

            newModel.Position.inix = newModel.Position.x;
            newModel.Position.iniy = newModel.Position.y;
            newModel.Position.iniz = newModel.Position.z;

            // Added object to the list and updated the TextBox controls.
            AddObject(newModel);

            MainWindow.main.threeDControl.InvokeGL(() =>
            {
                newModel.Drawer.Init();
                models.Add(newModel);
                MainWindow.main.threeDControl.UpdateChanges();
            });
        }

        // =====================================================================
        //  CloneObject
        // =====================================================================
        private bool CloneObject(PrintModel model)
        {
            PrintModel newModel = (PrintModel)model.cloneWithModel();
            Autoposition(newModel);

            listObjects.Items.Add(BuildRow(newModel));
            listObjects.SelectedItems.Clear();
            SetObjectSelected(newModel, true);
            UpdateOutOfBound();

            MainWindow.main.threeDControl.InvokeGL(() =>
            {
                newModel.Drawer.Init();
                models.Add(newModel);
            });
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
        private bool pointInPrintArea(float x, float y, float z)
        {
            double epsilon = 1e-4; // 0.0001

            if (z < -0.1 || z > MainWindow.main.threeDSettings.PrintAreaHeight)
                return false;

            if (x < -epsilon || x > MainWindow.main.threeDSettings.PrintAreaWidth + epsilon) return false;
            if (y < -epsilon || y > MainWindow.main.threeDSettings.PrintAreaDepth + epsilon) return false;

            return true;
        }
        public void UpdateOutOfBound()
        {
            var testList  = ListObjects(false);

            bool allModelsInside = true;
            foreach (var stl in testList)
            {
                stl.outside = false;
                if (    !pointInPrintArea(stl.xMin, stl.yMin, stl.zMin) ||
                        !pointInPrintArea(stl.xMax, stl.yMin, stl.zMin) ||
                        !pointInPrintArea(stl.xMin, stl.yMax, stl.zMin) ||
                        !pointInPrintArea(stl.xMax, stl.yMax, stl.zMin) ||
                        !pointInPrintArea(stl.xMin, stl.yMin, stl.zMax) ||
                        !pointInPrintArea(stl.xMax, stl.yMin, stl.zMax) ||
                        !pointInPrintArea(stl.xMin, stl.yMax, stl.zMax) ||
                        !pointInPrintArea(stl.xMax, stl.yMax, stl.zMax))
                {
                    stl.outside = true;
                    allModelsInside = false;
                }
            }

            MainWindow.main.OutofBound.Visibility = allModelsInside ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RefreshAllRows()
        {
            foreach (var row in AllRows().ToList())
            {
                int idx = listObjects.Items.IndexOf(row);
                if (idx < 0) continue;
                row.CollisionStatusImage = _icons[row.Model.outside ? 3 : 2];
                row.MeshStatusImage      = _icons[2];
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
                if (dlg.gIsScale) DoAutoScale(model);
                else if (dlg.gIsInch) DoMmToInch(model);
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

            model.Clear();
        }

        private void RemoveAllSelectedModels()
        {
            foreach (var stl in ListObjects(true).ToList())
                RemoveModel(stl);

            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        public void buttonRemoveSTL_Click(object sender, EventArgs e) => RemoveAllSelectedModels();

        private bool IsValidPrintModel(PrintModel model)
            => model.Name != "Unknown" &&
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
                textTransX.IsEnabled        = false;
                textTransY.IsEnabled        = false;
                textTransZ.IsEnabled        = false;

                MainWindow.main.setbuttonVisable(listObjects.SelectedItems.Count == 1 && (n>0));

                panelAnalysis.Visibility = Visibility.Collapsed;
            }
            else
            {
                textRotX.IsEnabled         = true;
                textRotY.IsEnabled         = true;
                textRotZ.IsEnabled         = true;
                textScaleX.IsEnabled       = true;
                textScaleY.IsEnabled       = true;
                textScaleZ.IsEnabled       = true;
                textTransX.IsEnabled       = true;
                textTransY.IsEnabled       = true;
                textTransZ.IsEnabled       = true;

                MainWindow.main.setbuttonVisable(listObjects.SelectedItems.Count == 1);

                panelAnalysis.Visibility = Visibility.Visible;
                updateAnalyserData();
            }
        }

        // =====================================================================
        //  Autoposition
        // =====================================================================
        bool Autoposition(PrintModel newModel)
        {
            List<PrintModel> allModels = new List<PrintModel>();
            foreach (var m in models)
                allModels.Add(m);

            allModels.Add(newModel);

            if (allModels.Count == 1)
            {
                var model = allModels[0];
                float x = MainWindow.main.threeDSettings.PrintAreaWidth / 2;
                float y = MainWindow.main.threeDSettings.PrintAreaDepth / 2;

                model.Position.x = x;
                model.Position.y = y;
                model.UpdateBoundingBoxAndMatrix();
                return true;
            }

            var packer    = new RectPacker(1, 1);
            var outPacker = new OutRectPacker(1000);
            int border    = 1;
            float maxW = MainWindow.main.threeDSettings.PrintAreaWidth, maxH = MainWindow.main.threeDSettings.PrintAreaDepth;
            float xOff = 0, yOff = 0;
            outPacker.SetPlatformSize(maxW, maxH);
            bool autosizeFailed = false;

            foreach (var stl in allModels)
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
                    s.UpdateBoundingBoxAndMatrix();
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
                s.UpdateBoundingBoxAndMatrix();
            }
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

                textTransX.Text = stl.Position.x.ToString("0.000");
                textTransY.Text = stl.Position.y.ToString("0.000");
                textTransZ.Text = stl.Position.z.ToString("0.000");

                textScaleX.Text = stl.Scale.x.ToString("0.000");
                textScaleY.Text = stl.Scale.y.ToString("0.000");
                textScaleZ.Text = stl.Scale.z.ToString("0.000");

                textRotX.Text = stl.Rotation.x.ToString("0");
                textRotY.Text = stl.Rotation.y.ToString("0");
                textRotZ.Text = stl.Rotation.z.ToString("0");

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
            float.TryParse(textTransX.Text, out stl.Position.x);
            if (Math.Abs(old - stl.Position.x) < 0.001f) return;
            stl.UpdateTransMatrix();
            stl.UpdateBoundingBoxByShift(stl.Position.x - old, 0, 0);
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textTransY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float old = stl.Position.y;
            float.TryParse(textTransY.Text, out stl.Position.y);
            if (Math.Abs(old - stl.Position.y) < 0.001f) return;
            stl.UpdateTransMatrix();
            stl.UpdateBoundingBoxByShift(0, stl.Position.y - old, 0);
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textTransZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float old = stl.Position.z;
            float.TryParse(textTransZ.Text, out stl.Position.z);
            if (Math.Abs(old - stl.Position.z) < 0.001f) return;
            stl.UpdateTransMatrix();
            stl.UpdateBoundingBoxByShift(0, 0, stl.Position.z - old);
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textScaleX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float.TryParse(textScaleX.Text, out stl.Scale.x);
            stl.UpdateBoundingBoxAndMatrix();
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textScaleY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float.TryParse(textScaleY.Text, out stl.Scale.y);
            stl.UpdateBoundingBoxAndMatrix();
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textScaleZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float old = stl.Scale.z;
            float.TryParse(textScaleZ.Text, out stl.Scale.z);
            if (old != stl.Scale.z) stl.Land();
            stl.UpdateBoundingBoxAndMatrix();
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        public void textRotX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float oriZmin = stl.zMin;
            float old = stl.Rotation.x;
            float.TryParse(textRotX.Text, out stl.Rotation.x);
            if (Math.Abs(old - stl.Rotation.x) < 0.001f) return;
            stl.UpdateBoundingBoxAndMatrix();
            stl.LandToMinZ(oriZmin);
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textRotY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float oriZmin = stl.zMin;
            float old = stl.Rotation.y;
            float.TryParse(textRotY.Text, out stl.Rotation.y);
            if (Math.Abs(old - stl.Rotation.y) < 0.001f) return;
            stl.UpdateBoundingBoxAndMatrix();
            stl.LandToMinZ(oriZmin);
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textRotZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float oriZmin = stl.zMin;
            float old = stl.Rotation.z;
            float.TryParse(textRotZ.Text, out stl.Rotation.z);
            if (Math.Abs(old - stl.Rotation.z) < 0.001f) return;
            stl.UpdateBoundingBoxAndMatrix();
            stl.LandToMinZ(oriZmin);
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        // =====================================================================
        //  Event handlers – buttons
        // =====================================================================
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
                float oldX = stl.Position.x;
                float oldY = stl.Position.y;

                if (stl.Position.x + dx < MainWindow.main.threeDSettings.PrintAreaWidth * 1.2f && stl.Position.x + dx > -MainWindow.main.threeDSettings.PrintAreaWidth * 0.2f) 
                    stl.Position.x += dx;
                if (stl.Position.y + dy < MainWindow.main.threeDSettings.PrintAreaDepth * 1.2f && stl.Position.y + dy > -MainWindow.main.threeDSettings.PrintAreaDepth * 0.2f) 
                    stl.Position.y += dy;
                if (listObjects.SelectedItems.Count == 1)
                {
                    _suppressTextEvents = true;
                    textTransX.Text = stl.Position.x.ToString("0.000");
                    textTransY.Text = stl.Position.y.ToString("0.000");
                    _suppressTextEvents = false;
                }
                stl.UpdateTransMatrix();
                stl.UpdateBoundingBoxByShift(stl.Position.x - oldX, stl.Position.y - oldY, 0);
                UpdateOutOfBound();
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

        private bool AskUserToChangeUnit()
        {
            var sb = new StringBuilder(Trans.T("M_RESIZE_MODEL_TOO_BIG")).AppendLine()
                                                                         .Append(Trans.T("M_RESIZE_ASK_TO_SCALE_UP"));
            return System.Windows.MessageBox.Show(sb.ToString(),
                                   Trans.T("M_RESIZE_SCALE_UP_TITLE"),
                                   MessageBoxButton.YesNo,
                                   MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        // Auto scale the model to fit the half printer bed in X axis on the largest dimension.
        public void DoAutoScale(PrintModel stl)
        {
            try
            {
                var bbox = stl.BoundingBox;

                // Find the largest dimension of the model.
                double maxDim = Math.Max(Math.Max(bbox.Size.x, bbox.Size.y), bbox.Size.z);
                double scaleFactor = (MainWindow.main.threeDSettings.PrintAreaWidth / 2) / maxDim;

                stl.Scale.x = (float)scaleFactor;
                stl.Scale.y = (float)scaleFactor;
                stl.Scale.z = (float)scaleFactor;

                stl.UpdateBoundingBoxAndMatrix();
                stl.Land();
                UpdateOutOfBound();
                MainWindow.main.threeDControl.UpdateChanges();
            }
            catch { }
        }

        public void DoMmToInch(PrintModel stl)
        {
            try
            {
                var ui   = MainWindow.main.UI_resize_advance;
                ui.button_mmtoinch.IsEnabled = false;
                ui.button_inchtomm.IsEnabled = true;
                var bbox = stl.BoundingBox;
                ui.chk_Uniform.IsChecked = true;
                ui.bboxnow = bbox.Size.x / stl.Scale.x;
                ui.bboynow = bbox.Size.y / stl.Scale.y;
                ui.bboznow = bbox.Size.z / stl.Scale.z;

                ui.chk_Uniform.IsChecked = true;
                double tempX = bbox.Size.x * 25.4, tempY = bbox.Size.y * 25.4, tempZ = bbox.Size.z * 25.4;
                if (tempX > MainWindow.main.threeDSettings.PrintAreaWidth || 
                    tempY > MainWindow.main.threeDSettings.PrintAreaDepth || 
                    tempZ > MainWindow.main.threeDSettings.PrintAreaHeight)
                {
                    if (!AskUserToChangeUnit())
                    {
                        ui.button_mmtoinch.IsEnabled = true;
                        ui.button_inchtomm.IsEnabled = false;
                        return;
                    }
                }
                ui.gIsShow = true;
                ui.dimX    = bbox.Size.x;
                ui.dimY    = bbox.Size.y;
                ui.dimZ    = bbox.Size.z;
                ui.updateTxt();
                ui.chk_Uniform_Checked(null, null);
                ui.gIsShow = false;
                stl.Scale.x = (float)(tempX / ui.bboxnow);
                stl.Scale.y = (float)(tempY / ui.bboynow);

                if (ui.bboznow == 0)
                    stl.Scale.z = 1;    // Consider the model is 2D without z hight.
                else
                    stl.Scale.z = (float)(tempZ / ui.bboznow);

                stl.UpdateBoundingBoxAndMatrix();
                stl.Land();
                UpdateOutOfBound();
                MainWindow.main.threeDControl.UpdateChanges();
            }
            catch { }
        }

        public void DoInchtomm(PrintModel stl)
        {
            try
            {
                var ui   = MainWindow.main.UI_resize_advance;
                var bbox = stl.BoundingBox;
                ui.chk_Uniform.IsChecked = true;
                double tempX = bbox.Size.x / 25.4, tempY = bbox.Size.y / 25.4, tempZ = bbox.Size.z / 25.4;
                ui.gIsShow = true;
                ui.dimX    = tempX;
                ui.dimY    = tempY;
                ui.dimZ    = tempZ;
                ui.updateTxt();
                ui.chk_Uniform_Checked(null, null);
                ui.gIsShow = false;
                textScaleX.Text = (tempX / ui.bboxnow).ToString("0.000");
                textScaleY.Text = (tempY / ui.bboynow).ToString("0.000");
                textScaleZ.Text = (tempZ / ui.bboznow).ToString("0.000");
                UpdateOutOfBound();
                stl.Land();
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
