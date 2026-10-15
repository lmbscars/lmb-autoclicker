using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace OPAutoClicker
{
    // ==========================================
    // WIN32 NATIVE METHODS & CONSTANTS
    // ==========================================
    internal static class NativeMethods
    {
        public const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP     = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        public const uint MOUSEEVENTF_MIDDLEUP   = 0x0040;

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 0x2;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint uMilliseconds);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
    }

    // ==========================================
    // APP CONFIG & SETTINGS
    // ==========================================
    public class Settings
    {
        public int Hours = 0;
        public int Mins = 0;
        public int Secs = 0;
        public int Milliseconds = 100;
        public bool RandomOffsetEnabled = false;
        public int RandomOffset = 40;
        public string MouseButton = "Left";
        public string ClickType = "Single";
        public bool RepeatTimesSelected = false;
        public int RepeatCount = 1;
        public bool RepeatUntilStopped = true;
        public bool CursorCurrentLocation = true;
        public int CursorX = 0;
        public int CursorY = 0;
        public Keys Hotkey = Keys.F6;
        public string CustomBackgroundPath = "";
        public string TextColorHex = "#00E6FF";

        public static string GetConfigPath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDir, "autoclicker_config.ini");
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Hours=" + Hours);
                sb.AppendLine("Mins=" + Mins);
                sb.AppendLine("Secs=" + Secs);
                sb.AppendLine("Milliseconds=" + Milliseconds);
                sb.AppendLine("RandomOffsetEnabled=" + RandomOffsetEnabled);
                sb.AppendLine("RandomOffset=" + RandomOffset);
                sb.AppendLine("MouseButton=" + MouseButton);
                sb.AppendLine("ClickType=" + ClickType);
                sb.AppendLine("RepeatTimesSelected=" + RepeatTimesSelected);
                sb.AppendLine("RepeatCount=" + RepeatCount);
                sb.AppendLine("RepeatUntilStopped=" + RepeatUntilStopped);
                sb.AppendLine("CursorCurrentLocation=" + CursorCurrentLocation);
                sb.AppendLine("CursorX=" + CursorX);
                sb.AppendLine("CursorY=" + CursorY);
                sb.AppendLine("Hotkey=" + (int)Hotkey);
                sb.AppendLine("CustomBackgroundPath=" + (CustomBackgroundPath ?? ""));
                sb.AppendLine("TextColorHex=" + (TextColorHex ?? ""));
                File.WriteAllText(GetConfigPath(), sb.ToString());
            }
            catch { }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (string line in lines)
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0)
                        {
                            string k = line.Substring(0, eq).Trim();
                            string v = line.Substring(eq + 1).Trim();
                            if (k == "Hours") int.TryParse(v, out s.Hours);
                            else if (k == "Mins") int.TryParse(v, out s.Mins);
                            else if (k == "Secs") int.TryParse(v, out s.Secs);
                            else if (k == "Milliseconds") int.TryParse(v, out s.Milliseconds);
                            else if (k == "RandomOffsetEnabled") bool.TryParse(v, out s.RandomOffsetEnabled);
                            else if (k == "RandomOffset") int.TryParse(v, out s.RandomOffset);
                            else if (k == "MouseButton") s.MouseButton = v;
                            else if (k == "ClickType") s.ClickType = v;
                            else if (k == "RepeatTimesSelected") bool.TryParse(v, out s.RepeatTimesSelected);
                            else if (k == "RepeatCount") int.TryParse(v, out s.RepeatCount);
                            else if (k == "RepeatUntilStopped") bool.TryParse(v, out s.RepeatUntilStopped);
                            else if (k == "CursorCurrentLocation") bool.TryParse(v, out s.CursorCurrentLocation);
                            else if (k == "CursorX") int.TryParse(v, out s.CursorX);
                            else if (k == "CursorY") int.TryParse(v, out s.CursorY);
                            else if (k == "Hotkey") { int h; if (int.TryParse(v, out h)) s.Hotkey = (Keys)h; }
                            else if (k == "CustomBackgroundPath") s.CustomBackgroundPath = v;
                            else if (k == "TextColorHex") s.TextColorHex = v;
                        }
                    }
                }
            }
            catch { }
            return s;
        }
    }

    // ==========================================
    // MAIN APPLICATION FORM (CYBER GLASS UI + GIF SUPPORT)
    // ==========================================
    public class MainForm : Form
    {
        private Settings settings;

        // Custom Background / GIF
        private Image customBackground = null;
        private bool isCustomGif = false;

        // Click interval controls
        private TextBox txtHours;
        private TextBox txtMins;
        private TextBox txtSecs;
        private TextBox txtMillis;
        private CheckBox chkRandomOffset;
        private TextBox txtRandomOffset;
        private Button btnHelpRandom;

        // Click options controls
        private ComboBox cboMouseButton;
        private ComboBox cboClickType;

        // Click repeat controls
        private RadioButton radRepeatTimes;
        private NumericUpDown numRepeatCount;
        private RadioButton radRepeatUntilStopped;

        // Cursor position controls
        private RadioButton radCurrentLocation;
        private RadioButton radPickLocation;
        private Button btnPickLocation;
        private TextBox txtCoordX;
        private TextBox txtCoordY;

        // Action buttons
        private Button btnStart;
        private Button btnStop;
        private Button btnHotkeySetting;
        private Label lblStatus;

        // Title bar buttons & Theme Color
        private Button btnClose;
        private Button btnMinimize;
        private Button btnResetBg;
        private Button btnTopBg;
        private Button btnTextColor;
        private Button btnDiscord;
        private Color currentTextColor = Color.FromArgb(0, 230, 255);

        // Click Engine
        private Thread clickThread;
        private volatile bool isClicking = false;
        private Random rand = new Random();

        // Hotkey polling timer
        private System.Windows.Forms.Timer hotkeyTimer;
        private long lastHotkeyTick = 0;

        public MainForm()
        {
            settings = Settings.Load();
            InitializeComponent();
            ApplySettingsToUI();
            SetupHotkeyTimer();

            // Load saved custom background or GIF if available
            if (!string.IsNullOrEmpty(settings.CustomBackgroundPath) && File.Exists(settings.CustomBackgroundPath))
            {
                LoadCustomBackground(settings.CustomBackgroundPath);
            }

            this.ActiveControl = btnStart;
        }

        private void InitializeComponent()
        {
            this.Text = ".gg/lmb auto clicker";
            this.ClientSize = new Size(540, 525);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);

            // Create Smooth Rounded Window Corners
            this.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, this.Width, this.Height, 26, 26));

            // Load App Icon (from app.ico or extracted from embedded exe resource)
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                    this.Icon = new Icon(iconPath);
                else
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            // ----------------------------------------------------
            // 0. Completely See-Through Header Bar Buttons
            // ----------------------------------------------------
            btnClose = new Button();
            btnClose.Text = "✕";
            btnClose.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnClose.ForeColor = Color.FromArgb(200, 215, 235);
            btnClose.BackColor = Color.Transparent;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 25, 65);
            btnClose.Size = new Size(36, 30);
            btnClose.Location = new Point(this.Width - 44, 8);
            btnClose.Click += (s, e) => this.Close();

            btnMinimize = new Button();
            btnMinimize.Text = "─";
            btnMinimize.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnMinimize.ForeColor = Color.FromArgb(200, 215, 235);
            btnMinimize.BackColor = Color.Transparent;
            btnMinimize.FlatStyle = FlatStyle.Flat;
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 50, 75);
            btnMinimize.Size = new Size(36, 30);
            btnMinimize.Location = new Point(this.Width - 82, 8);
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            btnResetBg = new Button();
            btnResetBg.Text = "[Reset]";
            btnResetBg.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnResetBg.ForeColor = Color.FromArgb(0, 225, 255);
            btnResetBg.BackColor = Color.Transparent;
            btnResetBg.FlatStyle = FlatStyle.Flat;
            btnResetBg.FlatAppearance.BorderSize = 0;
            btnResetBg.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 60, 90);
            btnResetBg.Size = new Size(56, 30);
            btnResetBg.Location = new Point(this.Width - 140, 8);
            btnResetBg.Click += (s, e) => ResetBackground();

            btnTopBg = new Button();
            btnTopBg.Text = "[+ BG]";
            btnTopBg.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnTopBg.ForeColor = Color.FromArgb(0, 225, 255);
            btnTopBg.BackColor = Color.Transparent;
            btnTopBg.FlatStyle = FlatStyle.Flat;
            btnTopBg.FlatAppearance.BorderSize = 0;
            btnTopBg.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 60, 90);
            btnTopBg.Size = new Size(56, 30);
            btnTopBg.Location = new Point(this.Width - 198, 8);
            btnTopBg.Click += (s, e) => PromptImportBackground();

            btnTextColor = new Button();
            btnTextColor.Text = "🎨 Color";
            btnTextColor.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnTextColor.ForeColor = Color.FromArgb(0, 225, 255);
            btnTextColor.BackColor = Color.Transparent;
            btnTextColor.FlatStyle = FlatStyle.Flat;
            btnTextColor.FlatAppearance.BorderSize = 0;
            btnTextColor.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 60, 90);
            btnTextColor.Size = new Size(66, 30);
            btnTextColor.Location = new Point(this.Width - 266, 8);
            btnTextColor.Click += (s, e) => ShowTextColorMenu(btnTextColor);

            btnDiscord = new Button();
            btnDiscord.Text = "Discord";
            btnDiscord.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnDiscord.ForeColor = Color.FromArgb(0, 225, 255);
            btnDiscord.BackColor = Color.Transparent;
            btnDiscord.FlatStyle = FlatStyle.Flat;
            btnDiscord.FlatAppearance.BorderSize = 0;
            btnDiscord.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 60, 90);
            btnDiscord.Size = new Size(66, 30);
            btnDiscord.Location = new Point(this.Width - 334, 8);
            btnDiscord.Click += (s, e) => OpenDiscordLink();

            // ----------------------------------------------------
            // 1. Controls for: // CLICK INTERVAL (Y: 48 to 154)
            // ----------------------------------------------------
            txtHours = CreateStyledTextBox("0", new Point(32, 78), 58);
            Label lblHours = CreateStyledLabel("hours", new Point(94, 81));

            txtMins = CreateStyledTextBox("0", new Point(144, 78), 58);
            Label lblMins = CreateStyledLabel("mins", new Point(206, 81));

            txtSecs = CreateStyledTextBox("0", new Point(252, 78), 58);
            Label lblSecs = CreateStyledLabel("secs", new Point(314, 81));

            txtMillis = CreateStyledTextBox("100", new Point(356, 78), 66);
            Label lblMillis = CreateStyledLabel("milliseconds", new Point(426, 81));

            // Help button [?]
            btnHelpRandom = new Button();
            btnHelpRandom.Text = "?";
            btnHelpRandom.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnHelpRandom.ForeColor = Color.FromArgb(0, 220, 255);
            btnHelpRandom.BackColor = Color.FromArgb(30, 20, 30, 48);
            btnHelpRandom.FlatStyle = FlatStyle.Flat;
            btnHelpRandom.FlatAppearance.BorderColor = Color.FromArgb(80, 0, 180, 230);
            btnHelpRandom.Location = new Point(32, 114);
            btnHelpRandom.Size = new Size(24, 24);
            btnHelpRandom.UseVisualStyleBackColor = false;
            btnHelpRandom.Click += BtnHelpRandom_Click;

            chkRandomOffset = new CheckBox();
            chkRandomOffset.Text = "Random offset ±";
            chkRandomOffset.ForeColor = Color.FromArgb(225, 235, 250);
            chkRandomOffset.BackColor = Color.Transparent;
            chkRandomOffset.AutoSize = true;
            chkRandomOffset.Location = new Point(62, 116);

            txtRandomOffset = CreateStyledTextBox("40", new Point(185, 114), 65);
            Label lblRandomMs = CreateStyledLabel("milliseconds", new Point(255, 116));

            // ----------------------------------------------------
            // 2. Controls for: // CLICK OPTIONS (Y: 162 to 277)
            // ----------------------------------------------------
            Label lblMouseBtn = CreateStyledLabel("Mouse button:", new Point(30, 198));
            cboMouseButton = new ComboBox();
            cboMouseButton.DropDownStyle = ComboBoxStyle.DropDownList;
            cboMouseButton.Items.AddRange(new object[] { "Left", "Right", "Middle" });
            cboMouseButton.SelectedIndex = 0;
            cboMouseButton.BackColor = Color.FromArgb(16, 22, 34);
            cboMouseButton.ForeColor = Color.FromArgb(0, 230, 255);
            cboMouseButton.FlatStyle = FlatStyle.Flat;
            cboMouseButton.Location = new Point(132, 194);
            cboMouseButton.Size = new Size(118, 23);

            Label lblClickType = CreateStyledLabel("Click type:", new Point(30, 234));
            cboClickType = new ComboBox();
            cboClickType.DropDownStyle = ComboBoxStyle.DropDownList;
            cboClickType.Items.AddRange(new object[] { "Single", "Double" });
            cboClickType.SelectedIndex = 0;
            cboClickType.BackColor = Color.FromArgb(16, 22, 34);
            cboClickType.ForeColor = Color.FromArgb(0, 230, 255);
            cboClickType.FlatStyle = FlatStyle.Flat;
            cboClickType.Location = new Point(132, 230);
            cboClickType.Size = new Size(118, 23);

            // ----------------------------------------------------
            // 3. Controls for: // CLICK REPEAT (Y: 162 to 277)
            // ----------------------------------------------------
            radRepeatTimes = new RadioButton();
            radRepeatTimes.Text = "Repeat";
            radRepeatTimes.ForeColor = Color.FromArgb(225, 235, 250);
            radRepeatTimes.BackColor = Color.Transparent;
            radRepeatTimes.AutoSize = true;
            radRepeatTimes.Location = new Point(288, 196);

            numRepeatCount = new NumericUpDown();
            numRepeatCount.Minimum = 1;
            numRepeatCount.Maximum = 9999999;
            numRepeatCount.Value = 1;
            numRepeatCount.BackColor = Color.FromArgb(16, 22, 34);
            numRepeatCount.ForeColor = Color.FromArgb(0, 230, 255);
            numRepeatCount.Location = new Point(360, 194);
            numRepeatCount.Size = new Size(68, 23);
            numRepeatCount.TextAlign = HorizontalAlignment.Center;
            numRepeatCount.Enter += (s, e) => radRepeatTimes.Checked = true;
            numRepeatCount.ValueChanged += (s, e) => radRepeatTimes.Checked = true;

            Label lblTimes = CreateStyledLabel("times", new Point(434, 196));
            lblTimes.Click += (s, e) => radRepeatTimes.Checked = true;

            radRepeatUntilStopped = new RadioButton();
            radRepeatUntilStopped.Text = "Repeat until stopped";
            radRepeatUntilStopped.ForeColor = Color.FromArgb(225, 235, 250);
            radRepeatUntilStopped.BackColor = Color.Transparent;
            radRepeatUntilStopped.AutoSize = true;
            radRepeatUntilStopped.Location = new Point(288, 232);
            radRepeatUntilStopped.Checked = true;

            // ----------------------------------------------------
            // 4. Controls for: // CURSOR POSITION (Y: 285 to 363)
            // ----------------------------------------------------
            radCurrentLocation = new RadioButton();
            radCurrentLocation.Text = "Current location";
            radCurrentLocation.ForeColor = Color.FromArgb(225, 235, 250);
            radCurrentLocation.BackColor = Color.Transparent;
            radCurrentLocation.AutoSize = true;
            radCurrentLocation.Location = new Point(32, 321);
            radCurrentLocation.Checked = true;

            radPickLocation = new RadioButton();
            radPickLocation.Text = "";
            radPickLocation.BackColor = Color.Transparent;
            radPickLocation.AutoSize = true;
            radPickLocation.Location = new Point(170, 323);

            btnPickLocation = new Button();
            btnPickLocation.Text = "[+] Pick Location";
            btnPickLocation.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnPickLocation.ForeColor = Color.FromArgb(0, 230, 255);
            btnPickLocation.BackColor = Color.FromArgb(35, 20, 30, 48);
            btnPickLocation.FlatStyle = FlatStyle.Flat;
            btnPickLocation.FlatAppearance.BorderColor = Color.FromArgb(90, 0, 180, 230);
            btnPickLocation.Location = new Point(190, 316);
            btnPickLocation.Size = new Size(118, 32);
            btnPickLocation.UseVisualStyleBackColor = false;
            btnPickLocation.Click += BtnPickLocation_Click;

            Label lblX = CreateStyledLabel("X", new Point(320, 321));
            txtCoordX = CreateStyledTextBox("0", new Point(338, 318), 65);
            txtCoordX.TextChanged += (s, e) => radPickLocation.Checked = true;

            Label lblY = CreateStyledLabel("Y", new Point(415, 321));
            txtCoordY = CreateStyledTextBox("0", new Point(433, 318), 65);
            txtCoordY.TextChanged += (s, e) => radPickLocation.Checked = true;

            // ----------------------------------------------------
            // 5. Action Buttons & Live Status
            // ----------------------------------------------------
            btnStart = new Button();
            btnStart.Text = "▶  START (" + settings.Hotkey + ")";
            btnStart.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnStart.ForeColor = Color.White;
            btnStart.BackColor = Color.FromArgb(220, 0, 200, 115);
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Location = new Point(18, 372);
            btnStart.Size = new Size(246, 52);
            btnStart.UseVisualStyleBackColor = false;
            btnStart.Click += (s, e) => StartClicking();

            btnStop = new Button();
            btnStop.Text = "⏹  STOP (" + settings.Hotkey + ")";
            btnStop.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnStop.ForeColor = Color.FromArgb(200, 200, 200);
            btnStop.BackColor = Color.FromArgb(160, 45, 52, 65);
            btnStop.FlatStyle = FlatStyle.Flat;
            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.Location = new Point(276, 372);
            btnStop.Size = new Size(246, 52);
            btnStop.UseVisualStyleBackColor = false;
            btnStop.Enabled = false;
            btnStop.Click += (s, e) => StopClicking();

            btnHotkeySetting = new Button();
            btnHotkeySetting.Text = "⚙ Hotkey Setting (" + settings.Hotkey + ")";
            btnHotkeySetting.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnHotkeySetting.ForeColor = Color.FromArgb(0, 220, 255);
            btnHotkeySetting.BackColor = Color.FromArgb(40, 20, 30, 48);
            btnHotkeySetting.FlatStyle = FlatStyle.Flat;
            btnHotkeySetting.FlatAppearance.BorderColor = Color.FromArgb(70, 0, 160, 210);
            btnHotkeySetting.Location = new Point(18, 432);
            btnHotkeySetting.Size = new Size(504, 38);
            btnHotkeySetting.UseVisualStyleBackColor = false;
            btnHotkeySetting.Click += BtnHotkeySetting_Click;

            lblStatus = new Label();
            lblStatus.Text = "● Status: IDLE (Press " + settings.Hotkey + " to start)";
            lblStatus.ForeColor = Color.FromArgb(140, 175, 210);
            lblStatus.BackColor = Color.Transparent;
            lblStatus.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            lblStatus.Location = new Point(18, 480);
            lblStatus.Size = new Size(504, 26);

            this.Controls.AddRange(new Control[] {
                btnClose, btnMinimize, btnResetBg, btnTopBg, btnTextColor, btnDiscord,
                txtHours, lblHours, txtMins, lblMins, txtSecs, lblSecs, txtMillis, lblMillis,
                btnHelpRandom, chkRandomOffset, txtRandomOffset, lblRandomMs,
                lblMouseBtn, cboMouseButton, lblClickType, cboClickType,
                radRepeatTimes, numRepeatCount, lblTimes, radRepeatUntilStopped,
                radCurrentLocation, radPickLocation, btnPickLocation, lblX, txtCoordX, lblY, txtCoordY,
                btnStart, btnStop, btnHotkeySetting, lblStatus
            });

            this.FormClosing += MainForm_FormClosing;

            // Drag from completely see-through header
            this.MouseDown += Header_MouseDown;

            // Right-click window background to change text color
            this.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    ShowTextColorMenu(this, Cursor.Position);
                }
            };
        }

        private TextBox CreateStyledTextBox(string defaultText, Point loc, int width)
        {
            TextBox txt = new TextBox();
            txt.Text = defaultText;
            txt.Location = loc;
            txt.Size = new Size(width, 24);
            txt.BackColor = Color.FromArgb(18, 22, 34);
            txt.ForeColor = Color.FromArgb(0, 240, 255);
            txt.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            txt.BorderStyle = BorderStyle.FixedSingle;
            txt.TextAlign = HorizontalAlignment.Right;
            txt.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                {
                    e.Handled = true;
                }
            };
            return txt;
        }

        private Label CreateStyledLabel(string text, Point loc)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.AutoSize = true;
            lbl.Location = loc;
            lbl.ForeColor = Color.FromArgb(210, 225, 245);
            lbl.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lbl.BackColor = Color.Transparent;
            return lbl;
        }

        // ==========================================
        // CUSTOM PAINTING (SEE-THROUGH GLASS + CUSTOM BG / GIF)
        // ==========================================
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Draw Custom Background Image / Animated GIF (or Default Cyber Background)
            if (customBackground != null)
            {
                if (isCustomGif)
                {
                    ImageAnimator.UpdateFrames();
                }
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(customBackground, this.ClientRectangle);

                // Subtle dark vignette overlay so text remains readable over bright images/GIFs
                using (SolidBrush darkWash = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                {
                    g.FillRectangle(darkWash, this.ClientRectangle);
                }
            }
            else
            {
                // Default Rich Cyber Dark Canvas
                Rectangle fullRect = this.ClientRectangle;
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                    fullRect,
                    Color.FromArgb(16, 20, 32),
                    Color.FromArgb(8, 10, 16),
                    55f))
                {
                    g.FillRectangle(bgBrush, fullRect);
                }

                // Ambient cyan glow
                using (GraphicsPath glow1 = new GraphicsPath())
                {
                    glow1.AddEllipse(-80, -80, 320, 320);
                    using (PathGradientBrush pgb1 = new PathGradientBrush(glow1))
                    {
                        pgb1.CenterColor = Color.FromArgb(50, 0, 210, 255);
                        pgb1.SurroundColors = new Color[] { Color.Transparent };
                        g.FillPath(pgb1, glow1);
                    }
                }

                // Ambient purple glow
                using (GraphicsPath glow2 = new GraphicsPath())
                {
                    glow2.AddEllipse(this.Width - 220, this.Height - 220, 300, 300);
                    using (PathGradientBrush pgb2 = new PathGradientBrush(glow2))
                    {
                        pgb2.CenterColor = Color.FromArgb(40, 140, 0, 255);
                        pgb2.SurroundColors = new Color[] { Color.Transparent };
                        g.FillPath(pgb2, glow2);
                    }
                }

                // Subtle background grid
                using (Pen gridPen = new Pen(Color.FromArgb(14, 255, 255, 255), 1f))
                {
                    for (int x = 20; x < this.Width; x += 40)
                        g.DrawLine(gridPen, x, 0, x, this.Height);
                    for (int y = 40; y < this.Height; y += 40)
                        g.DrawLine(gridPen, 0, y, this.Width, y);
                }
            }

            // ----------------------------------------------------
            // 2. Ultra See-Through Glass Cards (~10% opacity)
            // ----------------------------------------------------
            // Card 1: // CLICK INTERVAL
            DrawSeeThroughCard(g, new Rectangle(18, 48, 504, 106), "// CLICK INTERVAL");

            // Card 2: // CLICK OPTIONS
            DrawSeeThroughCard(g, new Rectangle(18, 162, 246, 115), "// CLICK OPTIONS");

            // Card 3: // CLICK REPEAT
            DrawSeeThroughCard(g, new Rectangle(276, 162, 246, 115), "// CLICK REPEAT");

            // Card 4: // CURSOR POSITION
            DrawSeeThroughCard(g, new Rectangle(18, 285, 504, 78), "// CURSOR POSITION");

            // ----------------------------------------------------
            // 3. Completely See-Through Header Bar (Floating Title)
            // ----------------------------------------------------
            double luminance = (0.299 * currentTextColor.R + 0.587 * currentTextColor.G + 0.114 * currentTextColor.B);
            bool isDarkText = luminance < 120;

            using (Font brandFont = new Font("Segoe UI", 10.5F, FontStyle.Bold))
            using (Font titleFont = new Font("Segoe UI", 10.5F, FontStyle.Bold))
            using (SolidBrush brandBrush = new SolidBrush(currentTextColor))
            using (SolidBrush textBrush = new SolidBrush(isDarkText ? Color.FromArgb(30, 35, 45) : Color.FromArgb(245, 250, 255)))
            {
                g.DrawString(".gg/lmb", brandFont, brandBrush, 18, 12);
                int offset = (int)g.MeasureString(".gg/lmb", brandFont).Width - 2;
                g.DrawString(" auto clicker", titleFont, textBrush, 18 + offset, 12);
            }

            // ----------------------------------------------------
            // 4. Smooth Outer Window Rounded Border
            // ----------------------------------------------------
            using (Pen borderPen = new Pen(Color.FromArgb(90, currentTextColor), 1.5f))
            {
                using (GraphicsPath bp = GetRoundPath(new Rectangle(1, 1, this.Width - 2, this.Height - 2), 24))
                {
                    g.DrawPath(borderPen, bp);
                }
            }
        }

        private void DrawSeeThroughCard(Graphics g, Rectangle rect, string title)
        {
            double luminance = (0.299 * currentTextColor.R + 0.587 * currentTextColor.G + 0.114 * currentTextColor.B);
            bool isDarkText = luminance < 120;

            using (GraphicsPath path = GetRoundPath(rect, 14))
            {
                // Ultra see-through glass tint
                Color cardBg = isDarkText ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(22, 10, 16, 26);
                using (SolidBrush b = new SolidBrush(cardBg))
                {
                    g.FillPath(b, path);
                }

                // Luminous subtle border
                using (Pen p = new Pen(Color.FromArgb(60, currentTextColor), 1.2f))
                {
                    g.DrawPath(p, path);
                }
            }

            if (!string.IsNullOrEmpty(title))
            {
                using (Font f = new Font("Segoe UI", 8.8F, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(currentTextColor))
                {
                    g.DrawString(title, f, tb, rect.X + 14, rect.Y + 8);
                }
            }
        }

        private GraphicsPath GetRoundPath(Rectangle r, int d)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Y <= 42)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(this.Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
            }
        }

        // ==========================================
        // CUSTOM BACKGROUND / ANIMATED GIF HANDLER
        // ==========================================
        private void PromptImportBackground()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select Custom Background Image or Animated GIF";
                ofd.Filter = "Image & GIF Files (*.gif;*.png;*.jpg;*.jpeg;*.bmp)|*.gif;*.png;*.jpg;*.jpeg;*.bmp|GIF Animations (*.gif)|*.gif|All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadCustomBackground(ofd.FileName);
                    settings.CustomBackgroundPath = ofd.FileName;
                    settings.Save();
                }
            }
        }

        private void LoadCustomBackground(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                // Clean up previous image/GIF animation
                if (customBackground != null)
                {
                    if (isCustomGif)
                    {
                        ImageAnimator.StopAnimate(customBackground, OnFrameChanged);
                    }
                    customBackground.Dispose();
                    customBackground = null;
                }

                // Load bytes without locking file on disk
                byte[] bytes = File.ReadAllBytes(path);
                MemoryStream ms = new MemoryStream(bytes);
                Image img = Image.FromStream(ms);
                customBackground = img;

                if (ImageAnimator.CanAnimate(img))
                {
                    isCustomGif = true;
                    ImageAnimator.Animate(img, OnFrameChanged);
                }
                else
                {
                    isCustomGif = false;
                }

                this.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load image: " + ex.Message, "Image Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnFrameChanged(object sender, EventArgs e)
        {
            if (!this.IsDisposed && this.IsHandleCreated)
            {
                this.BeginInvoke(new Action(() =>
                {
                    ImageAnimator.UpdateFrames();
                    this.Invalidate();
                }));
            }
        }

        private void ResetBackground()
        {
            if (customBackground != null)
            {
                if (isCustomGif)
                {
                    ImageAnimator.StopAnimate(customBackground, OnFrameChanged);
                }
                customBackground.Dispose();
                customBackground = null;
            }
            isCustomGif = false;
            settings.CustomBackgroundPath = "";
            settings.Save();
            this.Invalidate();
        }

        private void BtnHelpRandom_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Random offset ±:\n\n" +
                "When checked, adds or subtracts a random number of milliseconds (up to the entered offset value) to each click interval.\n\n" +
                "This creates natural human-like variation in clicking speed, which helps prevent anti-cheat and automated bot detection in games and applications.",
                "Random Offset Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ==========================================
        // TEXT COLOR THEME MANAGEMENT
        // ==========================================
        private void ShowTextColorMenu(Control anchor, Point? screenPos = null)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = true;
            menu.BackColor = Color.FromArgb(20, 24, 36);
            menu.ForeColor = Color.FromArgb(235, 245, 255);
            menu.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            ToolStripMenuItem itemCustom = new ToolStripMenuItem("🎨  Custom Color Picker...");
            itemCustom.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            itemCustom.ForeColor = currentTextColor;
            itemCustom.Click += (s, e) =>
            {
                using (ColorDialog cd = new ColorDialog())
                {
                    cd.AllowFullOpen = true;
                    cd.AnyColor = true;
                    cd.Color = currentTextColor;
                    if (cd.ShowDialog(this) == DialogResult.OK)
                    {
                        ApplyThemeTextColor(cd.Color);
                    }
                }
            };
            menu.Items.Add(itemCustom);
            menu.Items.Add(new ToolStripSeparator());

            AddColorPreset(menu, "Neon Cyan (Default)", Color.FromArgb(0, 230, 255));
            AddColorPreset(menu, "Crisp White", Color.FromArgb(255, 255, 255));
            AddColorPreset(menu, "Matrix Green", Color.FromArgb(0, 255, 136));
            AddColorPreset(menu, "Cyber Gold / Yellow", Color.FromArgb(255, 215, 0));
            AddColorPreset(menu, "Neon Coral / Orange", Color.FromArgb(255, 110, 60));
            AddColorPreset(menu, "Cyber Purple", Color.FromArgb(190, 75, 255));
            AddColorPreset(menu, "Hot Pink / Rose", Color.FromArgb(255, 60, 150));
            AddColorPreset(menu, "Crimson Red", Color.FromArgb(255, 50, 70));
            AddColorPreset(menu, "Deep Charcoal (for light BG)", Color.FromArgb(30, 35, 45));

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemReset = new ToolStripMenuItem("↺  Reset to Default (Cyan)");
            itemReset.ForeColor = Color.FromArgb(200, 215, 235);
            itemReset.Click += (s, e) => ApplyThemeTextColor(Color.FromArgb(0, 230, 255));
            menu.Items.Add(itemReset);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemDiscord = new ToolStripMenuItem("💬  Join Discord (discord.gg/lmb)");
            itemDiscord.ForeColor = Color.FromArgb(120, 180, 255);
            itemDiscord.Click += (s, e) => OpenDiscordLink();
            menu.Items.Add(itemDiscord);

            if (screenPos.HasValue)
            {
                menu.Show(screenPos.Value);
            }
            else if (anchor != null)
            {
                menu.Show(anchor, new Point(0, anchor.Height + 2));
            }
        }

        private void OpenDiscordLink()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://discord.com/invite/lmb",
                    UseShellExecute = true
                });
            }
            catch
            {
                try { Process.Start("https://discord.com/invite/lmb"); } catch { }
            }
        }

        private void AddColorPreset(ContextMenuStrip menu, string name, Color c)
        {
            ToolStripMenuItem pItem = new ToolStripMenuItem(name);
            pItem.ForeColor = Color.FromArgb(235, 245, 255);
            Bitmap swatch = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(swatch))
            {
                using (SolidBrush b = new SolidBrush(c))
                {
                    g.FillRectangle(b, 1, 1, 14, 14);
                }
                using (Pen pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
                {
                    g.DrawRectangle(pen, 1, 1, 13, 13);
                }
            }
            pItem.Image = swatch;
            pItem.Click += (s, e) => ApplyThemeTextColor(c);
            menu.Items.Add(pItem);
        }

        private void ApplyThemeTextColor(Color c)
        {
            currentTextColor = c;
            settings.TextColorHex = ColorTranslator.ToHtml(c);
            settings.Save();

            double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
            bool isDarkText = luminance < 120;
            Color boxBg = isDarkText ? Color.FromArgb(242, 245, 250) : Color.FromArgb(18, 22, 34);

            if (btnTextColor != null) btnTextColor.ForeColor = c;
            if (btnDiscord != null) btnDiscord.ForeColor = c;
            if (btnTopBg != null) btnTopBg.ForeColor = c;
            if (btnResetBg != null) btnResetBg.ForeColor = c;
            if (btnHelpRandom != null) { btnHelpRandom.ForeColor = c; btnHelpRandom.FlatAppearance.BorderColor = Color.FromArgb(80, c); }
            if (btnPickLocation != null) { btnPickLocation.ForeColor = c; btnPickLocation.FlatAppearance.BorderColor = Color.FromArgb(90, c); }
            if (btnHotkeySetting != null) { btnHotkeySetting.ForeColor = c; btnHotkeySetting.FlatAppearance.BorderColor = Color.FromArgb(70, c); }

            Color btnFore = isDarkText ? Color.FromArgb(40, 50, 65) : Color.FromArgb(200, 215, 235);
            if (btnClose != null) btnClose.ForeColor = btnFore;
            if (btnMinimize != null) btnMinimize.ForeColor = btnFore;

            TextBox[] textBoxes = new TextBox[] { txtHours, txtMins, txtSecs, txtMillis, txtRandomOffset, txtCoordX, txtCoordY };
            foreach (TextBox tb in textBoxes)
            {
                if (tb != null)
                {
                    tb.ForeColor = c;
                    tb.BackColor = boxBg;
                }
            }

            if (cboMouseButton != null) { cboMouseButton.ForeColor = c; cboMouseButton.BackColor = boxBg; }
            if (cboClickType != null) { cboClickType.ForeColor = c; cboClickType.BackColor = boxBg; }
            if (numRepeatCount != null) { numRepeatCount.ForeColor = c; numRepeatCount.BackColor = boxBg; }

            if (chkRandomOffset != null) chkRandomOffset.ForeColor = c;
            if (radRepeatTimes != null) radRepeatTimes.ForeColor = c;
            if (radRepeatUntilStopped != null) radRepeatUntilStopped.ForeColor = c;
            if (radCurrentLocation != null) radCurrentLocation.ForeColor = c;
            if (radPickLocation != null) radPickLocation.ForeColor = c;

            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is Label && ctrl != lblStatus)
                {
                    ctrl.ForeColor = c;
                }
            }

            if (lblStatus != null && !isClicking)
            {
                lblStatus.ForeColor = isDarkText ? Color.FromArgb(50, 60, 80) : Color.FromArgb(Math.Min(255, c.R / 2 + 100), Math.Min(255, c.G / 2 + 100), Math.Min(255, c.B / 2 + 100));
            }

            this.Invalidate(true);
        }

        private void ApplySettingsToUI()
        {
            txtHours.Text = Math.Max(0, settings.Hours).ToString();
            txtMins.Text = Math.Max(0, settings.Mins).ToString();
            txtSecs.Text = Math.Max(0, settings.Secs).ToString();
            txtMillis.Text = Math.Max(1, settings.Milliseconds).ToString();

            chkRandomOffset.Checked = settings.RandomOffsetEnabled;
            txtRandomOffset.Text = Math.Max(0, settings.RandomOffset).ToString();

            int btnIdx = cboMouseButton.FindStringExact(settings.MouseButton);
            if (btnIdx >= 0) cboMouseButton.SelectedIndex = btnIdx;

            int typeIdx = cboClickType.FindStringExact(settings.ClickType);
            if (typeIdx >= 0) cboClickType.SelectedIndex = typeIdx;

            if (settings.RepeatTimesSelected)
            {
                radRepeatTimes.Checked = true;
                radRepeatUntilStopped.Checked = false;
            }
            else
            {
                radRepeatUntilStopped.Checked = true;
                radRepeatTimes.Checked = false;
            }
            numRepeatCount.Value = Math.Max(1, Math.Min(9999999, settings.RepeatCount));

            if (settings.CursorCurrentLocation)
            {
                radCurrentLocation.Checked = true;
                radPickLocation.Checked = false;
            }
            else
            {
                radPickLocation.Checked = true;
                radCurrentLocation.Checked = false;
            }
            txtCoordX.Text = Math.Max(0, settings.CursorX).ToString();
            txtCoordY.Text = Math.Max(0, settings.CursorY).ToString();

            UpdateButtonHotkeys();

            if (!string.IsNullOrEmpty(settings.TextColorHex))
            {
                try
                {
                    currentTextColor = ColorTranslator.FromHtml(settings.TextColorHex);
                }
                catch { }
            }
            ApplyThemeTextColor(currentTextColor);
        }

        private void SaveSettingsFromUI()
        {
            int val;
            if (int.TryParse(txtHours.Text, out val)) settings.Hours = Math.Max(0, val);
            if (int.TryParse(txtMins.Text, out val)) settings.Mins = Math.Max(0, val);
            if (int.TryParse(txtSecs.Text, out val)) settings.Secs = Math.Max(0, val);
            if (int.TryParse(txtMillis.Text, out val)) settings.Milliseconds = Math.Max(1, val);

            settings.RandomOffsetEnabled = chkRandomOffset.Checked;
            if (int.TryParse(txtRandomOffset.Text, out val)) settings.RandomOffset = Math.Max(0, val);

            if (cboMouseButton.SelectedItem != null)
                settings.MouseButton = cboMouseButton.SelectedItem.ToString();

            if (cboClickType.SelectedItem != null)
                settings.ClickType = cboClickType.SelectedItem.ToString();

            settings.RepeatTimesSelected = radRepeatTimes.Checked;
            settings.RepeatCount = (int)numRepeatCount.Value;
            settings.RepeatUntilStopped = radRepeatUntilStopped.Checked;
            settings.CursorCurrentLocation = radCurrentLocation.Checked;

            if (int.TryParse(txtCoordX.Text, out val)) settings.CursorX = val;
            if (int.TryParse(txtCoordY.Text, out val)) settings.CursorY = val;

            settings.Save();
        }

        private long GetConfiguredIntervalMs()
        {
            int h = 0, m = 0, s = 0, ms = 100;
            int.TryParse(txtHours.Text, out h);
            int.TryParse(txtMins.Text, out m);
            int.TryParse(txtSecs.Text, out s);
            int.TryParse(txtMillis.Text, out ms);
            long total = ((long)h * 3600 + (long)m * 60 + (long)s) * 1000 + ms;
            return Math.Max(1, total);
        }

        private void SetStatusIdle()
        {
            if (lblStatus == null) return;
            lblStatus.Text = "● Status: IDLE (Press " + settings.Hotkey + " to start)";
            double lum = (0.299 * currentTextColor.R + 0.587 * currentTextColor.G + 0.114 * currentTextColor.B);
            lblStatus.ForeColor = lum < 120 ? Color.FromArgb(50, 60, 80) : Color.FromArgb(Math.Min(255, currentTextColor.R / 2 + 100), Math.Min(255, currentTextColor.G / 2 + 100), Math.Min(255, currentTextColor.B / 2 + 100));
        }

        private void UpdateButtonHotkeys()
        {
            btnStart.Text = "▶  START (" + settings.Hotkey + ")";
            btnStop.Text = "⏹  STOP (" + settings.Hotkey + ")";
            btnHotkeySetting.Text = "⚙ Hotkey Setting (" + settings.Hotkey + ")";
            if (!isClicking)
            {
                SetStatusIdle();
            }
        }

        // ==========================================
        // HOTKEY MANAGEMENT (VIA ASYNC POLLING)
        // ==========================================
        private void SetupHotkeyTimer()
        {
            hotkeyTimer = new System.Windows.Forms.Timer();
            hotkeyTimer.Interval = 20; // 50 checks/sec
            hotkeyTimer.Tick += (s, e) =>
            {
                if ((NativeMethods.GetAsyncKeyState((int)settings.Hotkey) & 0x8000) != 0)
                {
                    long now = Environment.TickCount;
                    if (now - lastHotkeyTick > 300)
                    {
                        lastHotkeyTick = now;
                        ToggleClicking();
                    }
                }
            };
            hotkeyTimer.Start();
        }

        private void ToggleClicking()
        {
            if (isClicking)
            {
                StopClicking();
            }
            else
            {
                StartClicking();
            }
        }

        // ==========================================
        // PICK LOCATION FEATURE
        // ==========================================
        private void BtnPickLocation_Click(object sender, EventArgs e)
        {
            radPickLocation.Checked = true;
            this.WindowState = FormWindowState.Minimized;

            using (LocationPickerForm picker = new LocationPickerForm())
            {
                if (picker.ShowDialog() == DialogResult.OK)
                {
                    txtCoordX.Text = picker.SelectedPoint.X.ToString();
                    txtCoordY.Text = picker.SelectedPoint.Y.ToString();
                }
            }

            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        // ==========================================
        // HOTKEY SETTING DIALOG (MODERN)
        // ==========================================
        private void BtnHotkeySetting_Click(object sender, EventArgs e)
        {
            using (HotkeySettingDialog dlg = new HotkeySettingDialog(settings.Hotkey))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    settings.Hotkey = dlg.SelectedKey;
                    UpdateButtonHotkeys();
                    SaveSettingsFromUI();
                }
            }
        }

        // ==========================================
        // CLICK EXECUTION ENGINE
        // ==========================================
        private void StartClicking()
        {
            if (isClicking) return;

            SaveSettingsFromUI();

            long totalMs = GetConfiguredIntervalMs();

            isClicking = true;
            btnStart.Enabled = false;
            btnStart.BackColor = Color.FromArgb(160, 40, 50, 65);
            btnStop.Enabled = true;
            btnStop.BackColor = Color.FromArgb(230, 230, 30, 60);

            lblStatus.Text = string.Format("● Status: CLICKING ACTIVE ({0} ms interval)", totalMs);
            lblStatus.ForeColor = Color.FromArgb(0, 240, 130);

            try { SystemSounds.Asterisk.Play(); } catch { }

            string mouseBtn = settings.MouseButton;
            string clickType = settings.ClickType;
            bool repeatUntilStopped = radRepeatUntilStopped.Checked;
            int repeatCount = (int)numRepeatCount.Value;
            bool useCurrentLoc = radCurrentLocation.Checked;
            int targetX = settings.CursorX;
            int targetY = settings.CursorY;
            bool randomOffset = chkRandomOffset.Checked;
            int offsetAmount = (int)Math.Max(0, settings.RandomOffset);

            clickThread = new Thread(() =>
            {
                NativeMethods.TimeBeginPeriod(1);
                int clicksPerformed = 0;

                try
                {
                    while (isClicking)
                    {
                        // 1. Move cursor if "Pick location" is chosen
                        if (!useCurrentLoc)
                        {
                            NativeMethods.SetCursorPos(targetX, targetY);
                        }

                        // 2. Perform realistic click (with hold down time so every click registers)
                        PerformMouseClick(mouseBtn, clickType, totalMs);
                        clicksPerformed++;

                        // 3. Check repeat count
                        if (!repeatUntilStopped && clicksPerformed >= repeatCount)
                        {
                            break;
                        }

                        // 4. Calculate next delay
                        long currentDelay = totalMs;
                        if (randomOffset && offsetAmount > 0)
                        {
                            int delta = rand.Next(-offsetAmount, offsetAmount + 1);
                            currentDelay += delta;
                            if (currentDelay < 1) currentDelay = 1;
                        }

                        // High precision waiting
                        PreciseSleep(currentDelay);
                    }
                }
                finally
                {
                    NativeMethods.TimeEndPeriod(1);
                    this.BeginInvoke(new Action(() =>
                    {
                        isClicking = false;
                        btnStart.Enabled = true;
                        btnStart.BackColor = Color.FromArgb(220, 0, 200, 115);
                        btnStop.Enabled = false;
                        btnStop.BackColor = Color.FromArgb(160, 45, 52, 65);

                        SetStatusIdle();
                    }));
                }
            });

            clickThread.IsBackground = true;
            clickThread.Priority = ThreadPriority.Highest;
            clickThread.Start();
        }

        private void StopClicking()
        {
            isClicking = false;
            btnStart.Enabled = true;
            btnStart.BackColor = Color.FromArgb(220, 0, 200, 115);
            btnStop.Enabled = false;
            btnStop.BackColor = Color.FromArgb(160, 45, 52, 65);

            SetStatusIdle();

            try { SystemSounds.Exclamation.Play(); } catch { }
        }

        private void PerformMouseClick(string button, string type, long intervalMs)
        {
            uint downFlag = NativeMethods.MOUSEEVENTF_LEFTDOWN;
            uint upFlag = NativeMethods.MOUSEEVENTF_LEFTUP;

            if (button == "Right")
            {
                downFlag = NativeMethods.MOUSEEVENTF_RIGHTDOWN;
                upFlag = NativeMethods.MOUSEEVENTF_RIGHTUP;
            }
            else if (button == "Middle")
            {
                downFlag = NativeMethods.MOUSEEVENTF_MIDDLEDOWN;
                upFlag = NativeMethods.MOUSEEVENTF_MIDDLEUP;
            }

            int holdMs = (int)Math.Max(1, Math.Min(10, intervalMs / 4));
            int count = (type == "Double") ? 2 : 1;

            for (int i = 0; i < count; i++)
            {
                NativeMethods.mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
                if (holdMs > 0) Thread.Sleep(holdMs);
                NativeMethods.mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);

                if (count > 1 && i < count - 1)
                {
                    Thread.Sleep(25);
                }
            }
        }

        private void PreciseSleep(long milliseconds)
        {
            if (milliseconds <= 0) return;

            Stopwatch sw = Stopwatch.StartNew();
            long targetTicks = (long)(milliseconds * (Stopwatch.Frequency / 1000.0));

            if (milliseconds > 15)
            {
                Thread.Sleep((int)(milliseconds - 12));
            }

            while (sw.ElapsedTicks < targetTicks && isClicking)
            {
                Thread.SpinWait(10);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopClicking();
            SaveSettingsFromUI();

            if (customBackground != null)
            {
                if (isCustomGif)
                {
                    ImageAnimator.StopAnimate(customBackground, OnFrameChanged);
                }
                customBackground.Dispose();
                customBackground = null;
            }

            if (hotkeyTimer != null)
            {
                hotkeyTimer.Stop();
                hotkeyTimer.Dispose();
            }
        }
    }

    // ==========================================
    // SCREEN LOCATION PICKER OVERLAY
    // ==========================================
    public class LocationPickerForm : Form
    {
        public Point SelectedPoint { get; private set; }
        private Label lblCoords;
        private System.Windows.Forms.Timer trackTimer;

        public LocationPickerForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.Opacity = 0.40;
            this.BackColor = Color.Black;
            this.Cursor = Cursors.Cross;
            this.ShowInTaskbar = false;

            lblCoords = new Label();
            lblCoords.AutoSize = true;
            lblCoords.BackColor = Color.FromArgb(20, 24, 36);
            lblCoords.ForeColor = Color.FromArgb(0, 230, 255);
            lblCoords.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblCoords.Padding = new Padding(8);
            lblCoords.Text = "Click anywhere on screen to pick location (ESC to cancel)";
            this.Controls.Add(lblCoords);

            trackTimer = new System.Windows.Forms.Timer();
            trackTimer.Interval = 16;
            trackTimer.Tick += (s, e) =>
            {
                Point cur = Cursor.Position;
                lblCoords.Text = string.Format("[+] X: {0}, Y: {1}  (Click to select, ESC to cancel)", cur.X, cur.Y);
                lblCoords.Location = new Point(cur.X + 20, cur.Y + 20);
                if (lblCoords.Right > this.Width) lblCoords.Left = cur.X - lblCoords.Width - 10;
                if (lblCoords.Bottom > this.Height) lblCoords.Top = cur.Y - lblCoords.Height - 10;
            };
            trackTimer.Start();

            this.MouseDown += (s, e) =>
            {
                SelectedPoint = Cursor.Position;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            trackTimer.Stop();
            trackTimer.Dispose();
            base.OnFormClosed(e);
        }
    }

    // ==========================================
    // HOTKEY SETTING DIALOG (MODERN)
    // ==========================================
    public class HotkeySettingDialog : Form
    {
        public Keys SelectedKey { get; private set; }
        private Label lblCurrent;
        private Label lblPrompt;
        private Button btnOk;
        private Button btnCancel;
        private ComboBox cboCommonKeys;

        public HotkeySettingDialog(Keys initialKey)
        {
            this.SelectedKey = initialKey;
            this.Text = "Hotkey Setting";
            this.ClientSize = new Size(340, 220);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.KeyPreview = true;
            this.BackColor = Color.FromArgb(16, 20, 32);
            this.Font = new Font("Segoe UI", 9F);

            lblPrompt = new Label();
            lblPrompt.Text = "Press any key on your keyboard to set as hotkey, or choose from the list:";
            lblPrompt.ForeColor = Color.FromArgb(200, 215, 235);
            lblPrompt.Location = new Point(18, 18);
            lblPrompt.Size = new Size(304, 38);

            lblCurrent = new Label();
            lblCurrent.Text = "Selected Key: " + initialKey.ToString();
            lblCurrent.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblCurrent.ForeColor = Color.FromArgb(0, 230, 255);
            lblCurrent.Location = new Point(18, 62);
            lblCurrent.Size = new Size(304, 30);

            cboCommonKeys = new ComboBox();
            cboCommonKeys.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCommonKeys.Items.AddRange(new object[] {
                "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
            });
            cboCommonKeys.SelectedItem = initialKey.ToString();
            cboCommonKeys.BackColor = Color.FromArgb(22, 28, 44);
            cboCommonKeys.ForeColor = Color.White;
            cboCommonKeys.FlatStyle = FlatStyle.Flat;
            cboCommonKeys.Location = new Point(18, 102);
            cboCommonKeys.Size = new Size(304, 25);
            cboCommonKeys.SelectedIndexChanged += (s, e) =>
            {
                if (cboCommonKeys.SelectedItem != null)
                {
                    try
                    {
                        SelectedKey = (Keys)Enum.Parse(typeof(Keys), cboCommonKeys.SelectedItem.ToString());
                        lblCurrent.Text = "Selected Key: " + SelectedKey.ToString();
                    }
                    catch { }
                }
            };

            btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.DialogResult = DialogResult.OK;
            btnOk.ForeColor = Color.White;
            btnOk.BackColor = Color.FromArgb(0, 190, 110);
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Location = new Point(148, 160);
            btnOk.Size = new Size(82, 32);

            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.ForeColor = Color.FromArgb(200, 210, 230);
            btnCancel.BackColor = Color.FromArgb(40, 48, 68);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Location = new Point(240, 160);
            btnCancel.Size = new Size(82, 32);

            this.Controls.AddRange(new Control[] {
                lblPrompt, lblCurrent, cboCommonKeys, btnOk, btnCancel
            });

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Escape && e.KeyCode != Keys.Enter)
                {
                    SelectedKey = e.KeyCode;
                    lblCurrent.Text = "Selected Key: " + SelectedKey.ToString();
                    cboCommonKeys.SelectedItem = SelectedKey.ToString();
                    e.Handled = true;
                }
            };
        }
    }

    // ==========================================
    // PROGRAM ENTRY POINT
    // ==========================================
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
