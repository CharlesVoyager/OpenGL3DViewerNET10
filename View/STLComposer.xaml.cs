using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenGL3DViewerNET10.ModelLib.model;
using OpenGL3DViewerNET10.ModelLib.Utils;
using OpenGL3DViewerNET10.MeshIOLib;
using View3D.model.geom;

#nullable disable

namespace View3D.view
{
    /// <summary>
    /// Wrapper that gives the WPF ListView an observable item to bind against.
    /// </summary>
    public class ListViewItemModel
    {
        public string         Name              { get; set; }
        public ThreeDModel    Model             { get; set; }
        public ImageSource    MeshStatusImage   { get; set; }
        public ImageSource    CollisionStatusImage { get; set; }
    }

    public partial class STLComposer : Window
    {
        public List<ThreeDModel> models     = new List<ThreeDModel>();

        // ── Private fields ────────────────────────────────────────────────────
        private List<ThreeDModel> cloneModels = new List<ThreeDModel>();

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

        private ListViewItemModel RowForModel(ThreeDModel model)
            => AllRows().FirstOrDefault(r => r.Model == model);

        // ── Add / remove rows ─────────────────────────────────────────────────
        private void AddObject(ThreeDModel model)
        {
            var row = BuildRow(model);
            listObjects.Items.Add(row);
            SetObjectSelected(model, true);
        }

        private ListViewItemModel BuildRow(ThreeDModel model)
        {
            return new ListViewItemModel
            {
                Name                 = model.Name,
                Model                = model,
                //MeshStatusImage      = _icons[2],                                 // _icons[2]: V, _icons[3]: X
                //CollisionStatusImage = _icons[model.outside        ? 3 : 2],
            };
        }

        public LinkedList<ThreeDModel> ListObjects(bool selected)
        {
            var list = new LinkedList<ThreeDModel>();
            if (selected)
                foreach (var row in SelectedRows()) list.AddLast(row.Model);
            else
                foreach (var row in AllRows())      list.AddLast(row.Model);
            return list;
        }

        public ThreeDModel SingleSelectedModel { get; set; } = null;

        void updateAnalyserData()
        {
            ThreeDModel model = SingleSelectedModel;
            if (model == null) return;

            txtOriginalModelSize.Text = "(" + model.Model.boundingBox.Size.x.ToString("0.000") + ", " +
                                              model.Model.boundingBox.Size.y.ToString("0.000") + "," +
                                              model.Model.boundingBox.Size.z.ToString("0.000") + ")";
            labelVertices.Text             = "(To be implemented)";
            txtTriangles.Text            = model.Model.drawTriangles.Count.ToString();

            // Colour: black when zero, red when non-zero
            var red   = new SolidColorBrush(Colors.Red);
            var black = new SolidColorBrush(Colors.Black);
        }

        public void SetObjectSelected(ThreeDModel model, bool select)
        {
            var row = RowForModel(model);
            if (row == null) return;
            if (select)
            {
                if (!listObjects.SelectedItems.Contains(row))
                    listObjects.SelectedItems.Add(row);

                SingleSelectedModel = model;
            }
            else
            {
                listObjects.SelectedItems.Remove(row);

                if (SingleSelectedModel == model)
                    SingleSelectedModel = null;
            }
        }

        public void RemoveAllObject()
        {
            foreach (var row in AllRows().ToList())
                if (row.Model.GetType() == typeof(ThreeDModel))
                    SetObjectSelected(row.Model, true);
            buttonRemoveSTL_Click(null, null);
        }

        public void RemoveLastModel()
        {
            if (0 == models.Count) return;
            int idx = models.Count - 1;
            while (idx >= 0)
            {
                if (typeof(ThreeDModel) == models[idx].GetType() && null != models[idx].Model)
                {
                    RemoveModel(models[idx]);
                    return;
                }
                idx--;
            }
        }

        public List<ThreeDModel> GetAllPrintModels()
        {
            var list = new List<ThreeDModel>();
            foreach (var m in models)
                if (IsValidPrintModel(m)) list.Add(m);
            return list;
        }

        public List<ThreeDModel> GetSelectedPrintModels()
        {
            var list = new List<ThreeDModel>();
            foreach (var m in models)
                if (IsValidPrintModel(m) && m.Selected) list.Add(m);
            return list;
        }

        bool isTooSmall(RHBoundingBox boundingBox)
        {
            // Don't use z size here because some STL files may have very small z size but large x/y size, and they should not be considered as "too small".
            return (boundingBox.Size.x < 10 && boundingBox.Size.y < 10 && boundingBox.Size.z < 10); 
        }

        bool isTooBig(RHBoundingBox boundingBox)
        {
            return (boundingBox.Size.x - 1e-4 > SettingsService.Instance.Settings.PrintAreaWidth) ||
                   (boundingBox.Size.y - 1e-4 > SettingsService.Instance.Settings.PrintAreaDepth) ||
                   (Math.Floor(boundingBox.Size.z * 1000) / 1000 > SettingsService.Instance.Settings.PrintAreaHeight);
        }

        public static readonly ManualResetEventSlim _meshDataReady = new ManualResetEventSlim(true);

        public async void OpenAndAddObject(string file)
        {
            if (MainWindow.main == null) return;

            listObjects.SelectedItems.Clear();
            ThreeDModel newModel = new ThreeDModel();
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
            if (MainWindow.main.BusyWindow.killed || newModel.Model.drawTriangles.Count == 0)
            {
                newModel.Model.Clear();
                return;
            }
            newModel.Name = Path.GetFileName(file);

            if (isTooSmall(newModel.BoundingBox) && newModel.Name.Contains(".glb"))
                DoAutoScale(newModel);
            else if (isTooSmall(newModel.BoundingBox))
                check_stl_size_too_small(newModel);
            else if (isTooBig(newModel.BoundingBox))  // the object is too big.
            {
                double tXBound = newModel.BoundingBox.Size.x / SettingsService.Instance.Settings.PrintAreaWidth;
                double tYBound = newModel.BoundingBox.Size.y / SettingsService.Instance.Settings.PrintAreaDepth;
                double tZBound = newModel.BoundingBox.Size.z / SettingsService.Instance.Settings.PrintAreaHeight;
                double tMax = Math.Max(Math.Max(tXBound, tYBound), Math.Max(tYBound, tZBound));
                double scaleValue = 0;

                if (tMax == tXBound) scaleValue = SettingsService.Instance.Settings.PrintAreaWidth / newModel.BoundingBox.Size.x;
                else if (tMax == tYBound) scaleValue = SettingsService.Instance.Settings.PrintAreaDepth / newModel.BoundingBox.Size.y;
                else if (tMax == tZBound) scaleValue = SettingsService.Instance.Settings.PrintAreaHeight / newModel.BoundingBox.Size.z;

                var result = MessageBox.Show(
                    Trans.T("M_OBJ_SCALE_DOWN") + " " + (int)(scaleValue * 100) + "%",
                    Trans.T("W_OBJ_TOO_LARGE"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        newModel.Scale.x = newModel.Scale.y = newModel.Scale.z = scaleValue;
                        newModel.UpdateBoundingBoxAndMatrix();
                        newModel.Land();
                    }
                    catch { }
                }
            }
            else
            {
                newModel.UpdateBoundingBoxAndMatrix();
            }

            newModel.Position.Z = newModel.BoundingBox.Size.z / 2;
            if (modelToLand)
            {
                Autoposition(newModel);
            }
            else
            {
                newModel.Position.X = (float)newModel.BoundingBox.Center.x;
                newModel.Position.Y = (float)newModel.BoundingBox.Center.y;
                newModel.UpdateTransMatrix();
            }

            // Remember initial positions for all models after Autoposition.
            foreach (var m in models)
            {
                m.InitialPosition.x = m.Position.X;
                m.InitialPosition.y = m.Position.Y;
                m.InitialPosition.z = m.Position.Z;
            }

            newModel.InitialPosition.x = newModel.Position.X;
            newModel.InitialPosition.y = newModel.Position.Y;
            newModel.InitialPosition.z = newModel.Position.Z;

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
        private bool CloneObject(ThreeDModel model)
        {
            ThreeDModel newModel = new ThreeDModel();
            model.CopyTo(newModel); 
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

            if (z < -0.1 || z > SettingsService.Instance.Settings.PrintAreaHeight)
                return false;

            if (x < -epsilon || x > SettingsService.Instance.Settings.PrintAreaWidth + epsilon) return false;
            if (y < -epsilon || y > SettingsService.Instance.Settings.PrintAreaDepth + epsilon) return false;

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

        public void check_stl_size_too_small(ThreeDModel model)
        {
            if (model == null) return;

            if (isTooSmall(model.BoundingBox))
            {
                var dlg = new ObjectResizeDialog(
                    model.BoundingBox.Size.x,
                    model.BoundingBox.Size.y,
                    model.BoundingBox.Size.z);
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
        private void RemoveModel(ThreeDModel model)
        {
            var row = RowForModel(model);
            if (row != null) listObjects.Items.Remove(row);

            // ThreeDModel
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

        private bool IsValidPrintModel(ThreeDModel model)
            => model.Name != "Unknown" &&
               typeof(ThreeDModel) == model.GetType() &&
               model.Model != null;

        // =====================================================================
        //  updateEnabled
        // =====================================================================
        private void updateEnabled()
        {
            bool enable = SingleSelectedModel != null;
            if (enable)
            {         
                panelAnalysis.Visibility = Visibility.Visible;
                updateAnalyserData();
            }
            else
            {
                panelAnalysis.Visibility = Visibility.Collapsed;
            }

            textTransX.IsEnabled       = enable;
            textTransY.IsEnabled       = enable;
            textTransZ.IsEnabled       = enable;
            textScaleX.IsEnabled       = enable;
            textScaleY.IsEnabled       = enable;
            textScaleZ.IsEnabled       = enable;                
            textRotX.IsEnabled         = enable;
            textRotY.IsEnabled         = enable;
            textRotZ.IsEnabled         = enable;

            MainWindow.main.setbuttonVisable(enable);
        }

        // =====================================================================
        //  Autoposition
        // =====================================================================
        bool Autoposition(ThreeDModel newModel)
        {
            List<ThreeDModel> allModels = new List<ThreeDModel>();
            foreach (var m in models)
                allModels.Add(m);

            allModels.Add(newModel);

            float maxW = SettingsService.Instance.Settings.PrintAreaWidth;
            float maxH = SettingsService.Instance.Settings.PrintAreaDepth;

            if (allModels.Count == 1)
            {
                var model = allModels[0];
                model.Position.X = maxW / 2;
                model.Position.Y = maxH / 2;
                model.UpdateTransMatrix();
                return true;
            }

            var packer    = new RectPacker(1, 1);
            var outPacker = new OutRectPacker(1000);
            int border    = 1;
            float xOff = 0, yOff = 0;
            outPacker.SetPlatformSize(maxW, maxH);
            bool autosizeFailed = false;

            foreach (var stl in allModels)
            {
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
                    var s = (ThreeDModel)outPacker.vRects[i].obj;
                    s.Position.X += xOff + xCenter + outPacker.vRects[i].x + border - 1000 - xOrigPos - s.xMin;
                    s.Position.Y += yOff + yCenter + outPacker.vRects[i].y + border - 1000 - yOrigPos - s.yMin;
                    s.UpdateTransMatrix();
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
                var s = (ThreeDModel)rect.obj;
                s.Position.X += xOff + xAdd + rect.x + border - s.xMin;
                s.Position.Y += yOff + yAdd + rect.y + border - s.yMin;
                s.UpdateTransMatrix();
            }
            return true;
        }

        // =====================================================================
        //  Event handlers – ListView
        // =====================================================================
        private void listObjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var list = ListObjects(false);
            var sellist = ListObjects(true);
            ThreeDModel stl = sellist.Count == 1 ? sellist.First.Value : null;
            foreach (var s in list)
                s.Selected = sellist.Contains(s);
 
            SingleSelectedModel = stl;
            updateEnabled();

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

                textTransX.Text = stl.Position.X.ToString("0.000");
                textTransY.Text = stl.Position.Y.ToString("0.000");
                textTransZ.Text = stl.Position.Z.ToString("0.000");

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
            double old = stl.Position.X;
            double.TryParse(textTransX.Text, out double outVal);
            stl.Position.X = outVal;
            if (Math.Abs(old - stl.Position.X) < 0.001f) return;
            stl.UpdateTransMatrix();
               UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textTransY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            double old = stl.Position.Y;
            double.TryParse(textTransY.Text, out double outVal);
            stl.Position.Y = outVal;
            if (Math.Abs(old - stl.Position.Y) < 0.001f) return;
            stl.UpdateTransMatrix();
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textTransZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            double old = stl.Position.Z;
            double.TryParse(textTransZ.Text, out double outVal);
            stl.Position.Z = outVal;
            if (Math.Abs(old - stl.Position.Z) < 0.001f) return;
            stl.UpdateTransMatrix();
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textScaleX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            double.TryParse(textScaleX.Text, out stl.Scale.x);
            stl.UpdateBoundingBoxAndMatrix();
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textScaleY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            double.TryParse(textScaleY.Text, out stl.Scale.y);
            stl.UpdateBoundingBoxAndMatrix();
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        private void textScaleZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            double old = stl.Scale.z;
            double.TryParse(textScaleZ.Text, out stl.Scale.z);
            stl.UpdateBoundingBoxAndMatrix();
            if (old != stl.Scale.z) stl.Land();
            UpdateOutOfBound();
            MainWindow.main.threeDControl.UpdateChanges();
        }

        public void textRotX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextEvents) return;
            var stl = SingleSelectedModel;
            if (stl == null) return;
            float oriZmin = stl.zMin;
            double old = stl.Rotation.x;
            double.TryParse(textRotX.Text, out stl.Rotation.x);
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
            double old = stl.Rotation.y;
            double.TryParse(textRotY.Text, out stl.Rotation.y);
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
            double old = stl.Rotation.z;
            double.TryParse(textRotZ.Text, out stl.Rotation.z);
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
            var model = (ThreeDModel)btn.Tag;
            RemoveModel(model);
            MainWindow.main.threeDControl.UpdateChanges();
        }

        // =====================================================================
        //  objectMoved / objectSelected  (called from ThreeDControl)
        // =====================================================================
        public void ObjectMoved(float dx, float dy)
        {
            float maxX = SettingsService.Instance.Settings.PrintAreaWidth * 1.2f;
            float minX = -SettingsService.Instance.Settings.PrintAreaWidth * 0.2f;
            float maxY = SettingsService.Instance.Settings.PrintAreaDepth * 1.2f;
            float minY = -SettingsService.Instance.Settings.PrintAreaDepth * 0.2f;

            foreach (var stl in ListObjects(true))
            {
                if ( dx < 0 && stl.Position.X + dx > minX)  // If the boject is out of bound, allow to move it back to the bound area.
                    stl.Position.X += dx;
                else if (stl.Position.X + dx < maxX && stl.Position.X + dx > minX) 
                    stl.Position.X += dx;

                if (dy < 0 && stl.Position.Y + dy > minY)
                    stl.Position.Y += dy;
                else if (stl.Position.Y + dy < maxY && stl.Position.Y + dy > minY) 
                    stl.Position.Y += dy;

                if (SingleSelectedModel != null)
                {
                    _suppressTextEvents = true;
                    textTransX.Text = stl.Position.X.ToString("0.000");
                    textTransY.Text = stl.Position.Y.ToString("0.000");
                    _suppressTextEvents = false;
                }
                stl.UpdateTransMatrix();
                UpdateOutOfBound();
            }
            MainWindow.main.threeDControl.UpdateChanges();
        }

        public void ObjectSelected(ThreeDModel sel)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                if (!sel.Selected) SetObjectSelected(sel, true);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                SetObjectSelected(sel, !sel.Selected);
            }
            else
            {
                listObjects.SelectedItems.Clear();
                SetObjectSelected(sel, true);
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
        public void DoAutoScale(ThreeDModel model)
        {
            try
            {
                var bbox = model.BoundingBox;

                // Find the largest dimension of the model.
                double maxDim = Math.Max(Math.Max(bbox.Size.x, bbox.Size.y), bbox.Size.z);
                double scaleFactor = (SettingsService.Instance.Settings.PrintAreaWidth / 2) / maxDim;

                model.Scale.x = (float)scaleFactor;
                model.Scale.y = (float)scaleFactor;
                model.Scale.z = (float)scaleFactor;

                model.UpdateBoundingBoxAndMatrix();
                model.Land();
                UpdateOutOfBound();
                MainWindow.main.threeDControl.UpdateChanges();
            }
            catch { }
        }

        public void DoMmToInch(ThreeDModel model)
        {
            try
            {
                var ui = MainWindow.main.UI_resize_advance;
                ui.button_mmtoinch.IsEnabled = false;
                ui.button_inchtomm.IsEnabled = true;

                double tempX = model.BoundingBox.Size.x * 25.4, tempY = model.BoundingBox.Size.y * 25.4, tempZ = model.BoundingBox.Size.z * 25.4;
                if (tempX > SettingsService.Instance.Settings.PrintAreaWidth || 
                    tempY > SettingsService.Instance.Settings.PrintAreaDepth || 
                    tempZ > SettingsService.Instance.Settings.PrintAreaHeight)
                {
                    if (!AskUserToChangeUnit())
                    {
                        ui.button_mmtoinch.IsEnabled = true;
                        ui.button_inchtomm.IsEnabled = false;
                        return;
                    }
                }

                double scaleFactor = 25.4; // 1 inch = 25.4 mm

                model.Scale.x *= (float)scaleFactor;
                model.Scale.y *= (float)scaleFactor;
                model.Scale.z *= (float)scaleFactor;

                model.UpdateBoundingBoxAndMatrix();
                model.Land();
                UpdateOutOfBound();
                MainWindow.main.threeDControl.UpdateChanges();
            }
            catch { }
        }

        public void DoInchtomm(ThreeDModel model)
        {
            try
            {
                var ui = MainWindow.main.UI_resize_advance;
                ui.button_mmtoinch.IsEnabled = true;
                ui.button_inchtomm.IsEnabled = false;

                double scaleFactor = 25.4; // 1 inch = 25.4 mm

                model.Scale.x /= (float)scaleFactor;
                model.Scale.y /= (float)scaleFactor;
                model.Scale.z /= (float)scaleFactor;

                model.UpdateBoundingBoxAndMatrix();
                model.Land();
                UpdateOutOfBound();
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
