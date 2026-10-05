using System.Drawing.Imaging;
using System.Text.Json;

namespace PhotoSorter;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
    }
}

class KeyBinding
{
    public Keys Key { get; set; } = Keys.None;
    public string Folder { get; set; } = "";
}

class MainForm : Form
{
    static readonly string[] Exts = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };
    static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhotoSorter", "bindings.json");

    readonly PictureBox picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(28, 28, 28) };
    readonly Label status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
    readonly FlowLayoutPanel bindingList = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

    readonly List<KeyBinding> bindings = new();
    readonly List<string> files = new();
    readonly Stack<(string origin, string moved, int index)> undo = new();
    int index = -1;
    string folder;
    KeyBinding capturing;      // binding currently waiting for a key press
    Button capturingButton;

    static readonly Keys[] Reserved = { Keys.Left, Keys.Right, Keys.Escape, Keys.Delete, Keys.Back, Keys.Home, Keys.End, Keys.Tab };

    public MainForm(string startPath)
    {
        Text = "Photo Sorter";
        Size = new Size(1100, 720);
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);
        KeyPreview = true;

        // top toolbar
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 8, 0, 0) };
        bar.Controls.Add(MakeButton("Open Photo…", (_, _) => OpenPhoto(), 130));
        bar.Controls.Add(MakeButton("◀ Previous", (_, _) => Step(-1), 110));
        bar.Controls.Add(MakeButton("Next ▶", (_, _) => Step(1), 100));
        bar.Controls.Add(MakeButton("Undo move", (_, _) => Undo(), 110));

        // right side panel
        var side = new Panel { Dock = DockStyle.Right, Width = 380, Padding = new Padding(8) };
        var header = new Label { Dock = DockStyle.Top, Height = 30, Text = "Keybinds → folders", Font = new Font(Font, FontStyle.Bold) };
        var add = MakeButton("+ Add keybind", (_, _) => { var b = new KeyBinding(); bindings.Add(b); AddRow(b); }, 140);
        var addHolder = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 6, 0, 0) };
        addHolder.Controls.Add(add);
        var hint = new Label
        {
            Dock = DockStyle.Bottom, Height = 70, ForeColor = Color.DimGray,
            Text = "Click a key box, then press the key you want. ← / → browse photos, Ctrl+Z undoes the last move."
        };
        side.Controls.Add(bindingList);
        side.Controls.Add(hint);
        side.Controls.Add(addHolder);
        side.Controls.Add(header);

        var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Color.FromArgb(240, 240, 240) };
        statusBar.Controls.Add(status);

        Controls.Add(picture);
        Controls.Add(side);
        Controls.Add(bar);
        Controls.Add(statusBar);

        LoadConfig();
        if (bindings.Count == 0) bindings.Add(new KeyBinding());
        foreach (var b in bindings) AddRow(b);

        FormClosing += (_, _) => SaveConfig();
        picture.AllowDrop = AllowDrop = true;
        DragEnter += (_, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] p && p.Length > 0) LoadFolder(p[0]);
        };

        if (startPath != null && File.Exists(startPath)) LoadFolder(startPath);
        else UpdateStatus();
    }

    static Button MakeButton(string text, EventHandler click, int width)
    {
        var b = new Button { Text = text, Width = width, Height = 32, FlatStyle = FlatStyle.System, TabStop = false };
        b.Click += click;
        return b;
    }

    // ---------- keybind rows ----------

    void AddRow(KeyBinding b)
    {
        var row = new Panel { Width = 340, Height = 40 };
        var keyBtn = new Button { Left = 0, Top = 4, Width = 80, Height = 30, TabStop = false, Text = KeyText(b.Key) };
        var folderBtn = new Button { Left = 86, Top = 4, Width = 190, Height = 30, TabStop = false, TextAlign = ContentAlignment.MiddleLeft, Text = FolderText(b.Folder) };
        var remove = new Button { Left = 282, Top = 4, Width = 36, Height = 30, TabStop = false, Text = "✕" };

        keyBtn.Click += (_, _) =>
        {
            CancelCapture();
            capturing = b; capturingButton = keyBtn;
            keyBtn.Text = "press key…";
            Focus();
        };
        folderBtn.Click += (_, _) =>
        {
            CancelCapture();
            using var d = new FolderBrowserDialog { Description = "Folder for this keybind", UseDescriptionForTitle = true, SelectedPath = b.Folder ?? "" };
            if (d.ShowDialog(this) == DialogResult.OK) { b.Folder = d.SelectedPath; folderBtn.Text = FolderText(b.Folder); SaveConfig(); }
        };
        remove.Click += (_, _) =>
        {
            CancelCapture();
            bindings.Remove(b); bindingList.Controls.Remove(row); SaveConfig();
        };
        row.Controls.AddRange(new Control[] { keyBtn, folderBtn, remove });
        bindingList.Controls.Add(row);
    }

    static string KeyText(Keys k) => k == Keys.None ? "(none)" : DisplayKey(k);
    static string DisplayKey(Keys k)
    {
        var s = k.ToString();
        if (s.Length == 2 && s[0] == 'D' && char.IsDigit(s[1])) return s[1].ToString();
        return s;
    }
    static string FolderText(string f) => string.IsNullOrEmpty(f) ? "Choose folder…" : (Path.GetFileName(f.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : f);

    void CancelCapture()
    {
        if (capturing != null) capturingButton.Text = KeyText(capturing.Key);
        capturing = null; capturingButton = null;
    }

    // ---------- keyboard ----------

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        var mods = keyData & Keys.Modifiers;

        if (capturing != null)
        {
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu) return true;
            if (key == Keys.Escape) { CancelCapture(); return true; }
            if (Array.IndexOf(Reserved, key) >= 0 || mods != Keys.None)
            {
                status.Text = "That key is reserved or uses a modifier – pick another.";
                return true;
            }
            var clash = bindings.FirstOrDefault(x => x != capturing && x.Key == key);
            if (clash != null) clash.Key = Keys.None;
            capturing.Key = key;
            capturing = null; capturingButton = null;
            RefreshRows();
            SaveConfig();
            return true;
        }

        if (mods == Keys.None && key == Keys.Left) { Step(-1); return true; }
        if (mods == Keys.None && key == Keys.Right) { Step(1); return true; }
        if (mods == Keys.Control && key == Keys.Z) { Undo(); return true; }

        if (mods == Keys.None && key != Keys.None)
        {
            var b = bindings.FirstOrDefault(x => x.Key == key);
            if (b != null) { MoveCurrent(b); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void RefreshRows()
    {
        for (int i = 0; i < bindings.Count && i < bindingList.Controls.Count; i++)
            bindingList.Controls[i].Controls[0].Text = KeyText(bindings[i].Key);
    }

    // ---------- photos ----------

    void OpenPhoto()
    {
        using var d = new OpenFileDialog { Title = "Open a photo", Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|All files|*.*" };
        if (d.ShowDialog(this) == DialogResult.OK) LoadFolder(d.FileName);
    }

    void LoadFolder(string photoPath)
    {
        if (Directory.Exists(photoPath)) { folder = photoPath; photoPath = null; }
        else folder = Path.GetDirectoryName(photoPath);

        files.Clear(); undo.Clear();
        files.AddRange(Directory.EnumerateFiles(folder)
            .Where(f => Exts.Contains(Path.GetExtension(f).ToLowerInvariant())));
        files.Sort(NaturalCompare);
        index = photoPath == null ? 0 : Math.Max(0, files.FindIndex(f => string.Equals(f, photoPath, StringComparison.OrdinalIgnoreCase)));
        ShowCurrent();
    }

    static int NaturalCompare(string a, string b) => StrCmpLogicalW(Path.GetFileName(a), Path.GetFileName(b));
    [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int StrCmpLogicalW(string x, string y);

    void Step(int d)
    {
        if (files.Count == 0) return;
        index = Math.Clamp(index + d, 0, files.Count - 1);
        ShowCurrent();
    }

    void ShowCurrent()
    {
        var old = picture.Image;
        picture.Image = null;
        old?.Dispose();
        if (index >= 0 && index < files.Count)
        {
            try { picture.Image = LoadImage(files[index]); }
            catch (Exception ex) { status.Text = "Can't open " + Path.GetFileName(files[index]) + ": " + ex.Message; return; }
        }
        UpdateStatus();
    }

    // Loads from memory (so the file isn't locked and can be moved) and applies EXIF rotation.
    static Image LoadImage(string path)
    {
        using var ms = new MemoryStream(File.ReadAllBytes(path));
        using var src = Image.FromStream(ms);
        var bmp = new Bitmap(src);
        try
        {
            if (src.PropertyIdList.Contains(0x0112))
            {
                int o = src.GetPropertyItem(0x0112).Value[0];
                var rot = o switch
                {
                    3 => RotateFlipType.Rotate180FlipNone,
                    6 => RotateFlipType.Rotate90FlipNone,
                    8 => RotateFlipType.Rotate270FlipNone,
                    _ => RotateFlipType.RotateNoneFlipNone
                };
                if (rot != RotateFlipType.RotateNoneFlipNone) bmp.RotateFlip(rot);
            }
        }
        catch { }
        return bmp;
    }

    void UpdateStatus()
    {
        if (files.Count == 0)
            status.Text = folder == null ? "Open a photo (or drag one in) to begin." : "No more photos in this folder.";
        else
            status.Text = $"{index + 1} / {files.Count}   {Path.GetFileName(files[index])}";
    }

    void MoveCurrent(KeyBinding b)
    {
        if (files.Count == 0) { status.Text = "No photo loaded."; return; }
        if (string.IsNullOrEmpty(b.Folder)) { status.Text = "That keybind has no folder yet – click its folder button."; return; }
        try
        {
            Directory.CreateDirectory(b.Folder);
            var src = files[index];
            var dest = UniquePath(b.Folder, Path.GetFileName(src));
            picture.Image?.Dispose(); picture.Image = null;
            File.Move(src, dest);
            undo.Push((src, dest, index));
            files.RemoveAt(index);
            if (index >= files.Count) index = files.Count - 1;
            ShowCurrent();
            status.Text = $"Moved to {FolderText(b.Folder)}.   " + status.Text;
        }
        catch (Exception ex)
        {
            status.Text = "Move failed: " + ex.Message;
            ShowCurrent();
        }
    }

    static string UniquePath(string dir, string name)
    {
        var p = Path.Combine(dir, name);
        if (!File.Exists(p)) return p;
        var stem = Path.GetFileNameWithoutExtension(name); var ext = Path.GetExtension(name);
        for (int i = 1; ; i++)
        {
            p = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(p)) return p;
        }
    }

    void Undo()
    {
        if (undo.Count == 0) { status.Text = "Nothing to undo."; return; }
        var (origin, moved, idx) = undo.Pop();
        try
        {
            picture.Image?.Dispose(); picture.Image = null;
            File.Move(moved, origin);
            files.Insert(Math.Min(idx, files.Count), origin);
            index = Math.Min(idx, files.Count - 1);
            ShowCurrent();
            status.Text = "Undone.   " + status.Text;
        }
        catch (Exception ex) { status.Text = "Undo failed: " + ex.Message; ShowCurrent(); }
    }

    // ---------- persistence ----------

    void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
                bindings.AddRange(JsonSerializer.Deserialize<List<KeyBinding>>(File.ReadAllText(ConfigPath)) ?? new());
        }
        catch { }
    }

    void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(bindings));
        }
        catch { }
    }
}
