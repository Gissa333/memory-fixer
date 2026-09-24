using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MemoryFixer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class RoundButton : Button
    {
        public int Radius { get; set; } = 8;
        public Color Normal { get; set; } = Color.FromArgb(124, 58, 237);
        public Color Hover { get; set; } = Color.FromArgb(139, 92, 246);
        public Color Pressed { get; set; } = Color.FromArgb(109, 40, 217);
        private bool h, p;

        public RoundButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { h = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { h = false; p = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { p = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { p = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color bg = !Enabled ? Color.FromArgb(70, 70, 90) : p ? Pressed : h ? Hover : Normal;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundPath(r, Radius))
            using (var b = new SolidBrush(bg))
                e.Graphics.FillPath(b, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private GraphicsPath RoundPath(Rectangle r, int rad)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            path.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            path.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class MainForm : Form
    {
        static readonly Color BgDark = Color.FromArgb(18, 18, 28);
        static readonly Color CardBg = Color.FromArgb(30, 30, 46);
        static readonly Color CardBorder = Color.FromArgb(60, 60, 90);
        static readonly Color Accent = Color.FromArgb(124, 58, 237);
        static readonly Color TextMain = Color.FromArgb(235, 235, 245);
        static readonly Color Success = Color.FromArgb(34, 197, 94);
        static readonly Color Warning = Color.FromArgb(245, 158, 11);
        static readonly Color Error = Color.FromArgb(239, 68, 68);
        static readonly Color Info = Color.FromArgb(96, 165, 250);
        static readonly Color Teal = Color.FromArgb(20, 184, 166);
        static readonly Color Pink = Color.FromArgb(236, 72, 153);

        const string Dev = "وقاص عباس جاويش التوم";
        const int BufSize = 1024 * 1024;
        const long MaxFileSize = 4L * 1024 * 1024 * 1024;

        ComboBox cmbDrives;
        Label lblInfo;
        Label lblReadOnly;
        TabControl tabs;
        CancellationTokenSource cts;

        ListView lvFiles;
        RichTextBox logFs;
        RoundButton btnReadMft, btnReadFat, btnReadFull, btnRecoverFromList;
        Label lblFsStatus;

        TextBox txtOut;
        RichTextBox logRecovery;
        ProgressBar pbRecovery;
        Label lblRecPercent;
        RoundButton btnStartRec, btnStopRec, btnBrowse;

        RichTextBox logRepair;
        RoundButton btnChkScan, btnChkFix, btnFormatQuick, btnFormatFull, btnFixBoot, btnBackupBoot, btnRestoreBoot;

        RichTextBox logBad;
        ProgressBar pbBad;
        RoundButton btnBadStart, btnBadStop;

        RichTextBox logSpeed;
        ProgressBar pbSpeed;
        RoundButton btnSpeedStart;

        HexViewer hexViewer;
        RoundButton btnHexFromFile, btnHexFromDrive;

        public MainForm()
        {
            Text = "Memory Fixer - صيانة ذواكر التخزين | " + Dev;
            Width = 1180; Height = 820;
            BackColor = BgDark; ForeColor = TextMain;
            Font = new Font("Segoe UI", 9.5F);
            StartPosition = FormStartPosition.CenterScreen;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            MinimumSize = new Size(1050, 720);

            BuildUI();
            LoadDrives();
        }

        void BuildUI()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 105 };
            header.Paint += (s, e) =>
            {
                using (var lg = new LinearGradientBrush(header.ClientRectangle,
                    Color.FromArgb(30, 60, 100), Color.FromArgb(20, 120, 140), 0F))
                    e.Graphics.FillRectangle(lg, header.ClientRectangle);
            };

            var title = new Label
            {
                Text = "Memory Fixer",
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = Color.White, AutoSize = true,
                Location = new Point(30, 15), BackColor = Color.Transparent
            };
            var sub = new Label
            {
                Text = "صيانة ذواكر التخزين (SD / فلاش USB) + استعادة الملفات + عارض Hex",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(200, 240, 255), AutoSize = true,
                Location = new Point(35, 60), BackColor = Color.Transparent
            };
            var dev = new Label
            {
                Text = "Developed by  " + Dev + "  •  v2.0",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 130), AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.Transparent
            };
            dev.Location = new Point(header.Width - 320, 75);
            header.Resize += (s, e) => dev.Location = new Point(header.Width - dev.Width - 30, 75);

            header.Controls.Add(title);
            header.Controls.Add(sub);
            header.Controls.Add(dev);

            var dp = new Panel
            {
                Location = new Point(20, 118),
                Size = new Size(1130, 65),
                BackColor = CardBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblD = new Label
            {
                Text = "الذاكرة:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TextMain, Location = new Point(20, 20),
                AutoSize = true, BackColor = Color.Transparent
            };

            cmbDrives = new ComboBox
            {
                Location = new Point(120, 18), Width = 450,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.FromArgb(45, 45, 65),
                ForeColor = TextMain, FlatStyle = FlatStyle.Flat
            };
            cmbDrives.SelectedIndexChanged += (s, e) => UpdateInfo();

            lblInfo = new Label
            {
                Text = "", Font = new Font("Segoe UI", 9F),
                ForeColor = Info, Location = new Point(585, 22),
                AutoSize = true, BackColor = Color.Transparent
            };

            lblReadOnly = new Label
            {
                Text = "وضع القراءة فقط",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Success, Location = new Point(1000, 22),
                AutoSize = true, BackColor = Color.Transparent
            };

            dp.Controls.Add(lblD);
            dp.Controls.Add(cmbDrives);
            dp.Controls.Add(lblInfo);
            dp.Controls.Add(lblReadOnly);

            tabs = new TabControl
            {
                Location = new Point(20, 195),
                Size = new Size(1130, 565),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Padding = new Point(15, 8),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                ItemSize = new Size(175, 36),
                SizeMode = TabSizeMode.Fixed
            };
            tabs.DrawItem += (s, e) =>
            {
                var g = e.Graphics;
                var rect = tabs.GetTabRect(e.Index);
                bool sel = e.Index == tabs.SelectedIndex;
                using (var bg = new SolidBrush(sel ? Accent : CardBg))
                    g.FillRectangle(bg, rect);
                TextRenderer.DrawText(g, tabs.TabPages[e.Index].Text,
                    new Font("Segoe UI", 9F, FontStyle.Bold), rect,
                    sel ? Color.White : TextMain,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            var t1 = new TabPage("نظام الملفات") { BackColor = CardBg, ForeColor = TextMain };
            var t2 = new TabPage("استعادة خام") { BackColor = CardBg, ForeColor = TextMain };
            var t3 = new TabPage("إصلاح الذاكرة") { BackColor = CardBg, ForeColor = TextMain };
            var t4 = new TabPage("فحص القطاعات") { BackColor = CardBg, ForeColor = TextMain };
            var t5 = new TabPage("اختبار السرعة") { BackColor = CardBg, ForeColor = TextMain };
            var t6 = new TabPage("عارض Hex") { BackColor = CardBg, ForeColor = TextMain };

            BuildTab1(t1);
            BuildTab2(t2);
            BuildTab3(t3);
            BuildTab4(t4);
            BuildTab5(t5);
            BuildTab6(t6);

            tabs.TabPages.Add(t1);
            tabs.TabPages.Add(t2);
            tabs.TabPages.Add(t3);
            tabs.TabPages.Add(t4);
            tabs.TabPages.Add(t5);
            tabs.TabPages.Add(t6);

            Controls.Add(header);
            Controls.Add(dp);
            Controls.Add(tabs);
        }

        void BuildTab1(TabPage tab)
        {
            lblFsStatus = new Label
            {
                Text = "اختر نظام الملفات للقراءة:",
                Location = new Point(20, 15), AutoSize = true,
                ForeColor = TextMain, Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            btnReadMft = MkBtn("قراءة MFT (NTFS)", Info, 20, 45);
            btnReadMft.Click += BtnReadMft_Click;

            btnReadFat = MkBtn("قراءة FAT/exFAT", Teal, 230, 45);
            btnReadFat.Click += BtnReadFat_Click;

            btnReadFull = MkBtn("قراءة كاملة", Pink, 440, 45);
            btnReadFull.Click += BtnReadFull_Click;

            btnRecoverFromList = MkBtn("استعادة المحدد", Success, 650, 45);
            btnRecoverFromList.Click += BtnRecoverFromList_Click;

            lvFiles = new ListView
            {
                Location = new Point(20, 110),
                Size = new Size(1080, 250),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(12, 12, 20),
                ForeColor = TextMain,
                Font = new Font("Consolas", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            lvFiles.Columns.Add("الاسم", 400);
            lvFiles.Columns.Add("الحجم", 120);
            lvFiles.Columns.Add("النوع", 100);
            lvFiles.Columns.Add("الحالة", 120);
            lvFiles.Columns.Add("الموقع", 200);
            lvFiles.SelectedIndexChanged += (s, e) => ShowHexForSelected();

            logFs = new RichTextBox
            {
                Location = new Point(20, 370),
                Size = new Size(1080, 130),
                BackColor = Color.FromArgb(12, 12, 20),
                ForeColor = TextMain,
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                RightToLeft = RightToLeft.No
            };

            tab.Controls.Add(lblFsStatus);
            tab.Controls.Add(btnReadMft);
            tab.Controls.Add(btnReadFat);
            tab.Controls.Add(btnReadFull);
            tab.Controls.Add(btnRecoverFromList);
            tab.Controls.Add(lvFiles);
            tab.Controls.Add(logFs);
        }

        void BtnReadMft_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbDrives.SelectedItem == null) { MessageBox.Show("اختر الذاكرة."); return; }
                char L = GetLetter();
                lvFiles.Items.Clear();
                logFs.Clear();
                Log(logFs, "قراءة MFT من NTFS...", Info);

                Task.Run(() =>
                {
                    var entries = MftReader.ReadDeletedEntries($@"\\.\{L}:", (pct, msg) =>
                    {
                        Log(logFs, msg, TextMain);
                    });

                    Invoke((Action)(() =>
                    {
                        foreach (var entry in entries)
                        {
                            var lvi = new ListViewItem(new string[]
                            {
                                entry.FileName,
                                FormatSize(entry.RealSize),
                                "NTFS",
                                "محذوف",
                                $"سجل #{entry.RecordNumber}"
                            });
                            lvi.Tag = entry;
                            lvFiles.Items.Add(lvi);
                        }
                        SetLabel(lblFsStatus, $"تم استخراج {entries.Count} ملف محذوف من MFT.", Success);
                        Log(logFs, $"اكتمل: {entries.Count} ملف", Success);
                    }));
                });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void BtnReadFat_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbDrives.SelectedItem == null) { MessageBox.Show("اختر الذاكرة."); return; }
                char L = GetLetter();
                lvFiles.Items.Clear();
                logFs.Clear();
                Log(logFs, "قراءة دليل FAT/exFAT...", Info);

                Task.Run(() =>
                {
                    var entries = FatReader.ReadDirectory($@"\\.\{L}:");
                    Invoke((Action)(() =>
                    {
                        foreach (var entry in entries)
                        {
                            var lvi = new ListViewItem(new string[]
                            {
                                entry.FileName,
                                FormatSize(entry.Size),
                                entry.IsDirectory ? "مجلد" : "ملف",
                                "ظاهر",
                                $"Cluster #{entry.FirstCluster}"
                            });
                            lvi.Tag = entry;
                            lvFiles.Items.Add(lvi);
                        }
                        SetLabel(lblFsStatus, $"تم استخراج {entries.Count} ملف من دليل FAT.", Success);
                        Log(logFs, $"اكتمل: {entries.Count} ملف", Success);
                    }));
                });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void BtnReadFull_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbDrives.SelectedItem == null) { MessageBox.Show("اختر الذاكرة."); return; }
                char L = GetLetter();
                lvFiles.Items.Clear();
                logFs.Clear();
                Log(logFs, "قراءة كاملة عبر التواقيع...", Warning);

                Task.Run(() =>
                {
                    try
                    {
                        using (var stream = new FileStream($@"\\.\{L}:", FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufSize))
                        {
                            byte[] buf = new byte[BufSize];
                            long total = stream.Length;
                            long pos = 0;
                            long lastEnd = -1;
                            int found = 0;
                            int maxFound = 500;

                            while (pos < total && found < maxFound)
                            {
                                int read = stream.Read(buf, 0, BufSize);
                                if (read <= 0) break;

                                for (int i = 0; i < read - 16 && found < maxFound; i++)
                                {
                                    long off = pos + i;
                                    if (off <= lastEnd) continue;
                                    var sig = Detect(buf, i, read);
                                    if (sig == null) continue;

                                    found++;
                                    long fakeSize = EstimateFileSize(stream, off, sig.Value.ext);
                                    lastEnd = off + fakeSize;

                                    var cap = new CaptureInfo
                                    {
                                        Offset = off,
                                        Ext = sig.Value.ext,
                                        Name = sig.Value.name,
                                        Size = fakeSize
                                    };

                                    int capturedFound = found;
                                    Invoke((Action)(() =>
                                    {
                                        var lvi = new ListViewItem(new string[]
                                        {
                                            $"ملف_{capturedFound:D4}.{sig.Value.ext}",
                                            FormatSize(fakeSize),
                                            sig.Value.name,
                                            "من خلال التوقيع",
                                            $"Offset {off:N0}"
                                        });
                                        lvi.Tag = cap;
                                        lvFiles.Items.Add(lvi);
                                    }));
                                }
                                pos += read;
                            }

                            int finalFound = found;
                            Invoke((Action)(() =>
                            {
                                SetLabel(lblFsStatus, $"تم العثور على {finalFound} ملف عبر التواقيع.", Success);
                                Log(logFs, $"اكتمل: {finalFound} ملف", Success);
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Invoke((Action)(() => Log(logFs, "خطأ: " + ex.Message,
