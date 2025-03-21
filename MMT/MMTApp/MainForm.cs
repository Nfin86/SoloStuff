using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MMTApp
{
    public partial class MainForm : Form
    {
        private Button? recordButton;
        private Button? stopButton;
        private Button? playButton;
        private List<MouseEvent> recordedEvents = new List<MouseEvent>();
        private bool isRecording = false;
        private string? currentFilePath = null;
        private int playbackCount = 1;
        private bool stopPlayback = false;

        private const int HOTKEY_RECORD_ID = 1;
        private const int HOTKEY_PLAY_ID = 2;
        private const int HOTKEY_STOP_RECORD_ID = 3;
        private const int HOTKEY_STOP_PLAYBACK_ID = 4;
        private uint recordHotkey = 0x74; // F5
        private uint playHotkey = 0x75; // F6
        private uint stopRecordHotkey = 0x76; // F7
        private uint stopPlaybackHotkey = 0x51; // Q (with Ctrl)
        private uint recordModifier = 0;
        private uint playModifier = 0;
        private uint stopRecordModifier = 0;
        private uint stopPlaybackModifier = 0x0002; // Ctrl

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        const int MOUSEEVENTF_LEFTUP = 0x0004;
        const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const int MOUSEEVENTF_RIGHTUP = 0x0010;
        const int VK_LBUTTON = 0x01;
        const int VK_RBUTTON = 0x02;

        struct POINT
        {
            public int X;
            public int Y;
        }

        struct MouseEvent
        {
            public long Timestamp;
            public int X;
            public int Y;
            public bool LeftDown;
            public bool RightDown;
        }

        private class HotkeyDialog : Form
        {
            public uint NewHotkey { get; private set; }
            public uint NewModifier { get; private set; }

            public HotkeyDialog()
            {
                this.Text = "Set Hotkey";
                this.Size = new System.Drawing.Size(300, 150);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.StartPosition = FormStartPosition.CenterParent;

                Label prompt = new Label
                {
                    Text = "Press a key or key combination...",
                    Location = new System.Drawing.Point(20, 20),
                    Size = new System.Drawing.Size(260, 20)
                };
                this.Controls.Add(prompt);

                this.KeyPreview = true;
                this.KeyDown += HotkeyDialog_KeyDown;
            }

            private void HotkeyDialog_KeyDown(object sender, KeyEventArgs e)
{
    NewHotkey = (uint)e.KeyCode;
    NewModifier = 0;
    if (e.Control) NewModifier |= 0x0002; // Ctrl
    if (e.Alt) NewModifier |= 0x0001;     // Alt
    if (e.Shift) NewModifier |= 0x0004;   // Shift
    this.DialogResult = DialogResult.OK;
    this.Close();
}
        }

        public MainForm()
        {
            InitializeComponents();
            RegisterHotkeys();
        }

        private void InitializeComponents()
        {
            this.Text = "Mouse Macro Tool";
            this.Size = new System.Drawing.Size(600, 400);

            recordButton = new Button();
            recordButton.Text = "Record";
            recordButton.Location = new System.Drawing.Point(20, 20);
            recordButton.Click += new EventHandler(RecordButton_Click);
            this.Controls.Add(recordButton);

            stopButton = new Button();
            stopButton.Text = "Stop";
            stopButton.Location = new System.Drawing.Point(100, 20);
            stopButton.Enabled = false;
            stopButton.Click += new EventHandler(StopButton_Click);
            this.Controls.Add(stopButton);

            playButton = new Button();
            playButton.Text = "Play";
            playButton.Location = new System.Drawing.Point(180, 20);
            playButton.Enabled = false;
            playButton.Click += new EventHandler(PlayButton_Click);
            this.Controls.Add(playButton);

            MenuStrip menuStrip = new MenuStrip();
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("New", null, NewFile_Click);
            fileMenu.DropDownItems.Add("Open...", null, OpenFile_Click);
            fileMenu.DropDownItems.Add("Save", null, SaveFile_Click);
            fileMenu.DropDownItems.Add("Save As...", null, SaveAsFile_Click);
            menuStrip.Items.Add(fileMenu);

            ToolStripMenuItem editMenu = new ToolStripMenuItem("Edit");
            editMenu.DropDownItems.Add("Settings", null, Settings_Click);
            editMenu.DropDownItems.Add("Hotkeys", null, Hotkeys_Click);
            menuStrip.Items.Add(editMenu);

            this.Controls.Add(menuStrip);
            this.MainMenuStrip = menuStrip;
        }

        private void RegisterHotkeys()
        {
            RegisterHotKey(this.Handle, HOTKEY_RECORD_ID, recordModifier, recordHotkey);
            RegisterHotKey(this.Handle, HOTKEY_PLAY_ID, playModifier, playHotkey);
            RegisterHotKey(this.Handle, HOTKEY_STOP_RECORD_ID, stopRecordModifier, stopRecordHotkey);
            RegisterHotKey(this.Handle, HOTKEY_STOP_PLAYBACK_ID, stopPlaybackModifier, stopPlaybackHotkey);
        }

        private void UnregisterHotkeys()
        {
            UnregisterHotKey(this.Handle, HOTKEY_RECORD_ID);
            UnregisterHotKey(this.Handle, HOTKEY_PLAY_ID);
            UnregisterHotKey(this.Handle, HOTKEY_STOP_RECORD_ID);
            UnregisterHotKey(this.Handle, HOTKEY_STOP_PLAYBACK_ID);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY)
            {
                switch ((int)m.WParam)
                {
                    case HOTKEY_RECORD_ID:
                        if (!isRecording && !stopPlayback) RecordButton_Click(this, EventArgs.Empty);
                        break;
                    case HOTKEY_PLAY_ID:
                        if (!isRecording && !stopPlayback) PlayButton_Click(this, EventArgs.Empty);
                        break;
                    case HOTKEY_STOP_RECORD_ID:
                        if (isRecording) StopButton_Click(this, EventArgs.Empty);
                        break;
                    case HOTKEY_STOP_PLAYBACK_ID:
                        stopPlayback = true;
                        break;
                }
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnregisterHotkeys();
            }
            base.Dispose(disposing);
        }

        private void RecordButton_Click(object? sender, EventArgs e)
        {
            recordButton!.Enabled = false;
            stopButton!.Enabled = true;
            playButton!.Enabled = false;
            recordedEvents.Clear();
            isRecording = true;
            System.Threading.ThreadPool.QueueUserWorkItem(_ => RecordEvents());
        }

        private void StopButton_Click(object? sender, EventArgs e)
        {
            isRecording = false;
            recordButton!.Enabled = true;
            stopButton!.Enabled = false;
            playButton!.Enabled = true;
        }

        private void PlayButton_Click(object? sender, EventArgs e)
        {
            recordButton!.Enabled = false;
            stopButton!.Enabled = false;
            playButton!.Enabled = false;
            stopPlayback = false;
            System.Threading.Thread playbackThread = new System.Threading.Thread(() =>
            {
                for (int i = 0; i < playbackCount && !stopPlayback; i++)
                {
                    ReplayEvents();
                }
                this.Invoke((MethodInvoker)delegate
                {
                    recordButton!.Enabled = true;
                    stopButton!.Enabled = false;
                    playButton!.Enabled = true;
                });
            });
            playbackThread.Start();
        }

        private void NewFile_Click(object? sender, EventArgs e)
        {
            recordedEvents.Clear();
            currentFilePath = null;
            this.Text = "Mouse Macro Tool - Untitled";
        }

        private void OpenFile_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Macro Files (*.mcr)|*.mcr|All Files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    recordedEvents.Clear();
                    string[] lines = File.ReadAllLines(ofd.FileName);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length == 5 && long.TryParse(parts[0], out long timestamp) && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y) && bool.TryParse(parts[3], out bool leftDown) && bool.TryParse(parts[4], out bool rightDown))
                        {
                            recordedEvents.Add(new MouseEvent { Timestamp = timestamp, X = x, Y = y, LeftDown = leftDown, RightDown = rightDown });
                        }
                    }
                    currentFilePath = ofd.FileName;
                    this.Text = $"Mouse Macro Tool - {Path.GetFileName(currentFilePath)}";
                    playButton!.Enabled = true;
                }
            }
        }

        private void SaveFile_Click(object? sender, EventArgs e)
        {
            if (currentFilePath == null)
            {
                SaveAsFile_Click(sender, e);
            }
            else
            {
                SaveToFile(currentFilePath);
            }
        }

        private void SaveAsFile_Click(object? sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Macro Files (*.mcr)|*.mcr|All Files (*.*)|*.*";
                sfd.DefaultExt = "mcr";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    SaveToFile(sfd.FileName);
                    currentFilePath = sfd.FileName;
                    this.Text = $"Mouse Macro Tool - {Path.GetFileName(currentFilePath)}";
                }
            }
        }

        private void Settings_Click(object? sender, EventArgs e)
        {
            using (Form settingsForm = new Form())
            {
                settingsForm.Text = "Settings";
                settingsForm.Size = new System.Drawing.Size(600, 450);

                Label label = new Label();
                label.Text = "Total Playbacks:";
                label.Location = new System.Drawing.Point(20, 20);
                settingsForm.Controls.Add(label);

                TextBox playbackTextBox = new TextBox();
                playbackTextBox.Text = playbackCount.ToString();
                playbackTextBox.Location = new System.Drawing.Point(120, 18);
                playbackTextBox.Width = 60;
                playbackTextBox.TextChanged += (s, ev) =>
                {
                    if (int.TryParse(playbackTextBox.Text, out int newCount) && newCount >= 1)
                    {
                        playbackCount = newCount;
                    }
                };
                settingsForm.Controls.Add(playbackTextBox);

                settingsForm.ShowDialog();
            }
        }

        private void Hotkeys_Click(object? sender, EventArgs e)
{
    using (Form hotkeysForm = new Form())
    {
        hotkeysForm.Text = "Hotkeys";
        hotkeysForm.Size = new System.Drawing.Size(400, 300);

        Label recordLabel = new Label { Text = "Record:", Location = new System.Drawing.Point(20, 20) };
        hotkeysForm.Controls.Add(recordLabel);

        TextBox recordBox = new TextBox
        {
            Text = "F5",
            Location = new System.Drawing.Point(300, 18),
            Width = 80,
            ReadOnly = true
        };
        recordBox.Click += RecordBox_Click;
        hotkeysForm.Controls.Add(recordBox);

        Label playLabel = new Label { Text = "Play:", Location = new System.Drawing.Point(20, 50) };
        hotkeysForm.Controls.Add(playLabel);

        TextBox playBox = new TextBox
        {
            Text = "F6",
            Location = new System.Drawing.Point(300, 48),
            Width = 80,
            ReadOnly = true
        };
        playBox.Click += PlayBox_Click;
        hotkeysForm.Controls.Add(playBox);

        Label stopRecordLabel = new Label { Text = "Stop Recording:", Location = new System.Drawing.Point(20, 80) };
        hotkeysForm.Controls.Add(stopRecordLabel);

        TextBox stopRecordBox = new TextBox
        {
            Text = "F7",
            Location = new System.Drawing.Point(300, 78),
            Width = 80,
            ReadOnly = true
        };
        stopRecordBox.Click += StopRecordBox_Click;
        hotkeysForm.Controls.Add(stopRecordBox);

        Label stopPlaybackLabel = new Label { Text = "Stop Playback:", Location = new System.Drawing.Point(20, 110) };
        hotkeysForm.Controls.Add(stopPlaybackLabel);

        TextBox stopPlaybackBox = new TextBox
        {
            Text = "Ctrl+Q",
            Location = new System.Drawing.Point(300, 108),
            Width = 80,
            ReadOnly = true
        };
        stopPlaybackBox.Click += StopPlaybackBox_Click;
        hotkeysForm.Controls.Add(stopPlaybackBox);

        hotkeysForm.ShowDialog();
    }
}

        private void RecordBox_Click(object sender, EventArgs e)
{
    TextBox box = (TextBox)sender;
    var (newHotkey, newModifier) = SetupHotkeyBox(box, HOTKEY_RECORD_ID);
    if (newHotkey != 0)
    {
        recordHotkey = newHotkey;
        recordModifier = newModifier;
    }
}

private void PlayBox_Click(object sender, EventArgs e)
{
    TextBox box = (TextBox)sender;
    var (newHotkey, newModifier) = SetupHotkeyBox(box, HOTKEY_PLAY_ID);
    if (newHotkey != 0)
    {
        playHotkey = newHotkey;
        playModifier = newModifier;
    }
}

private void StopRecordBox_Click(object sender, EventArgs e)
{
    TextBox box = (TextBox)sender;
    var (newHotkey, newModifier) = SetupHotkeyBox(box, HOTKEY_STOP_RECORD_ID);
    if (newHotkey != 0)
    {
        stopRecordHotkey = newHotkey;
        stopRecordModifier = newModifier;
    }
}

private void StopPlaybackBox_Click(object sender, EventArgs e)
{
    TextBox box = (TextBox)sender;
    var (newHotkey, newModifier) = SetupHotkeyBox(box, HOTKEY_STOP_PLAYBACK_ID);
    if (newHotkey != 0)
    {
        stopPlaybackHotkey = newHotkey;
        stopPlaybackModifier = newModifier;
    }
}

        private (uint, uint) SetupHotkeyBox(TextBox box, int hotkeyId)
        {
            using (HotkeyDialog dialog = new HotkeyDialog())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    uint newHotkey = dialog.NewHotkey;
                    uint newModifier = dialog.NewModifier;

                    UnregisterHotKey(this.Handle, hotkeyId);
                    if (RegisterHotKey(this.Handle, hotkeyId, newModifier, newHotkey))
                    {
                        string displayText = "";
                        if ((newModifier & 0x0002) != 0) displayText += "Ctrl+";
                        if ((newModifier & 0x0001) != 0) displayText += "Alt+";
                        if ((newModifier & 0x0004) != 0) displayText += "Shift+";
                        displayText += Enum.GetName(typeof(Keys), (Keys)newHotkey);
                        box.Text = displayText ?? "Unknown";
                        return (newHotkey, newModifier);
                    }
                    else
                    {
                        box.Text = "Error setting hotkey";
                    }
                }
            }
            return (0, 0); // Indicate failure
        }

        private void SaveToFile(string filePath)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                foreach (MouseEvent ev in recordedEvents)
                {
                    sw.WriteLine($"{ev.Timestamp},{ev.X},{ev.Y},{ev.LeftDown},{ev.RightDown}");
                }
            }
        }

        private void RecordEvents()
        {
            recordedEvents.Clear();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            System.Threading.Thread.Sleep(1000);
            while (isRecording)
            {
                long timestamp = stopwatch.ElapsedMilliseconds;
                GetCursorPos(out POINT p);
                bool leftDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
                bool rightDown = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
                recordedEvents.Add(new MouseEvent { Timestamp = timestamp, X = p.X, Y = p.Y, LeftDown = leftDown, RightDown = rightDown });
                System.Threading.Thread.Sleep(10);
            }
        }

        private void ReplayEvents()
        {
            if (recordedEvents.Count == 0) return;
            var playbackStopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool lastLeftDown = recordedEvents[0].LeftDown;
            bool lastRightDown = recordedEvents[0].RightDown;
            SetCursorPos(recordedEvents[0].X, recordedEvents[0].Y);

            for (int i = 0; i < recordedEvents.Count && !stopPlayback; i++)
            {
                var ev = recordedEvents[i];
                while (playbackStopwatch.ElapsedMilliseconds < ev.Timestamp && !stopPlayback)
                {
                    System.Threading.Thread.Sleep(1);
                }
                if (stopPlayback) break;

                SetCursorPos(ev.X, ev.Y);
                if (ev.LeftDown != lastLeftDown)
                {
                    if (ev.LeftDown) mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                    else mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
                    lastLeftDown = ev.LeftDown;
                }
                if (ev.RightDown != lastRightDown)
                {
                    if (ev.RightDown) mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                    else mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
                    lastRightDown = ev.RightDown;
                }
            }
        }
    }
}