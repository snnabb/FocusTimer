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

    internal sealed class TimerForm : Form
    {
        private static readonly Color Background = Color.FromArgb(48, 45, 39);
        private static readonly Color Accent = Color.FromArgb(251, 188, 75);
        private static readonly Color AccentDark = Color.FromArgb(143, 101, 3);
        private static readonly Color Ring = Color.FromArgb(83, 70, 43);
        private static readonly Color TimeText = Color.FromArgb(255, 241, 223);
        private readonly Button countdownButton;
        private readonly Button stopwatchButton;
        private readonly Button startPauseButton;
        private readonly Button resetButton;
        private readonly Button soundButton;
        private readonly CheckBox topMostCheckBox;
        private readonly CircularTimerDisplay display;
        private readonly Timer tickTimer;
        private TimerMode mode = TimerMode.Countdown;
        private bool isRunning;
        private bool soundEnabled = true;
        private long elapsedCentiseconds;
        private long countdownCentiseconds = 5 * 60 * 100;
        private DateTime runStartedAt;

        public TimerForm()
        {
            base.Text = "计时器";
            ClientSize = new Size(960, 650);
            MinimumSize = new Size(976, 689);
            BackColor = Background;
            Font = new Font("Microsoft YaHei UI", 10F);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            countdownButton = CreateModeButton("⌛  定时器", new Point(18, 10));
            countdownButton.Click += delegate { SelectMode(TimerMode.Countdown); };
            stopwatchButton = CreateModeButton("◷  秒表", new Point(145, 10));
            stopwatchButton.Click += delegate { SelectMode(TimerMode.Stopwatch); };

            soundButton = CreateIconButton("🔊", new Point(835, 13), new Size(44, 42));
            soundButton.Click += delegate
            {
                soundEnabled = !soundEnabled;
                soundButton.Text = soundEnabled ? "🔊" : "🔇";
            };
            topMostCheckBox = new CheckBox
            {
                Text = "窗口置顶",
                Location = new Point(881, 24),
                Size = new Size(70, 24),
                ForeColor = Accent,
                BackColor = Background,
                Font = new Font("Microsoft YaHei UI", 8.5F),
                TextAlign = ContentAlignment.MiddleRight
            };
            topMostCheckBox.CheckedChanged += delegate { TopMost = topMostCheckBox.Checked; };

            display = new CircularTimerDisplay
            {
                Location = new Point(246, 96),
                Size = new Size(468, 468),
                BackColor = Background,
                ForeColor = TimeText
            };

            startPauseButton = CreateBottomButton("▶", new Point(3, 566), new Size(466, 66));
            display.DoubleClick += delegate { ConfigureCountdown(); };
            startPauseButton.Click += StartPauseButton_Click;
            resetButton = CreateBottomButton("↻", new Point(477, 566), new Size(466, 66));
            resetButton.Click += delegate { ResetTimer(); };

            tickTimer = new Timer { Interval = 15 };
            tickTimer.Tick += TickTimer_Tick;
            Controls.AddRange(new Control[] { countdownButton, stopwatchButton, soundButton, topMostCheckBox, display, startPauseButton, resetButton });
            SelectMode(TimerMode.Countdown);
        }

        private Button CreateModeButton(string text, Point location)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = new Size(116, 49),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                ForeColor = Accent,
                BackColor = Background,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
        }

        private Button CreateIconButton(string text, Point location, Size size)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                ForeColor = Accent,
                BackColor = Background,
                Font = new Font("Segoe UI Symbol", 16F),
                Cursor = Cursors.Hand
            };
        }

        private Button CreateBottomButton(string text, Point location, Size size)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = AccentDark,
                ForeColor = TimeText,
                Font = new Font("Segoe UI Symbol", 21F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
        }

        private void SelectMode(TimerMode newMode)
        {
            tickTimer.Stop();
            isRunning = false;
            mode = newMode;
            elapsedCentiseconds = 0;
            bool stopwatch = mode == TimerMode.Stopwatch;
            countdownButton.BackColor = stopwatch ? Background : AccentDark;
            stopwatchButton.BackColor = stopwatch ? AccentDark : Background;
            countdownButton.Text = stopwatch ? "⌛  定时器" : "✓  定时器";
            stopwatchButton.Text = stopwatch ? "✓  秒表" : "◷  秒表";
            startPauseButton.Text = "▶";
            RefreshDisplay();
        }

        private void StartPauseButton_Click(object? sender, EventArgs e)
        {
            if (isRunning)
            {
                PauseTimer();
                return;
            }

            if (mode == TimerMode.Countdown && countdownCentiseconds == 0)
            {
                MessageBox.Show("倒计时时长不能为 0。", "计时器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isRunning = true;
            runStartedAt = DateTime.UtcNow;
            tickTimer.Start();
            startPauseButton.Text = "Ⅱ";
        }

        private void PauseTimer()
        {
            UpdateElapsedTime();
            isRunning = false;
            tickTimer.Stop();
            startPauseButton.Text = "▶";
        }

        private void TickTimer_Tick(object? sender, EventArgs e)
        {
            UpdateElapsedTime();
            if (mode != TimerMode.Countdown || RemainingCentiseconds > 0) return;
            tickTimer.Stop();
            isRunning = false;
            startPauseButton.Text = "▶";
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
            isRunning = false;
            elapsedCentiseconds = 0;
            startPauseButton.Text = "▶";
            RefreshDisplay();
        }

        private void ConfigureCountdown()
        {
            if (mode != TimerMode.Countdown || isRunning) return;

            using (var dialog = new Form
            {
                Text = "设置倒计时",
                ClientSize = new Size(260, 132),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Background,
                ForeColor = TimeText
            })
            using (var minutesInput = new NumericUpDown { Minimum = 0, Maximum = 999, Value = countdownCentiseconds / 6000, Location = new Point(30, 46), Size = new Size(80, 28), Font = new Font("Segoe UI", 11F) })
            using (var secondsInput = new NumericUpDown { Minimum = 0, Maximum = 59, Value = countdownCentiseconds / 100 % 60, Location = new Point(142, 46), Size = new Size(80, 28), Font = new Font("Segoe UI", 11F) })
            {
                dialog.Controls.AddRange(new Control[]
                {
                    new Label { Text = "分钟", ForeColor = TimeText, BackColor = dialog.BackColor, Location = new Point(48, 20), AutoSize = true },
                    new Label { Text = "秒", ForeColor = TimeText, BackColor = dialog.BackColor, Location = new Point(170, 20), AutoSize = true },
                    minutesInput,
                    secondsInput
                });
                var confirmButton = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(93, 91), Size = new Size(74, 28) };
                dialog.Controls.Add(confirmButton);
                dialog.AcceptButton = confirmButton;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                countdownCentiseconds = ((long)minutesInput.Value * 60 + (long)secondsInput.Value) * 100;
                elapsedCentiseconds = 0;
                RefreshDisplay();
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
        private long centiseconds;
        private long totalCentiseconds;
        private bool countdown;

        public CircularTimerDisplay()
        {
            DoubleBuffered = true;
        }

        public void SetTime(long newCentiseconds, bool isCountdown, long total)
        {
            centiseconds = newCentiseconds;
            countdown = isCountdown;
            totalCentiseconds = Math.Max(1, total);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int inset = 14;
            var bounds = new Rectangle(inset, inset, Width - inset * 2, Height - inset * 2);
            using (var ringPen = new Pen(Color.FromArgb(83, 70, 43), 8F))
            {
                e.Graphics.DrawEllipse(ringPen, bounds);
            }

            float progress = countdown ? 1F - Math.Min(1F, (float)centiseconds / totalCentiseconds) : (float)(centiseconds % totalCentiseconds) / totalCentiseconds;
            float angle = -90F + progress * 360F;
            double radians = angle * Math.PI / 180D;
            float radius = bounds.Width / 2F;
            float centerX = bounds.Left + radius;
            float centerY = bounds.Top + radius;
            float markerX = centerX + (float)Math.Cos(radians) * radius;
            float markerY = centerY + (float)Math.Sin(radians) * radius;
            using (var markerBrush = new SolidBrush(Color.FromArgb(251, 188, 75)))
            {
                e.Graphics.FillEllipse(markerBrush, markerX - 14, markerY - 14, 28, 28);
            }

            long totalSeconds = centiseconds / 100;
            int hours = (int)(totalSeconds / 3600);
            int minutes = (int)(totalSeconds % 3600 / 60);
            int seconds = (int)(totalSeconds % 60);
            int fractions = (int)(centiseconds % 100);
            string time = hours > 0
                ? string.Format("{0:00}:{1:00}:{2:00}.{3:00}", hours, minutes, seconds, fractions)
                : string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, fractions);
            using (var font = new Font("Segoe UI", 46F, FontStyle.Regular, GraphicsUnit.Point))
            using (var textBrush = new SolidBrush(Color.FromArgb(255, 241, 223)))
            {
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(time, font, textBrush, new RectangleF(18, 18, Width - 36, Height - 36), format);
            }
        }
    }
}
