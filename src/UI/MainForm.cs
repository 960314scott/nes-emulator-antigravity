using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using NesEmulator.Audio;
using NesEmulator.Core;

namespace NesEmulator.UI
{
    public class MainForm : Form
    {
        private Nes _nes;
        private WaveOutPlayer _audioPlayer;

        private Bitmap _screenBitmap;
        private int _scale = 2;
        private bool _smoothFilter = false;
        private bool _isPaused = false;
        private bool _isTurbo = false;
        private bool _isRunning = true;
        private Thread _emuThread;

        // UI Components
        private MenuStrip _menuStrip;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripStatusLabel _fpsLabel;
        private ToolStripStatusLabel _mapperLabel;
        private Panel _screenPanel;

        // FPS calculation
        private int _fpsCounter = 0;
        private long _lastFpsTime = 0;

        public MainForm()
        {
            InitializeComponent();
            InitializeEmulator();
        }

        private void InitializeComponent()
        {
            Text = "NES Emulator (AccuracyCoin Ready)";
            ClientSize = new Size(Ppu.ScreenWidth * _scale, Ppu.ScreenHeight * _scale + 46);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AllowDrop = true;
            KeyPreview = true;

            DragEnter += OnFormDragEnter;
            DragDrop += OnFormDragDrop;
            KeyDown += OnFormKeyDown;
            KeyUp += OnFormKeyUp;
            FormClosing += OnFormClosing;

            // Menu Strip
            _menuStrip = new MenuStrip();

            // File Menu
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("&File");
            ToolStripMenuItem openItem = new ToolStripMenuItem("&Open ROM...", null, (s, e) => OpenRomDialog()) { ShortcutKeys = Keys.Control | Keys.O };
            ToolStripMenuItem resetItem = new ToolStripMenuItem("&Reset", null, (s, e) => ResetEmulator()) { ShortcutKeys = Keys.Control | Keys.R };
            ToolStripMenuItem saveItem = new ToolStripMenuItem("&Quick Save State", null, (s, e) => QuickSave()) { ShortcutKeys = Keys.F5 };
            ToolStripMenuItem loadItem = new ToolStripMenuItem("&Quick Load State", null, (s, e) => QuickLoad()) { ShortcutKeys = Keys.F7 };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("E&xit", null, (s, e) => Close());
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { openItem, resetItem, new ToolStripSeparator(), saveItem, loadItem, new ToolStripSeparator(), exitItem });

            // Emulation Menu
            ToolStripMenuItem emuMenu = new ToolStripMenuItem("&Emulation");
            ToolStripMenuItem pauseItem = new ToolStripMenuItem("&Pause / Resume (P)", null, (s, e) => TogglePause());
            ToolStripMenuItem turboItem = new ToolStripMenuItem("&Turbo Mode (Tab)", null, (s, e) => ToggleTurbo());
            emuMenu.DropDownItems.AddRange(new ToolStripItem[] { pauseItem, turboItem });

            // Video Menu
            ToolStripMenuItem videoMenu = new ToolStripMenuItem("&Video");
            ToolStripMenuItem scale1x = new ToolStripMenuItem("1x (256x240)", null, (s, e) => SetScale(1));
            ToolStripMenuItem scale2x = new ToolStripMenuItem("2x (512x480)", null, (s, e) => SetScale(2));
            ToolStripMenuItem scale3x = new ToolStripMenuItem("3x (768x720)", null, (s, e) => SetScale(3));
            ToolStripMenuItem scale4x = new ToolStripMenuItem("4x (1024x960)", null, (s, e) => SetScale(4));
            ToolStripMenuItem smoothItem = new ToolStripMenuItem("Smooth &Bilinear Filtering", null, (s, e) => ToggleSmoothFilter());
            videoMenu.DropDownItems.AddRange(new ToolStripItem[] { scale1x, scale2x, scale3x, scale4x, new ToolStripSeparator(), smoothItem });

            // Audio Menu
            ToolStripMenuItem audioMenu = new ToolStripMenuItem("&Audio");
            ToolStripMenuItem muteItem = new ToolStripMenuItem("&Mute Sound", null, (s, e) => ToggleMute()) { ShortcutKeys = Keys.Control | Keys.M };
            audioMenu.DropDownItems.Add(muteItem);

            // Help Menu
            ToolStripMenuItem helpMenu = new ToolStripMenuItem("&Help");
            ToolStripMenuItem controlsItem = new ToolStripMenuItem("&Controls Guide", null, (s, e) => ShowControls());
            ToolStripMenuItem aboutItem = new ToolStripMenuItem("&About", null, (s, e) => ShowAbout());
            helpMenu.DropDownItems.AddRange(new ToolStripItem[] { controlsItem, aboutItem });

            _menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, emuMenu, videoMenu, audioMenu, helpMenu });

            // Status Strip
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("No ROM loaded (Drag & Drop .nes file)") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _mapperLabel = new ToolStripStatusLabel("Mapper: - ");
            _fpsLabel = new ToolStripStatusLabel("FPS: 0");
            _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _mapperLabel, _fpsLabel });

            // Render Panel
            _screenPanel = new ScreenPanel(this)
            {
                Location = new Point(0, _menuStrip.Height),
                Size = new Size(Ppu.ScreenWidth * _scale, Ppu.ScreenHeight * _scale),
                BackColor = Color.Black
            };

            Controls.Add(_screenPanel);
            Controls.Add(_menuStrip);
            Controls.Add(_statusStrip);
            MainMenuStrip = _menuStrip;
        }

        private void InitializeEmulator()
        {
            _nes = new Nes();
            _audioPlayer = new WaveOutPlayer();
            _nes.Apu.Player = _audioPlayer;

            _screenBitmap = new Bitmap(Ppu.ScreenWidth, Ppu.ScreenHeight, PixelFormat.Format32bppRgb);

            _lastFpsTime = Stopwatch.GetTimestamp();

            _emuThread = new Thread(EmulationLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            _emuThread.Start();
        }

        private void SetScale(int scale)
        {
            _scale = scale;
            _screenPanel.Size = new Size(Ppu.ScreenWidth * _scale, Ppu.ScreenHeight * _scale);
            ClientSize = new Size(Ppu.ScreenWidth * _scale, Ppu.ScreenHeight * _scale + _menuStrip.Height + _statusStrip.Height);
            _screenPanel.Invalidate();
        }

        private void ToggleSmoothFilter()
        {
            _smoothFilter = !_smoothFilter;
            _screenPanel.Invalidate();
        }

        private void TogglePause()
        {
            _isPaused = !_isPaused;
            UpdateStatus();
        }

        private void ToggleTurbo()
        {
            _isTurbo = !_isTurbo;
        }

        private void ToggleMute()
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.Muted = !_audioPlayer.Muted;
            }
        }

        private void ResetEmulator()
        {
            if (_nes.IsRomLoaded)
            {
                _nes.Reset();
                UpdateStatus();
            }
        }

        private void QuickSave()
        {
            if (_nes.IsRomLoaded)
            {
                string path = Path.ChangeExtension(_nes.Cartridge.RomPath, ".sav");
                if (_nes.SaveState(path))
                {
                    _statusLabel.Text = "State saved to " + Path.GetFileName(path);
                }
            }
        }

        private void QuickLoad()
        {
            if (_nes.IsRomLoaded)
            {
                string path = Path.ChangeExtension(_nes.Cartridge.RomPath, ".sav");
                if (_nes.LoadState(path))
                {
                    _statusLabel.Text = "State loaded from " + Path.GetFileName(path);
                }
            }
        }

        private void OpenRomDialog()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "NES ROM Files (*.nes)|*.nes|All Files (*.*)|*.*";
                ofd.Title = "Select NES ROM";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadRom(ofd.FileName);
                }
            }
        }

        public void LoadRom(string filePath)
        {
            if (_nes.LoadRom(filePath))
            {
                string name = Path.GetFileName(filePath);
                _statusLabel.Text = string.Format("Loaded: {0}", name);
                _mapperLabel.Text = string.Format("Mapper: {0}", _nes.Cartridge.MapperId);
                Text = string.Format("NES Emulator - {0}", name);
            }
            else
            {
                MessageBox.Show("Failed to load ROM: " + filePath, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateStatus()
        {
            if (!_nes.IsRomLoaded)
            {
                _statusLabel.Text = "No ROM loaded";
            }
            else if (_isPaused)
            {
                _statusLabel.Text = "Paused";
            }
            else
            {
                _statusLabel.Text = string.Format("Running: {0}", Path.GetFileName(_nes.Cartridge.RomPath));
            }
        }

        private void EmulationLoop()
        {
            long frequency = Stopwatch.Frequency;
            double targetFrameTime = (double)frequency / 60.0;
            long lastTime = Stopwatch.GetTimestamp();

            while (_isRunning)
            {
                if (_nes.IsRomLoaded && !_isPaused)
                {
                    _nes.StepFrame();

                    // Copy frame buffer to bitmap
                    lock (_screenBitmap)
                    {
                        BitmapData data = _screenBitmap.LockBits(
                            new Rectangle(0, 0, Ppu.ScreenWidth, Ppu.ScreenHeight),
                            ImageLockMode.WriteOnly,
                            PixelFormat.Format32bppRgb);

                        Marshal.Copy(_nes.Ppu.FrameBuffer, 0, data.Scan0, Ppu.ScreenWidth * Ppu.ScreenHeight);
                        _screenBitmap.UnlockBits(data);
                    }

                    _screenPanel.BeginInvoke((MethodInvoker)delegate { _screenPanel.Invalidate(); });

                    _fpsCounter++;
                    long now = Stopwatch.GetTimestamp();
                    if (now - _lastFpsTime >= frequency)
                    {
                        int fps = _fpsCounter;
                        _fpsCounter = 0;
                        _lastFpsTime = now;
                        BeginInvoke((MethodInvoker)delegate { _fpsLabel.Text = string.Format("FPS: {0}", fps); });
                    }

                    if (!_isTurbo)
                    {
                        long elapsed = Stopwatch.GetTimestamp() - lastTime;
                        while (elapsed < targetFrameTime)
                        {
                            long remainingTicks = (long)targetFrameTime - elapsed;
                            if (remainingTicks > (frequency / 500)) // More than 2ms
                            {
                                Thread.Sleep(1);
                            }
                            else
                            {
                                Thread.SpinWait(10);
                            }
                            elapsed = Stopwatch.GetTimestamp() - lastTime;
                        }
                    }
                    lastTime = Stopwatch.GetTimestamp();
                }
                else
                {
                    Thread.Sleep(15);
                }
            }
        }

        public void DrawScreen(Graphics g)
        {
            lock (_screenBitmap)
            {
                g.InterpolationMode = _smoothFilter ? InterpolationMode.Bilinear : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(_screenBitmap, new Rectangle(0, 0, _screenPanel.Width, _screenPanel.Height));
            }
        }

        private void OnFormDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void OnFormDragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && Path.GetExtension(files[0]).Equals(".nes", StringComparison.OrdinalIgnoreCase))
            {
                LoadRom(files[0]);
            }
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.P)
            {
                TogglePause();
                return;
            }
            if (e.KeyCode == Keys.Tab)
            {
                _isTurbo = true;
                return;
            }
            HandleKey(e.KeyCode, true);
        }

        private void OnFormKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                _isTurbo = false;
                return;
            }
            HandleKey(e.KeyCode, false);
        }

        private void HandleKey(Keys key, bool pressed)
        {
            if (_nes == null || _nes.Bus == null) return;

            switch (key)
            {
                case Keys.Z: _nes.Bus.Controller1.SetButton(NesButton.A, pressed); break;
                case Keys.X: _nes.Bus.Controller1.SetButton(NesButton.B, pressed); break;
                case Keys.C: case Keys.RShiftKey: case Keys.ShiftKey: _nes.Bus.Controller1.SetButton(NesButton.Select, pressed); break;
                case Keys.Enter: _nes.Bus.Controller1.SetButton(NesButton.Start, pressed); break;
                case Keys.Up: _nes.Bus.Controller1.SetButton(NesButton.Up, pressed); break;
                case Keys.Down: _nes.Bus.Controller1.SetButton(NesButton.Down, pressed); break;
                case Keys.Left: _nes.Bus.Controller1.SetButton(NesButton.Left, pressed); break;
                case Keys.Right: _nes.Bus.Controller1.SetButton(NesButton.Right, pressed); break;
            }
        }

        private void ShowControls()
        {
            string msg = "NES Emulator Controls:\n\n" +
                         "D-Pad:  Arrow Keys (Up, Down, Left, Right)\n" +
                         "A Button:  Z\n" +
                         "B Button:  X\n" +
                         "Select:  Right Shift / C\n" +
                         "Start:  Enter\n\n" +
                         "Hotkeys:\n" +
                         "P:  Pause / Resume\n" +
                         "Tab:  Turbo Speed (Hold / Toggle)\n" +
                         "F5:  Quick Save State\n" +
                         "F7:  Quick Load State\n" +
                         "Ctrl + R:  Reset Emulator\n" +
                         "Ctrl + O:  Open ROM File";
            MessageBox.Show(msg, "Controls Guide", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowAbout()
        {
            string msg = "NES Emulator (AccuracyCoin Ready)\n\n" +
                         "Supports:\n" +
                         " - Ricoh 2A03 (6502) CPU with unofficial opcodes\n" +
                         " - Ricoh 2C02 PPU with fine scrolling & sprite 0 hit\n" +
                         " - APU pulse, triangle & noise audio output via Win32 WaveOut\n" +
                         " - Mappers: 0 (NROM), 1 (MMC1), 2 (UxROM), 3 (CNROM), 4 (MMC3), 7 (AxROM)\n" +
                         " - Drag and Drop .nes ROM support\n\n" +
                         "Designed for high-accuracy retro gaming!";
            MessageBox.Show(msg, "About NES Emulator", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            _isRunning = false;
            if (_audioPlayer != null)
            {
                _audioPlayer.Dispose();
            }
        }

        private class ScreenPanel : Panel
        {
            private readonly MainForm _form;

            public ScreenPanel(MainForm form)
            {
                _form = form;
                DoubleBuffered = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                _form.DrawScreen(e.Graphics);
            }
        }
    }
}
