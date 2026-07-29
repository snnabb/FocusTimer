using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Media;
using System.Windows.Forms;

namespace FocusTimer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TimerForm());
        }
    }

    internal static class Colors
    {
        public static readonly Color Window = Color.FromArgb(246, 246, 248);
        public static readonly Color Card = Color.White;
        public static readonly Color Text = Color.FromArgb(29, 29, 31);
        public static readonly Color Secondary = Color.FromArgb(110, 110, 115);
        public static readonly Color Line = Color.FromArgb(226, 226, 230);
        public static readonly Color Soft = Color.FromArgb(235, 235, 239);
        public static readonly Color Accent = Color.FromArgb(64, 111, 221);
        public static readonly Color AccentSoft = Color.FromArgb(230, 237, 252);
        public static readonly Color Danger = Color.FromArgb(220, 74, 67);
    }

    internal enum TimerMode { Countdown, Stopwatch }
    internal enum TimerState { Ready, Running, Paused, Finished }

    internal sealed class TimerForm : Form
    {
        private readonly TimerCanvas canvas;

        public TimerForm()
        {
            Text = "Mori Timer";
            ClientSize = new Size(560, 480);
            MinimumSize = MaximumSize = new Size(576, 519);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Colors.Window;
            KeyPreview = true;
            Padding = new Padding(1);
            canvas = new TimerCanvas { Dock = DockStyle.Fill };
            Controls.Add(canvas);
            Resize += delegate { ApplyRoundedWindow(); };
            Shown += delegate { ApplyRoundedWindow(); canvas.Focus(); };
        }

        private void ApplyRoundedWindow()
        {
            using (var path = Shape.RoundRect(new Rectangle(0, 0, Width, Height), 24))
                Region = new Region(path);
        }
    }

    internal sealed class TimerCanvas : Control
    {
        private readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer { Interval = 30 };
        private TimerMode mode = TimerMode.Countdown;
        private TimerState state = TimerState.Ready;
        private long durationMilliseconds = 25 * 60 * 1000;
        private long accumulatedMilliseconds;
        private DateTime runStartedAt;
        private bool pinned;
        private Point dragOrigin;
        private Rectangle countdownTab;
        private Rectangle stopwatchTab;
        private Rectangle minimizeButton;
        private Rectangle closeButton;
        private Rectangle pinButton;
        private Rectangle primaryButton;
        private Rectangle secondaryButton;
        private Rectangle customButton;
        private readonly Rectangle[] presetButtons = new Rectangle[3];
        private static readonly int[] Presets = { 5, 25, 45 };

        public TimerCanvas()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            TabStop = true;
            BackColor = Colors.Window;
            Font = new Font("Microsoft YaHei UI", 10F);
            ticker.Tick += delegate { Invalidate(); CheckCompletion(); };
            MouseDown += HandleMouseDown;
            MouseMove += HandleMouseMove;
            MouseUp += HandleMouseUp;
            KeyDown += HandleKeyDown;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (var border = new Pen(Colors.Line, 1F))
            using (var path = Shape.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 23))
                g.DrawPath(border, path);
            DrawTitleBar(g);
            DrawModePicker(g);
            DrawMainCard(g);
            DrawBottomActions(g);
        }

        private void DrawTitleBar(Graphics g)
        {
            DrawLabel(g, "Mori", 14F, FontStyle.Bold, Colors.Text, new Rectangle(24, 13, 130, 32), StringAlignment.Near);
            DrawLabel(g, "少一点干扰，多一点专注", 8F, FontStyle.Regular, Colors.Secondary, new Rectangle(24, 40, 210, 22), StringAlignment.Near);
            pinButton = new Rectangle(430, 17, 30, 30);
            minimizeButton = new Rectangle(467, 17, 30, 30);
            closeButton = new Rectangle(504, 17, 30, 30);
            CircleIcon(g, pinButton, pinned ? Colors.AccentSoft : Colors.Window, "⌖", pinned ? Colors.Accent : Colors.Secondary, 11F);
            CircleIcon(g, minimizeButton, Colors.Window, "—", Colors.Secondary, 10F);
            CircleIcon(g, closeButton, Colors.Window, "×", Colors.Secondary, 12F);
        }

        private void DrawModePicker(Graphics g)
        {
            var container = new Rectangle(176, 76, 208, 40);
            Shape.Fill(g, container, 13, Colors.Soft);
            countdownTab = new Rectangle(180, 80, 100, 32);
            stopwatchTab = new Rectangle(280, 80, 100, 32);
            Shape.Fill(g, mode == TimerMode.Countdown ? countdownTab : stopwatchTab, 10, Colors.Card);
            DrawLabel(g, "倒计时", 9F, FontStyle.Bold, mode == TimerMode.Countdown ? Colors.Text : Colors.Secondary, countdownTab, StringAlignment.Center);
            DrawLabel(g, "正计时", 9F, FontStyle.Bold, mode == TimerMode.Stopwatch ? Colors.Text : Colors.Secondary, stopwatchTab, StringAlignment.Center);
        }

        private void DrawMainCard(Graphics g)
        {
            var shadow = new Rectangle(35, 135, 490, 255);
            Shape.Fill(g, new Rectangle(shadow.X, shadow.Y + 3, shadow.Width, shadow.Height), 24, Color.FromArgb(14, 0, 0, 0));
            Shape.Fill(g, shadow, 24, Colors.Card);

            string label = mode == TimerMode.Countdown ? "剩余时间" : "已专注";
            DrawLabel(g, label, 9F, FontStyle.Regular, Colors.Secondary, new Rectangle(60, 160, 440, 24), StringAlignment.Center);
            string time = mode == TimerMode.Countdown ? FormatCountdown(RemainingMilliseconds) : FormatStopwatch(CurrentElapsedMilliseconds);
            float size = time.Length > 8 ? 40F : 50F;
            DrawLabel(g, time, size, FontStyle.Regular, Colors.Text, new Rectangle(54, 190, 452, 84), StringAlignment.Center);
            DrawLabel(g, StatusText(), 8.5F, FontStyle.Regular, state == TimerState.Finished ? Colors.Danger : Colors.Secondary, new Rectangle(60, 270, 440, 24), StringAlignment.Center);

            if (mode == TimerMode.Countdown)
            {
                var track = new Rectangle(78, 313, 404, 6);
                Shape.Fill(g, track, 3, Colors.Soft);
                float progress = durationMilliseconds == 0 ? 0 : 1F - (float)RemainingMilliseconds / durationMilliseconds;
                if (progress > 0)
                {
                    var fill = new Rectangle(track.X, track.Y, Math.Max(6, (int)(track.Width * Math.Min(1F, progress))), track.Height);
                    Shape.Fill(g, fill, 3, Colors.Accent);
                }
                DrawPresets(g);
            }
            else
            {
                DrawLabel(g, "精确到百分之一秒", 8F, FontStyle.Regular, Colors.Secondary, new Rectangle(60, 327, 440, 22), StringAlignment.Center);
            }
        }

        private void DrawPresets(Graphics g)
        {
            int x = 126;
            for (int i = 0; i < Presets.Length; i++)
            {
                presetButtons[i] = new Rectangle(x + i * 80, 339, 70, 32);
                bool selected = state == TimerState.Ready && durationMilliseconds == Presets[i] * 60L * 1000L;
                Shape.Fill(g, presetButtons[i], 16, selected ? Colors.AccentSoft : Colors.Window);
                DrawLabel(g, Presets[i] + " 分", 8.5F, FontStyle.Bold, selected ? Colors.Accent : Colors.Secondary, presetButtons[i], StringAlignment.Center);
            }
            customButton = new Rectangle(366, 339, 68, 32);
            Shape.Fill(g, customButton, 16, Colors.Window);
            DrawLabel(g, "自定义", 8.5F, FontStyle.Bold, Colors.Secondary, customButton, StringAlignment.Center);
        }

        private void DrawBottomActions(Graphics g)
        {
            int y = 414;
            secondaryButton = new Rectangle(35, y, 112, 44);
            primaryButton = new Rectangle(state == TimerState.Ready ? 159 : 159, y, state == TimerState.Ready ? 366 : 366, 44);
            bool secondaryEnabled = state != TimerState.Ready;
            Shape.Fill(g, secondaryButton, 16, secondaryEnabled ? Colors.Soft : Color.FromArgb(241, 241, 243));
            DrawLabel(g, "重置", 9F, FontStyle.Bold, secondaryEnabled ? Colors.Secondary : Color.FromArgb(185, 185, 190), secondaryButton, StringAlignment.Center);
            bool running = state == TimerState.Running;
            Shape.Fill(g, primaryButton, 16, running ? Colors.Text : Colors.Accent);
            DrawLabel(g, running ? "暂停" : state == TimerState.Paused ? "继续" : "开始", 9.5F, FontStyle.Bold, Color.White, primaryButton, StringAlignment.Center);
        }

        private void HandleMouseDown(object? sender, MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Left && e.Y < 66 && !pinButton.Contains(e.Location) && !minimizeButton.Contains(e.Location) && !closeButton.Contains(e.Location))
                dragOrigin = e.Location;
        }

        private void HandleMouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || dragOrigin == Point.Empty) return;
            var form = FindForm();
            if (form != null) form.Location = new Point(form.Left + e.X - dragOrigin.X, form.Top + e.Y - dragOrigin.Y);
        }

        private void HandleMouseUp(object? sender, MouseEventArgs e)
        {
            dragOrigin = Point.Empty;
            if (closeButton.Contains(e.Location)) { FindForm()?.Close(); return; }
            if (minimizeButton.Contains(e.Location)) { if (FindForm() != null) FindForm()!.WindowState = FormWindowState.Minimized; return; }
            if (pinButton.Contains(e.Location)) { pinned = !pinned; if (FindForm() != null) FindForm()!.TopMost = pinned; Invalidate(); return; }
            if (countdownTab.Contains(e.Location)) { SwitchMode(TimerMode.Countdown); return; }
            if (stopwatchTab.Contains(e.Location)) { SwitchMode(TimerMode.Stopwatch); return; }
            if (primaryButton.Contains(e.Location)) { Toggle(); return; }
            if (secondaryButton.Contains(e.Location) && state != TimerState.Ready) { ResetTimer(); return; }
            if (mode == TimerMode.Countdown && state == TimerState.Ready)
            {
                for (int i = 0; i < Presets.Length; i++)
                    if (presetButtons[i].Contains(e.Location)) { durationMilliseconds = Presets[i] * 60L * 1000L; accumulatedMilliseconds = 0; Invalidate(); return; }
                if (customButton.Contains(e.Location)) ConfigureDuration();
            }
        }

        private void HandleKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { Toggle(); e.Handled = true; }
            else if (e.KeyCode == Keys.R) ResetTimer();
            else if (e.KeyCode == Keys.D1) SwitchMode(TimerMode.Countdown);
            else if (e.KeyCode == Keys.D2) SwitchMode(TimerMode.Stopwatch);
        }

        private void SwitchMode(TimerMode next)
        {
            ticker.Stop();
            mode = next;
            state = TimerState.Ready;
            accumulatedMilliseconds = 0;
            Invalidate();
        }

        private void Toggle()
        {
            if (state == TimerState.Running)
            {
                accumulatedMilliseconds = CurrentElapsedMilliseconds;
                ticker.Stop();
                state = TimerState.Paused;
            }
            else
            {
                if (mode == TimerMode.Countdown && RemainingMilliseconds <= 0) accumulatedMilliseconds = 0;
                runStartedAt = DateTime.UtcNow;
                state = TimerState.Running;
                ticker.Start();
            }
            Invalidate();
        }

        private void ResetTimer()
        {
            ticker.Stop();
            accumulatedMilliseconds = 0;
            state = TimerState.Ready;
            Invalidate();
        }

        private void CheckCompletion()
        {
            if (mode != TimerMode.Countdown || state != TimerState.Running || RemainingMilliseconds > 0) return;
            accumulatedMilliseconds = durationMilliseconds;
            ticker.Stop();
            state = TimerState.Finished;
            Invalidate();
            SystemSounds.Exclamation.Play();
            MessageBox.Show(FindForm(), "时间到，休息一下吧。", "Mori Timer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ConfigureDuration()
        {
            using (var dialog = new DurationDialog(durationMilliseconds / 1000))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                durationMilliseconds = dialog.TotalSeconds * 1000;
                accumulatedMilliseconds = 0;
                Invalidate();
            }
        }

        private long CurrentElapsedMilliseconds => state == TimerState.Running
            ? accumulatedMilliseconds + Math.Max(0, (long)(DateTime.UtcNow - runStartedAt).TotalMilliseconds)
            : accumulatedMilliseconds;
        private long RemainingMilliseconds => Math.Max(0, durationMilliseconds - CurrentElapsedMilliseconds);

        private string StatusText()
        {
            if (state == TimerState.Running) return "正在专注";
            if (state == TimerState.Paused) return "已暂停";
            if (state == TimerState.Finished) return "已完成";
            return mode == TimerMode.Countdown ? "选择时长，然后开始" : "准备开始";
        }

        private static string FormatCountdown(long milliseconds)
        {
            long total = (long)Math.Ceiling(milliseconds / 1000D);
            long hours = total / 3600;
            long minutes = total % 3600 / 60;
            long seconds = total % 60;
            return hours > 0 ? string.Format("{0}:{1:00}:{2:00}", hours, minutes, seconds) : string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        private static string FormatStopwatch(long milliseconds)
        {
            return string.Format("{0:00}:{1:00}.{2:00}", milliseconds / 60000, milliseconds % 60000 / 1000, milliseconds % 1000 / 10);
        }

        private static void CircleIcon(Graphics g, Rectangle bounds, Color background, string value, Color foreground, float size)
        {
            using (var brush = new SolidBrush(background)) g.FillEllipse(brush, bounds);
            DrawLabel(g, value, size, FontStyle.Bold, foreground, bounds, StringAlignment.Center);
        }

        private static void DrawLabel(Graphics g, string value, float size, FontStyle style, Color color, Rectangle bounds, StringAlignment alignment)
        {
            using (var font = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                g.DrawString(value, font, brush, bounds, format);
        }
    }

    internal static class Shape
    {
        public static void Fill(Graphics g, Rectangle bounds, int radius, Color color)
        {
            using (var path = RoundRect(bounds, radius))
            using (var brush = new SolidBrush(color)) g.FillPath(brush, path);
        }

        public static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class DurationDialog : Form
    {
        private readonly NumericUpDown hours;
        private readonly NumericUpDown minutes;
        private readonly NumericUpDown seconds;
        public long TotalSeconds => (long)hours.Value * 3600 + (long)minutes.Value * 60 + (long)seconds.Value;

        public DurationDialog(long totalSeconds)
        {
            Text = "自定义时长";
            ClientSize = new Size(368, 205);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Colors.Window;
            Font = new Font("Microsoft YaHei UI", 10F);
            hours = Input(totalSeconds / 3600, 0, 23, 24);
            minutes = Input(totalSeconds % 3600 / 60, 0, 59, 136);
            seconds = Input(totalSeconds % 60, 0, 59, 248);
            Controls.AddRange(new Control[] { Label("小时", 24), Label("分钟", 136), Label("秒", 248), hours, minutes, seconds });
            var cancel = Button("取消", DialogResult.Cancel, 178, Colors.Soft, Colors.Text);
            var confirm = Button("完成", DialogResult.OK, 272, Colors.Accent, Color.White);
            Controls.AddRange(new Control[] { cancel, confirm });
            AcceptButton = confirm;
            CancelButton = cancel;
        }

        private NumericUpDown Input(decimal value, decimal min, decimal max, int x) => new NumericUpDown
        {
            Value = value, Minimum = min, Maximum = max, Location = new Point(x, 62), Size = new Size(96, 38),
            Font = new Font("Microsoft YaHei UI", 14F), TextAlign = HorizontalAlignment.Center, BackColor = Color.White, ForeColor = Colors.Text
        };
        private Label Label(string value, int x) => new Label { Text = value, Location = new Point(x, 34), Size = new Size(96, 24), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Colors.Secondary };
        private Button Button(string value, DialogResult result, int x, Color background, Color foreground) => new Button
        {
            Text = value, DialogResult = result, Location = new Point(x, 145), Size = new Size(78, 38), FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 }, BackColor = background, ForeColor = foreground, Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
        };
    }
}
