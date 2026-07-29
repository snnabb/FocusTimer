using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;

namespace FocusTimer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TimerForm());
        }
    }

    internal enum TimerMode { Countdown, Stopwatch }
    internal enum TimerState { Ready, Running, Paused }

    internal sealed class TimerForm : Form
    {
        private static readonly Color BlueBackground = Color.FromArgb(43, 47, 62);
        private static readonly Color BlueSurface = Color.FromArgb(61, 67, 88);
        private static readonly Color BlueAccent = Color.FromArgb(159, 193, 245);
        private static readonly Color BlueSelected = Color.FromArgb(61, 94, 143);
        private static readonly Color GoldBackground = Color.FromArgb(48, 45, 39);
        private static readonly Color GoldSurface = Color.FromArgb(143, 101, 3);
        private static readonly Color GoldAccent = Color.FromArgb(251, 188, 75);
        private readonly Button countdownButton;
        private readonly Button stopwatchButton;
        private readonly Button startPauseButton;
        private readonly Button resetButton;
        private readonly Button soundButton;
        private readonly CheckBox topMostCheckBox;
        private readonly CircularTimerDisplay display;
        private readonly Timer tickTimer;
        private TimerMode mode = TimerMode.Stopwatch;
        private TimerState state = TimerState.Ready;
        private bool soundEnabled = true;
        private long elapsedCentiseconds;
        private long countdownCentiseconds = 5 * 60 * 100;
        private DateTime runStartedAt;

        public TimerForm()
        {
            base.Text = "计时器";
            ClientSize = new Size(976, 651);
            MinimumSize = new Size(992, 690);
            Font = new Font("Microsoft YaHei UI", 10F);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            countdownButton = CreateModeButton(new Point(19, 10));
            countdownButton.Click += delegate { SelectMode(TimerMode.Countdown); };
            stopwatchButton = CreateModeButton(new Point(149, 10));
            stopwatchButton.Click += delegate { SelectMode(TimerMode.Stopwatch); };
            soundButton = CreateIconButton("🔊", new Point(845, 13), new Size(44, 42));
            soundButton.Click += delegate { soundEnabled = !soundEnabled; soundButton.Text = soundEnabled ? "🔊" : "🔇"; };
            topMostCheckBox = new CheckBox
            {
                Text = "窗口置顶",
                Location = new Point(892, 24),
                Size = new Size(72, 24),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleRight
            };
            topMostCheckBox.CheckedChanged += delegate { TopMost = topMostCheckBox.Checked; };

            display = new CircularTimerDisplay { Location = new Point(254, 89), Size = new Size(468, 468) };
            display.DoubleClick += delegate { ConfigureCountdown(); };
            startPauseButton = CreateBottomButton("▶", new Point(19, 566), new Size(466, 66));
            startPauseButton.Click += StartPauseButton_Click;
            resetButton = CreateBottomButton("↻", new Point(493, 566), new Size(466, 66));
            resetButton.Click += delegate { ResetTimer(); };

            tickTimer = new Timer { Interval = 15 };
            tickTimer.Tick += TickTimer_Tick;
            Controls.AddRange(new Control[] { countdownButton, stopwatchButton, soundButton, topMostCheckBox, display, startPauseButton, resetButton });
            ApplyVisualState();
        }

        private Button CreateModeButton(Point location) => new Button
        {
            Location = location, Size = new Size(122, 50), FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 }, Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold), Cursor = Cursors.Hand
        };

        private Button CreateIconButton(string text, Point location, Size size) => new Button
        {
            Text = text, Location = location, Size = size, FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 }, Font = new Font("Segoe UI Symbol", 16F), Cursor = Cursors.Hand
        };

        private Button CreateBottomButton(string text, Point location, Size size) => new Button
        {
            Text = text, Location = location, Size = size, FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 }, Font = new Font("Segoe UI Symbol", 21F, FontStyle.Bold), Cursor = Cursors.Hand
        };

        private void ApplyVisualState()
        {
            bool gold = state == TimerState.Paused;
            Color background = gold ? GoldBackground : BlueBackground;
            Color surface = gold ? GoldSurface : BlueSurface;
            Color accent = gold ? GoldAccent : BlueAccent;
            Color selected = gold ? GoldSurface : BlueSelected;
            bool stopwatch = mode == TimerMode.Stopwatch;
            BackColor = background;
            countdownButton.BackColor = stopwatch ? background : selected;
            stopwatchButton.BackColor = stopwatch ? selected : background;
            countdownButton.ForeColor = accent;
            stopwatchButton.ForeColor = accent;
            countdownButton.Text = stopwatch ? "⌛  定时器" : "✓  定时器";
            stopwatchButton.Text = stopwatch ? "✓  秒表" : "◷  秒表";
            soundButton.BackColor = background;
            soundButton.ForeColor = accent;
            topMostCheckBox.BackColor = background;
            topMostCheckBox.ForeColor = accent;
            display.BackColor = background;
            startPauseButton.BackColor = state == TimerState.Ready ? BlueAccent : surface;
            startPauseButton.ForeColor = state == TimerState.Ready ? Color.FromArgb(25, 31, 45) : Color.FromArgb(239, 242, 255);
            resetButton.BackColor = surface;
            resetButton.ForeColor = Color.FromArgb(239, 242, 255);
            startPauseButton.Text = state == TimerState.Running ? "Ⅱ" : "▶";
            display.SetVisuals(background, surface, accent, Color.FromArgb(239, 242, 255), state != TimerState.Ready);
            startPauseButton.Location = state == TimerState.Ready ? new Point(29, 539) : new Point(19, 566);
            startPauseButton.Size = state == TimerState.Ready ? new Size(918, 66) : new Size(466, 66);
            resetButton.Visible = state != TimerState.Ready;
            RefreshDisplay();
        }

        private void SelectMode(TimerMode newMode)
        {
            tickTimer.Stop();
            mode = newMode;
            elapsedCentiseconds = 0;
            state = TimerState.Ready;
            ApplyVisualState();
        }

        private void StartPauseButton_Click(object? sender, EventArgs e)
        {
            if (state == TimerState.Running) { PauseTimer(); return; }
            if (mode == TimerMode.Countdown && countdownCentiseconds == 0)
            {
                MessageBox.Show("倒计时时长不能为 0。", "计时器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            state = TimerState.Running;
            runStartedAt = DateTime.UtcNow;
            tickTimer.Start();
            ApplyVisualState();
        }

        private void PauseTimer()
        {
            UpdateElapsedTime();
            tickTimer.Stop();
            state = TimerState.Paused;
            ApplyVisualState();
        }

        private void TickTimer_Tick(object? sender, EventArgs e)
        {
            UpdateElapsedTime();
            if (mode != TimerMode.Countdown || RemainingCentiseconds > 0) return;
            tickTimer.Stop();
            state = TimerState.Ready;
            ApplyVisualState();
            if (soundEnabled) SystemSounds.Exclamation.Play();
            MessageBox.Show("时间到。", "计时器", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateElapsedTime()
        {
            long passed = Math.Max(0, (long)Math.Floor((DateTime.UtcNow - runStartedAt).TotalMilliseconds / 10));
            if (passed == 0) return;
            elapsedCentiseconds += passed;
            runStartedAt = runStartedAt.AddMilliseconds(passed * 10);
            RefreshDisplay();
        }

        private void ResetTimer()
        {
            tickTimer.Stop();
            elapsedCentiseconds = 0;
            state = TimerState.Ready;
            ApplyVisualState();
        }

        private void ConfigureCountdown()
        {
            if (mode != TimerMode.Countdown || state == TimerState.Running) return;
            using (var dialog = new DurationDialog(countdownCentiseconds / 100))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                countdownCentiseconds = dialog.TotalSeconds * 100;
                elapsedCentiseconds = 0;
                state = TimerState.Ready;
                ApplyVisualState();
            }
        }

        private long RemainingCentiseconds => Math.Max(0, countdownCentiseconds - elapsedCentiseconds);
        private void RefreshDisplay()
        {
            long value = mode == TimerMode.Countdown ? RemainingCentiseconds : elapsedCentiseconds;
            display.SetTime(value, mode == TimerMode.Countdown, mode == TimerMode.Countdown ? countdownCentiseconds : 6000);
        }
    }

    internal sealed class CircularTimerDisplay : Control
    {
        private long centiseconds, totalCentiseconds;
        private bool countdown, showRing;
        private Color background, ring, accent, text;
        public CircularTimerDisplay() { DoubleBuffered = true; }
        public void SetVisuals(Color newBackground, Color newRing, Color newAccent, Color newText, bool shouldShowRing)
        {
            background = newBackground; ring = newRing; accent = newAccent; text = newText; showRing = shouldShowRing; Invalidate();
        }
        public void SetTime(long value, bool isCountdown, long total)
        {
            centiseconds = value; countdown = isCountdown; totalCentiseconds = Math.Max(1, total); Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (showRing)
            {
                int inset = 14;
                var bounds = new Rectangle(inset, inset, Width - inset * 2, Height - inset * 2);
                using (var ringPen = new Pen(ring, 8F)) e.Graphics.DrawEllipse(ringPen, bounds);
                float progress = countdown ? 1F - Math.Min(1F, (float)centiseconds / totalCentiseconds) : (float)(centiseconds % totalCentiseconds) / totalCentiseconds;
                double radians = (-90F + progress * 360F) * Math.PI / 180D;
                float radius = bounds.Width / 2F, centerX = bounds.Left + radius, centerY = bounds.Top + radius;
                float markerX = centerX + (float)Math.Cos(radians) * radius, markerY = centerY + (float)Math.Sin(radians) * radius;
                using (var brush = new SolidBrush(accent)) e.Graphics.FillEllipse(brush, markerX - 14, markerY - 14, 28, 28);
            }
            long seconds = centiseconds / 100;
            int hours = (int)(seconds / 3600), minutes = (int)(seconds % 3600 / 60), secs = (int)(seconds % 60), fraction = (int)(centiseconds % 100);
            string time = hours > 0 ? string.Format("{0:00}:{1:00}:{2:00}.{3:00}", hours, minutes, secs, fraction) : string.Format("{0}.{1:00}", minutes * 60 + secs, fraction);
            using (var font = new Font("Segoe UI", 52F))
            using (var brush = new SolidBrush(text))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                e.Graphics.DrawString(time, font, brush, new RectangleF(18, 18, Width - 36, Height - 36), format);
        }
    }

    internal sealed class DurationDialog : Form
    {
        private readonly NumericUpDown minutes, seconds;
        public long TotalSeconds => (long)minutes.Value * 60 + (long)seconds.Value;
        public DurationDialog(long totalSeconds)
        {
            Text = "设置倒计时"; ClientSize = new Size(260, 132); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(43, 47, 62); ForeColor = Color.FromArgb(239, 242, 255);
            minutes = new NumericUpDown { Minimum = 0, Maximum = 999, Value = totalSeconds / 60, Location = new Point(30, 46), Size = new Size(80, 28), Font = new Font("Segoe UI", 11F) };
            seconds = new NumericUpDown { Minimum = 0, Maximum = 59, Value = totalSeconds % 60, Location = new Point(142, 46), Size = new Size(80, 28), Font = new Font("Segoe UI", 11F) };
            Controls.AddRange(new Control[] { new Label { Text = "分钟", ForeColor = ForeColor, BackColor = BackColor, Location = new Point(48, 20), AutoSize = true }, new Label { Text = "秒", ForeColor = ForeColor, BackColor = BackColor, Location = new Point(170, 20), AutoSize = true }, minutes, seconds });
            var confirm = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(93, 91), Size = new Size(74, 28) }; Controls.Add(confirm); AcceptButton = confirm;
        }
    }
}
