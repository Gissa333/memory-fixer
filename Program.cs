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

        // Tab 1 - Filesystem
        ListView lvFiles;
        RichTextBox logFs;
        RoundButton btnReadMft, btnReadFat, btnReadFull, btnRecoverFromList;
        Label lblFsStatus;

        // Tab 2 - Recovery (Raw scan)
        TextBox txtOut;
        RichTextBox logRecovery;
        ProgressBar pbRecovery;
        Label lblRecPercent;
        RoundButton btnStartRec, btnStopRec, btnBrowse;

        // Tab 3 - Repair
        RichTextBox logRepair;
        RoundButton btnChkScan, btnChkFix, btnFormatQuick, btnFormatFull, btnFixBoot, btnBackupBoot, btnRestoreBoot;

        // Tab 4 - Bad sectors
        RichTextBox logBad;
        ProgressBar pbBad;
        RoundButton btnBadStart, btnBadStop;

        // Tab 5 - Speed
        RichTextBox logSpeed;
        ProgressBar pbSpeed;
        RoundButton btnSpeedStart;

        // Tab 6 - Hex
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

        // ============= Tab 1: Filesystem (MFT + FAT + preview) =============
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

            btnReadFull = MkBtn("قراءة كاملة (معاينة Hex)", Pink, 440, 45);
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
                Log(logFs, "قراءة أول 512 بايت من كل ملف عبر التواقيع (معاينة محدودة)...", Warning);

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

                                    Invoke((Action)(() =>
                                    {
                                        var lvi = new ListViewItem(new string[]
                                        {
                                            $"ملف_{found:D4}.{sig.Value.ext}",
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

                            Invoke((Action)(() =>
                            {
                                SetLabel(lblFsStatus, $"تم العثور على {found} ملف عبر التواقيع.", Success);
                                Log(logFs, $"اكتمل: {found} ملف", Success);
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Invoke((Action)(() => Log(logFs, "خطأ: " + ex.Message, Error)));
                    }
                });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        long EstimateFileSize(FileStream stream, long offset, string ext)
        {
            try
            {
                long saved = stream.Position;
                stream.Seek(offset, SeekOrigin.Begin);
                byte[] buf = new byte[BufSize];
                int read = stream.Read(buf, 0, BufSize);
                int zero = 0;
                for (int i = 0; i < read; i++)
                {
                    if (buf[i] == 0x00) zero++;
                    else zero = 0;
                    if (zero >= 1024 * 64) { stream.Position = saved; return i; }
                }
                stream.Position = saved;
                return 256 * 1024;
            }
            catch { return 0; }
        }

        void ShowHexForSelected()
        {
            try
            {
                if (lvFiles.SelectedItems.Count == 0) { hexViewer?.Clear(); return; }
                var item = lvFiles.SelectedItems[0];
                if (cmbDrives.SelectedItem == null) return;
                char L = GetLetter();

                long offset = -1;
                if (item.Tag is MftReader.MftEntry mft && mft.DataRuns.Count > 0)
                    offset = mft.DataRuns[0].Item1;
                else if (item.Tag is CaptureInfo cap)
                    offset = cap.Offset;

                if (offset < 0) { hexViewer?.Clear(); return; }

                Task.Run(() =>
                {
                    try
                    {
                        using (var s = new FileStream($@"\\.\{L}:", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            s.Seek(offset, SeekOrigin.Begin);
                            byte[] preview = new byte[512];
                            int r = s.Read(preview, 0, 512);
                            if (r < 512) Array.Resize(ref preview, r);

                            Invoke((Action)(() =>
                            {
                                if (hexViewer != null)
                                {
                                    hexViewer.SetData(preview, $"معاينة {item.Text} - Offset 0x{offset:X}");
                                    tabs.SelectedIndex = 5;
                                }
                            }));
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        void BtnRecoverFromList_Click(object sender, EventArgs e)
        {
            try
            {
                if (lvFiles.SelectedItems.Count == 0) { MessageBox.Show("حدد ملفًا من القائمة."); return; }
                if (cmbDrives.SelectedItem == null) return;

                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "اختر مجلد الحفظ";
                    if (fbd.ShowDialog() != DialogResult.OK) return;

                    char L = GetLetter();
                    string drivePath = $@"\\.\{L}:";
                    string outDir = fbd.SelectedPath;

                    foreach (ListViewItem item in lvFiles.SelectedItems)
                    {
                        string outName = SanitizeFileName(item.Text);
                        string outPath = Path.Combine(outDir, outName);

                        long offset = -1;
                        long size = 0;

                        if (item.Tag is MftReader.MftEntry mft)
                        {
                            if (mft.DataRuns.Count > 0) { offset = mft.DataRuns[0].Item1; size = mft.RealSize; }
                        }
                        else if (item.Tag is CaptureInfo cap)
                        {
                            offset = cap.Offset;
                            size = cap.Size;
                        }
                        else if (item.Tag is FatReader.FatEntry fat)
                        {
                            Log(logFs, $"FAT: {fat.FileName} — استخدم التبويب الثاني للاستعادة الخام.", Warning);
                            continue;
                        }

                        if (offset >= 0)
                        {
                            Task.Run(() =>
                            {
                                try
                                {
                                    using (var src = new FileStream(drivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                    using (var dst = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                                    {
                                        src.Seek(offset, SeekOrigin.Begin);
                                        byte[] buf = new byte[BufSize];
                                        long written = 0;
                                        while (written < size)
                                        {
                                            int toRead = (int)Math.Min(BufSize, size - written);
                                            int r = src.Read(buf, 0, toRead);
                                            if (r <= 0) break;
                                            dst.Write(buf, 0, r);
                                            written += r;
                                        }
                                    }
                                    Invoke((Action)(() => Log(logFs, $"تم حفظ: {outName}", Success)));
                                }
                                catch (Exception ex)
                                {
                                    Invoke((Action)(() => Log(logFs, "خطأ: " + ex.Message, Error)));
                                }
                            });
                        }
                    }
                    MessageBox.Show("تم بدء الاستعادة. راقب السجل.", "معلومة", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        class CaptureInfo
        {
            public long Offset;
            public long Size;
            public string Ext;
            public string Name;
        }

        // ============= Tab 2: Raw Recovery =============
        void BuildTab2(TabPage tab)
        {
            var lblOut = new Label
            {
                Text = "مجلد الحفظ (يفضّل قرص مختلف):",
                Location = new Point(20, 20), AutoSize = true,
                ForeColor = TextMain, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            txtOut = new TextBox
            {
                Location = new Point(20, 48), Width = 800,
                BackColor = Color.FromArgb(45, 45, 65), ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10F)
            };

            btnBrowse = new RoundButton
            {
                Text = "استعراض", Location = new Point(830, 46),
                Size = new Size(120, 30),
                Normal = Color.FromArgb(55, 55, 80),
                Hover = Color.FromArgb(75, 75, 105), Radius = 6
            };
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) txtOut.Text = fbd.SelectedPath;
            };

            btnStartRec = new RoundButton
            {
                Text = "بدء الاستعادة الخام", Location = new Point(20, 90),
                Size = new Size(200, 42), Normal = Accent,
                Hover = Color.FromArgb(139, 92, 246), Radius = 8,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
            };
            btnStartRec.Click += BtnStartRec_Click;

            btnStopRec = new RoundButton
            {
                Text = "إيقاف", Location = new Point(230, 90),
                Size = new Size(120, 42), Normal = Error,
                Hover = Color.FromArgb(220, 38, 38), Radius = 8, Enabled = false
            };
            btnStopRec.Click += (s, e) => { cts?.Cancel(); Log(logRecovery, "تم الإيقاف", Warning); };

            lblRecPercent = new Label
            {
                Text = "0%", Location = new Point(370, 95), AutoSize = true,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Accent, BackColor = Color.Transparent
            };

            pbRecovery = new ProgressBar
            {
                Location = new Point(20, 145), Width = 1080, Height = 24,
                Style = ProgressBarStyle.Continuous,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            logRecovery = new RichTextBox
            {
                Location = new Point(20, 185), Size = new Size(1080, 315),
                BackColor = Color.FromArgb(12, 12, 20), ForeColor = TextMain,
                Font = new Font("Consolas", 9F), ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle, ScrollBars = RichTextBoxScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                RightToLeft = RightToLeft.No
            };

            tab.Controls.Add(lblOut);
            tab.Controls.Add(txtOut);
            tab.Controls.Add(btnBrowse);
            tab.Controls.Add(btnStartRec);
            tab.Controls.Add(btnStopRec);
            tab.Controls.Add(lblRecPercent);
            tab.Controls.Add(pbRecovery);
            tab.Controls.Add(logRecovery);
        }

        // ============= Tab 3: Repair =============
        void BuildTab3(TabPage tab)
        {
            var lbl = new Label
            {
                Text = "أدوات إصلاح الذاكرة:",
                Location = new Point(20, 20), AutoSize = true,
                ForeColor = TextMain, Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            btnChkScan = MkBtn("فحص بدون إصلاح", Info, 20, 55);
            btnChkScan.Click += (s, e) => RunRepair("chkdsk_scan");

            btnChkFix = MkBtn("فحص وإصلاح", Success, 230, 55);
            btnChkFix.Click += (s, e) => RunRepair("chkdsk_fix");

            btnFormatQuick = MkBtn("فورمات سريع", Warning, 440, 55);
            btnFormatQuick.Click += (s, e) => RunRepair("format_quick");

            btnFormatFull = MkBtn("فورمات كامل", Warning, 650, 55);
            btnFormatFull.Click += (s, e) => RunRepair("format_full");

            btnFixBoot = MkBtn("إصلاح Boot Sector", Accent, 20, 120);
            btnFixBoot.Click += (s, e) => RunRepair("fix_boot");

            btnBackupBoot = MkBtn("نسخ Boot Sector", Teal, 230, 120);
            btnBackupBoot.Click += (s, e) => BackupBoot();

            btnRestoreBoot = MkBtn("استعادة Boot Sector", Pink, 440, 120);
            btnRestoreBoot.Click += (s, e) => RestoreBoot();

            logRepair = new RichTextBox
            {
                Location = new Point(20, 190), Size = new Size(1080, 310),
                BackColor = Color.FromArgb(12, 12, 20), ForeColor = TextMain,
                Font = new Font("Consolas", 9F), ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle, ScrollBars = RichTextBoxScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                RightToLeft = RightToLeft.No
            };

            tab.Controls.Add(lbl);
            tab.Controls.Add(btnChkScan);
            tab.Controls.Add(btnChkFix);
            tab.Controls.Add(btnFormatQuick);
            tab.Controls.Add(btnFormatFull);
            tab.Controls.Add(btnFixBoot);
            tab.Controls.Add(btnBackupBoot);
            tab.Controls.Add(btnRestoreBoot);
            tab.Controls.Add(logRepair);
        }

        // ============= Tab 4: Bad sectors =============
        void BuildTab4(TabPage tab)
        {
            btnBadStart = MkBtn("بدء فحص القطاعات", Accent, 20, 20);
            btnBadStart.Size = new Size(220, 45);
            btnBadStart.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnBadStart.Click += BtnBadStart_Click;

            btnBadStop = MkBtn("إيقاف", Error, 260, 20);
            btnBadStop.Size = new Size(120, 45);
            btnBadStop.Enabled = false;
            btnBadStop.Click += (s, e) => cts?.Cancel();

            pbBad = new ProgressBar
            {
                Location = new Point(20, 80), Width = 1080, Height = 24,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            logBad = new RichTextBox
            {
                Location = new Point(20, 120), Size = new Size(1080, 380),
                BackColor = Color.FromArgb(12, 12, 20), ForeColor = TextMain,
                Font = new Font("Consolas", 9F), ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle, ScrollBars = RichTextBoxScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                RightToLeft = RightToLeft.No
            };

            tab.Controls.Add(btnBadStart);
            tab.Controls.Add(btnBadStop);
            tab.Controls.Add(pbBad);
            tab.Controls.Add(logBad);
        }

        // ============= Tab 5: Speed =============
        void BuildTab5(TabPage tab)
        {
            var lbl = new Label
            {
                Text = "يكتب 100 ميجا ثم يقرأها، ويحسب السرعة الحقيقية للذاكرة.",
                Location = new Point(20, 20), AutoSize = true,
                ForeColor = TextMain, Font = new Font("Segoe UI", 10F),
                BackColor = Color.Transparent
            };

            btnSpeedStart = MkBtn("بدء اختبار السرعة", Teal, 20, 55);
            btnSpeedStart.Size = new Size(230, 45);
            btnSpeedStart.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnSpeedStart.Click += BtnSpeedStart_Click;

            pbSpeed = new ProgressBar
            {
                Location = new Point(270, 65), Width = 830, Height = 24,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            logSpeed = new RichTextBox
            {
                Location = new Point(20, 130), Size = new Size(1080, 370),
                BackColor = Color.FromArgb(12, 12, 20), ForeColor = TextMain,
                Font = new Font("Consolas", 10F), ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle, ScrollBars = RichTextBoxScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                RightToLeft = RightToLeft.No
            };

            tab.Controls.Add(lbl);
            tab.Controls.Add(btnSpeedStart);
            tab.Controls.Add(pbSpeed);
            tab.Controls.Add(logSpeed);
        }

        // ============= Tab 6: Hex Viewer =============
        void BuildTab6(TabPage tab)
        {
            var lbl = new Label
            {
                Text = "عارض Hex - يعرض أول 512 بايت من أي ملف أو قطاع",
                Location = new Point(20, 15), AutoSize = true,
                ForeColor = TextMain, Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            btnHexFromFile = MkBtn("فتح ملف", Info, 20, 45);
            btnHexFromFile.Click += (s, e) =>
            {
                using (var ofd = new OpenFileDialog())
                {
                    if (ofd.ShowDialog() != DialogResult.OK) return;
                    try
                    {
                        var data = File.ReadAllBytes(ofd.FileName);
                        if (data.Length > 512) Array.Resize(ref data, 512);
                        hexViewer.SetData(data, Path.GetFileName(ofd.FileName));
                    }
                    catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message); }
                }
            };

            btnHexFromDrive = MkBtn("قطاع من الذاكرة", Pink, 230, 45);
            btnHexFromDrive.Click += (s, e) =>
            {
                try
                {
                    if (cmbDrives.SelectedItem == null) { MessageBox.Show("اختر الذاكرة."); return; }
                    char L = GetLetter();
                    var input = Microsoft.VisualBasic.Interaction.InputBox(
                        "أدخل رقم القطاع (Sector Number):", "قراءة قطاع", "0");
                    if (!long.TryParse(input, out long sector)) return;

                    using (var s = new FileStream($@"\\.\{L}:", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        s.Seek(sector * 512, SeekOrigin.Begin);
                        byte[] buf = new byte[512];
                        s.Read(buf, 0, 512);
                        hexViewer.SetData(buf, $"Sector #{sector} (Offset 0x{sector * 512:X})");
                    }
                }
                catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message); }
            };

            hexViewer = new HexViewer
            {
                Location = new Point(20, 100),
                Size = new Size(1080, 400),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            tab.Controls.Add(lbl);
            tab.Controls.Add(btnHexFromFile);
            tab.Controls.Add(btnHexFromDrive);
            tab.Controls.Add(hexViewer);
        }

        // ============= Helpers =============
        RoundButton MkBtn(string text, Color color, int x, int y)
        {
            return new RoundButton
            {
                Text = text, Location = new Point(x, y),
                Size = new Size(200, 45), Normal = color,
                Hover = Lighten(color, 25), Radius = 8,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
        }

        Color Lighten(Color c, int amt)
        {
            return Color.FromArgb(Math.Min(255, c.R + amt),
                Math.Min(255, c.G + amt), Math.Min(255, c.B + amt));
        }

        string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("F2") + " GB";
        }

        string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        void LoadDrives()
        {
            cmbDrives.Items.Clear();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.IsReady && (d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed))
                    {
                        long gb = d.TotalSize / (1024L * 1024 * 1024);
                        long free = d.AvailableFreeSpace / (1024L * 1024 * 1024);
                        string lbl = string.IsNullOrEmpty(d.VolumeLabel) ? "بدون اسم" : d.VolumeLabel;
                        cmbDrives.Items.Add($"{d.Name}  |  {lbl}  |  {d.DriveFormat}  |  {gb} GB (متاح {free} GB)");
                    }
                }
                catch { }
            }
            if (cmbDrives.Items.Count > 0) cmbDrives.SelectedIndex = 0;
        }

        void UpdateInfo()
        {
            if (cmbDrives.SelectedItem == null) { lblInfo.Text = ""; return; }
            try
            {
                char L = cmbDrives.SelectedItem.ToString()[0];
                var d = new DriveInfo(L.ToString());
                long sectors = d.TotalSize / 512;
                lblInfo.Text = $"القطاعات: ~{sectors:N0}  |  النوع: {d.DriveType}";
            }
            catch { lblInfo.Text = ""; }
        }

        char GetLetter()
        {
            if (cmbDrives.SelectedItem == null) throw new Exception("اختر الذاكرة أولاً.");
            return cmbDrives.SelectedItem.ToString()[0];
        }

        void BtnStartRec_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbDrives.SelectedItem == null) { MessageBox.Show("اختر الذاكرة."); return; }
                if (string.IsNullOrWhiteSpace(txtOut.Text) || !Directory.Exists(txtOut.Text))
                { MessageBox.Show("اختر مجلد حفظ صحيح."); return; }

                char L = GetLetter();
                cts = new CancellationTokenSource();
                btnStartRec.Enabled = false;
                btnStopRec.Enabled = true;
                logRecovery.Clear();
                pbRecovery.Value = 0;
                lblRecPercent.Text = "0%";

                string path = $@"\\.\{L}:";
                string outDir = txtOut.Text;
                var token = cts.Token;

                Task.Run(() => RecoverFiles(path, outDir, token));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void RecoverFiles(string drivePath, string outDir, CancellationToken token)
        {
            Log(logRecovery, "===============================================", Info);
            Log(logRecovery, "  Memory Fixer v2.0 - استعادة خام", Info);
            Log(logRecovery, "  " + Dev, Info);
            Log(logRecovery, "  وضع: قراءة فقط (بدون كتابة على الكارت)", Success);
            Log(logRecovery, "===============================================", Info);

            try
            {
                using (var stream = new FileStream(drivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufSize))
                {
                    long total = stream.Length;
                    Log(logRecovery, $"الحجم: {total / (1024L * 1024 * 1024)} GB", Info);
                    Log(logRecovery, "جارٍ البحث...", Warning);
                    Log(logRecovery, "-----------------------------------------------", CardBorder);

                    byte[] buf = new byte[BufSize];
                    long pos = 0;
                    long lastEnd = -1;
                    int found = 0;

                    while (pos < total)
                    {
                        if (token.IsCancellationRequested) { Log(logRecovery, "إيقاف.", Warning); break; }
                        int read = stream.Read(buf, 0, BufSize);
                        if (read <= 0) break;

                        for (int i = 0; i < read - 16; i++)
                        {
                            long off = pos + i;
                            if (off <= lastEnd) continue;
                            var sig = Detect(buf, i, read);
                            if (sig == null) continue;

                            found++;
                            Log(logRecovery, $"[{found}] {sig.Value.name} @ {off:N0}", Success);
                            string fn = $"rec_{found:D4}_{DateTime.Now:yyyyMMdd_HHmmss}.{sig.Value.ext}";
                            string fp = Path.Combine(outDir, fn);

                            long ex = Extract(stream, off, fp, sig.Value.minSize, token);
                            if (ex > 0)
                            {
                                lastEnd = off + ex;
                                Log(logRecovery, $"    {ex / (1024 * 1024)} MB -> {fn}", Success);
                            }
                            else found--;
                        }

                        pos += read;
                        int pct = (int)((pos * 100) / total);
                        UpdatePb(pbRecovery, lblRecPercent, pct);
                    }

                    Log(logRecovery, "-----------------------------------------------", CardBorder);
                    Log(logRecovery, $"انتهى. عدد الملفات: {found}", Success);
                }
            }
            catch (UnauthorizedAccessException) { Log(logRecovery, "خطأ: شغّل كمسؤول.", Error); }
            catch (Exception ex) { Log(logRecovery, "خطأ: " + ex.Message, Error); }
            finally { Invoke((Action)(() => { btnStartRec.Enabled = true; btnStopRec.Enabled = false; })); }
        }

        (string name, string ext, long minSize)? Detect(byte[] b, int i, int len)
        {
            if (i + 4 >= len) return null;
            if (b[i] == 0xFF && b[i + 1] == 0xD8 && b[i + 2] == 0xFF) return ("JPEG", "jpg", 1024);
            if (i + 8 < len && b[i] == 0x89 && b[i + 1] == 0x50 && b[i + 2] == 0x4E && b[i + 3] == 0x47) return ("PNG", "png", 1024);
            if (i + 6 < len && b[i] == 0x47 && b[i + 1] == 0x49 && b[i + 2] == 0x46) return ("GIF", "gif", 1024);
            if (b[i] == 0x42 && b[i + 1] == 0x4D) return ("BMP", "bmp", 1024);
            if (i + 4 < len && b[i] == 0x25 && b[i + 1] == 0x50 && b[i + 2] == 0x44 && b[i + 3] == 0x46) return ("PDF", "pdf", 1024);
            if (b[i] == 0x50 && b[i + 1] == 0x4B && b[i + 2] == 0x03 && b[i + 3] == 0x04) return ("ZIP/Office", "zip", 1024);
            if (i + 5 < len && b[i] == 0x52 && b[i + 1] == 0x61 && b[i + 2] == 0x72 && b[i + 3] == 0x21) return ("RAR", "rar", 1024);
            if (i + 6 < len && b[i] == 0x37 && b[i + 1] == 0x7A && b[i + 2] == 0xBC) return ("7ZIP", "7z", 1024);
            if (i + 8 < len && b[i] == 0xD0 && b[i + 1] == 0xCF && b[i + 2] == 0x11 && b[i + 3] == 0xE0) return ("MS Office", "doc", 1024);
            if (b[i] == 0x49 && b[i + 1] == 0x44 && b[i + 2] == 0x33) return ("MP3", "mp3", 1024);
            if (b[i] == 0xFF && (b[i + 1] & 0xE0) == 0xE0) return ("MP3/AAC", "mp3", 1024);
            if (i + 12 < len && b[i] == 0x52 && b[i + 1] == 0x49 && b[i + 2] == 0x46 && b[i + 3] == 0x46 &&
                b[i + 8] == 0x57 && b[i + 9] == 0x41 && b[i + 10] == 0x56) return ("WAV", "wav", 1024);
            if (i + 4 < len && b[i] == 0x66 && b[i + 1] == 0x4C && b[i + 2] == 0x61 && b[i + 3] == 0x43) return ("FLAC", "flac", 1024);
            if (i + 4 < len && b[i] == 0x4F && b[i + 1] == 0x67 && b[i + 2] == 0x67 && b[i + 3] == 0x53) return ("OGG", "ogg", 1024);
            if (i >= 4 && i + 8 < len && b[i] == 0x66 && b[i + 1] == 0x74 && b[i + 2] == 0x79 && b[i + 3] == 0x70)
            {
                string brand = Encoding.ASCII.GetString(b, i + 4, 4);
                if (brand.StartsWith("qt")) return ("MOV", "mov", 4096);
                if (brand.StartsWith("3g")) return ("3GP", "3gp", 4096);
                if (brand.StartsWith("M4V") || brand.StartsWith("M4A")) return ("M4V", "m4v", 4096);
                return ("MP4", "mp4", 4096);
            }
            if (i + 12 < len && b[i] == 0x52 && b[i + 1] == 0x49 && b[i + 2] == 0x46 && b[i + 3] == 0x46 &&
                b[i + 8] == 0x41 && b[i + 9] == 0x56 && b[i + 10] == 0x49) return ("AVI", "avi", 4096);
            if (i + 4 < len && b[i] == 0x1A && b[i + 1] == 0x45 && b[i + 2] == 0xDF && b[i + 3] == 0xA3) return ("MKV", "mkv", 4096);
            if (i + 4 < len && b[i] == 0x30 && b[i + 1] == 0x26 && b[i + 2] == 0xB2 && b[i + 3] == 0x75) return ("WMV", "wmv", 4096);
            if (i + 4 < len && b[i] == 0x46 && b[i + 1] == 0x4C && b[i + 2] == 0x56 && b[i + 3] == 0x01) return ("FLV", "flv", 4096);
            return null;
        }

        long Extract(FileStream stream, long offset, string outPath, long minSize, CancellationToken token)
        {
            try
            {
                stream.Seek(offset, SeekOrigin.Begin);
                using (var output = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, BufSize))
                {
                    byte[] buf = new byte[BufSize];
                    long written = 0;
                    int zero = 0;

                    while (written < MaxFileSize)
                    {
                        if (token.IsCancellationRequested) break;
                        int r = stream.Read(buf, 0, BufSize);
                        if (r <= 0) break;

                        bool stop = false;
                        for (int j = 0; j < r; j++)
                        {
                            if (buf[j] == 0x00) zero++;
                            else zero = 0;
                            if (zero >= 1024 * 1024)
                            {
                                int cut = j - (1024 * 1024) + 1;
                                if (cut > 0) { output.Write(buf, 0, cut); written += cut; }
                                stop = true; break;
                            }
                        }
                        if (stop) break;

                        output.Write(buf, 0, r);
                        written += r;
                    }

                    if (written < minSize)
                    {
                        output.Close();
                        try { File.Delete(outPath); } catch { }
                        return 0;
                    }
                    return written;
                }
            }
            catch { return 0; }
        }

        void RunRepair(string op)
        {
            try
            {
                if (cmbDrives.SelectedItem == null) { MessageBox.Show("اختر الذاكرة."); return; }
                char L = GetLetter();
                string drive = L + ":";

                if (op.StartsWith("format"))
                {
                    if (MessageBox.Show($"تحذير: سيُمسح كل الذاكرة {drive}\n\nمتابعة؟", "تحذير",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                }

                Log(logRepair, $"العملية: {op} على {drive}", Info);

                Task.Run(() =>
                {
                    try
                    {
                        string file, args;
                        switch (op)
                        {
                            case "format_quick": file = "format.com"; args = $"{drive} /FS:exFAT /Q /Y"; break;
                            case "format_full": file = "format.com"; args = $"{drive} /FS:exFAT /Y"; break;
                            case "chkdsk_scan": file = "chkdsk.exe"; args = $"{drive}"; break;
                            case "chkdsk_fix": file = "chkdsk.exe"; args = $"{drive} /f /r"; break;
                            case "fix_boot": file = "bootsect.exe"; args = $"/nt60 {drive}"; break;
                            default: return;
                        }

                        var psi = new ProcessStartInfo(file, args)
                        {
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8
                        };

                        using (var p = Process.Start(psi))
                        {
                            var o = p.StandardOutput.ReadToEnd();
                            var er = p.StandardError.ReadToEnd();
                            p.WaitForExit();
                            if (!string.IsNullOrWhiteSpace(o)) Log(logRepair, o, TextMain);
                            if (!string.IsNullOrWhiteSpace(er)) Log(logRepair, "تحذير: " + er, Warning);
                            Log(logRepair, "انتهت العملية.", Success);
                        }
                    }
                    catch (Exception ex) { Log(logRepair, "خطأ: " + ex.Message, Error); }
                });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void BackupBoot()
        {
            try
            {
                if (cmbDrives.SelectedItem == null) return;
                char L = GetLetter();
                Task.Run(() =>
                {
                    try
                    {
                        using (var s = new FileStream($@"\\.\{L}:", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            byte[] boot = new byte[512];
                            s.Read(boot, 0, 512);
                            string path = Path.Combine(Environment.CurrentDirectory,
                                $"boot_{L}_{DateTime.Now:yyyyMMdd_HHmmss}.bin");
                            File.WriteAllBytes(path, boot);
                            Log(logRepair, $"حفظ في: {path}", Success);
                        }
                    }
                    catch (Exception ex) { Log(logRepair, "خطأ: " + ex.Message, Error); }
                });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void RestoreBoot()
        {
            try
            {
                if (cmbDrives.SelectedItem == null) return;
                using (var ofd = new OpenFileDialog { Filter = "Boot Backup|*.bin" })
                {
                    if (ofd.ShowDialog() != DialogResult.OK) return;
                    char L = GetLetter();
                    if (MessageBox.Show($"تحذير: سيُستبدل Boot Sector للذاكرة {L}:\\\n\nمتابعة؟",
                        "تحذير", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                    Task.Run(() =>
                    {
                        try
                        {
                            byte[] boot = File.ReadAllBytes(ofd.FileName);
                            using (var d = new FileStream($@"\\.\{L}:", FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                            {
                                d.Seek(0, SeekOrigin.Begin);
                                d.Write(boot, 0, Math.Min(512, boot.Length));
                                d.Flush();
                            }
                            Log(logRepair, "تم استعادة Boot Sector.", Success);
                        }
                        catch (Exception ex) { Log(logRepair, "خطأ: " + ex.Message, Error); }
                    });
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void BtnBadStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbDrives.SelectedItem == null) { MessageBox.Show("اختر الذاكرة."); return; }
                char L = GetLetter();
                cts = new CancellationTokenSource();
                btnBadStart.Enabled = false;
                btnBadStop.Enabled = true;
                logBad.Clear();
                pbBad.Value = 0;
                var token = cts.Token;
                Task.Run(() => BadScan(L, token));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void BadScan(char L, CancellationToken token)
        {
            Log(logBad, "===============================================", Info);
            Log(logBad, "  فحص القطاعات (وضع القراءة فقط)", Info);
            Log(logBad, "===============================================", Info);

            try
            {
                using (var s = new FileStream($@"\\.\{L}:", FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufSize))
                {
                    long total = s.Length;
                    long sectors = total / 512;
                    Log(logBad, $"القطاعات: {sectors:N0}", Info);
                    Log(logBad, "جارٍ الفحص...", Warning);

                    byte[] buf = new byte[BufSize];
                    long pos = 0;
                    int bad = 0;

                    while (pos < total)
                    {
                        if (token.IsCancellationRequested) { Log(logBad, "إيقاف.", Warning); break; }
                        try
                        {
                            int r = s.Read(buf, 0, BufSize);
                            if (r <= 0) break;
                            pos += r;
                        }
                        catch
                        {
                            bad++;
                            Log(logBad, $"قطاع تالف @ {pos:N0}", Error);
                            pos += BufSize;
                        }
                        int pct = (int)((pos * 100) / total);
                        UpdatePb(pbBad, null, pct);
                    }

                    Log(logBad, "-----------------------------------------------", CardBorder);
                    if (bad == 0) Log(logBad, "لا توجد قطاعات تالفة.", Success);
                    else Log(logBad, $"عدد القطاعات التالفة: {bad}", Warning);
                }
            }
            catch (Exception ex) { Log(logBad, "خطأ: " + ex.Message, Error); }
            finally { Invoke((Action)(() => { btnBadStart.Enabled = true; btnBadStop.Enabled = false; })); }
        }

        void BtnSpeedStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbDrives.SelectedItem == null) { MessageBox.Show("اختر الذاكرة."); return; }
                char L = GetLetter();
                btnSpeedStart.Enabled = false;
                logSpeed.Clear();
                pbSpeed.Value = 0;
                Task.Run(() => SpeedTest(L));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void SpeedTest(char L)
        {
            string root = L + ":\\";
            string testFile = Path.Combine(root, "memfixer_test.tmp");
            const int mb = 100;
            int bytes = mb * 1024 * 1024;

            Log(logSpeed, "===============================================", Info);
            Log(logSpeed, "  اختبار السرعة", Info);
            Log(logSpeed, "===============================================", Info);

            try
            {
                byte[] buf = new byte[BufSize];
                new Random(42).NextBytes(buf);

                Log(logSpeed, $"كتابة {mb} ميجا...", Warning);
                var sw = Stopwatch.StartNew();
                using (var fs = new FileStream(testFile, FileMode.Create, FileAccess.Write, FileShare.None, BufSize))
                {
                    int w = 0;
                    while (w < bytes)
                    {
                        int c = Math.Min(BufSize, bytes - w);
                        fs.Write(buf, 0, c);
                        w += c;
                        UpdatePb(pbSpeed, null, (int)((w * 50L) / bytes));
                    }
                    fs.Flush(true);
                }
                sw.Stop();
                double ws = (bytes / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
                Log(logSpeed, $"سرعة الكتابة: {ws:F2} MB/s", Success);

                Log(logSpeed, $"قراءة {mb} ميجا...", Warning);
                sw.Restart();
                using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.Read, BufSize))
                {
                    int r = 0;
                    while (r < bytes)
                    {
                        int c = fs.Read(buf, 0, BufSize);
                        if (c <= 0) break;
                        r += c;
                        UpdatePb(pbSpeed, null, 50 + (int)((r * 50L) / bytes));
                    }
                }
                sw.Stop();
                double rs = (bytes / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
                Log(logSpeed, $"سرعة القراءة: {rs:F2} MB/s", Success);

                Log(logSpeed, "-----------------------------------------------", CardBorder);
                Log(logSpeed, $"الملخص: كتابة {ws:F2} MB/s | قراءة {rs:F2} MB/s", Info);

                string cls = ws >= 80 ? "UHS-II عالي" :
                             ws >= 30 ? "UHS-I / Class 10" :
                             ws >= 10 ? "Class 6-10" :
                             ws >= 4 ? "Class 4-6" : "Class 2-4 (بطيء)";
                Log(logSpeed, $"التصنيف: {cls}", Info);
            }
            catch (Exception ex) { Log(logSpeed, "خطأ: " + ex.Message, Error); }
            finally
            {
                try { if (File.Exists(testFile)) File.Delete(testFile); } catch { }
                Invoke((Action)(() => { btnSpeedStart.Enabled = true; }));
            }
        }

        void Log(RichTextBox box, string msg, Color color)
        {
            if (box.InvokeRequired) { box.Invoke((Action)(() => Log(box, msg, color))); return; }
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionColor = color;
            box.AppendText(msg + Environment.NewLine);
            box.SelectionColor = box.ForeColor;
            box.ScrollToCaret();
        }

        void UpdatePb(ProgressBar pb, Label lbl, int pct)
        {
            if (pb.InvokeRequired) { pb.Invoke((Action)(() => UpdatePb(pb, lbl, pct))); return; }
            pb.Value = Math.Min(100, Math.Max(0, pct));
            if (lbl != null) lbl.Text = pct + "%";
        }

        void SetLabel(Label lbl, string text, Color color)
        {
            if (lbl.InvokeRequired) { lbl.Invoke((Action)(() => SetLabel(lbl, text, color))); return; }
            lbl.Text = text;
            lbl.ForeColor = color;
        }
    }
}
