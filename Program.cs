using System;
using System.Collections.Generic;
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

    internal sealed class TimerForm : Form
    {
        public TimerForm()
        {
            Text = "Focus Timer";
            ClientSize = new Size(520, 680);
            MinimumSize = new Size(536, 719);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Palette.Background;
            KeyPreview = true;
            Controls.Add(new TimerCanvas { Dock = DockStyle.Fill });
        }
    }

    internal enum TimerMode { Timer, Stopwatch }
    internal enum TimerState { Ready, Running, Paused, Finished }

    internal static class Palette
    {
        public static readonly Color Background = Color.FromArgb(11, 11, 12);
        public static readonly Color Surface = Color.FromArgb(28, 28, 30);
        public static readonly Color SurfaceRaised = Color.FromArgb(44, 44, 46);
        public static readonly Color Separator = Color.FromArgb(56, 56, 58);
        public static readonly Color Primary = Color.FromArgb(242, 242, 247);
        public static readonly Color Secondary = Color.FromArgb(142, 142, 147);
        public static readonly Color Orange = Color.FromArgb(255, 159, 10);
        public static readonly Color OrangeDim = Color.FromArgb(72, 48, 14);
        public static readonly Color Green = Color.FromArgb(48, 209, 88);
        public static readonly Color GreenDim = Color.FromArgb(21, 65, 34);
        public static readonly Color Red = Color.FromArgb(255, 69, 58);
        public static readonly Color RedDim = Color.FromArgb(73, 29, 27);
        public static readonly Color Blue = Color.FromArgb(10, 132, 255);
    }

    internal sealed class TimerCanvas : Control
    {
        private readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer { Interval = 16 };
        private readonly List<long> laps = new List<long>();
        private TimerMode mode = TimerMode.Timer;
        private TimerState state = TimerState.Ready;
        private long durationMilliseconds = 25 * 60 * 1000;
        private long accumulatedMilliseconds;
        private long lastLapMilliseconds;
        private DateTime runStartedAt;
        private bool soundEnabled = true;
        private bool pinned;
        private Rectangle timerTab;
        private Rectangle stopwatchTab;
        private Rectangle soundRect;
        private Rectangle pinRect;
        private Rectangle primaryAction;
        private Rectangle secondaryAction;
        private Rectangle customDurationRect;
        private readonly Rectangle[] presetRects = new Rectangle[4];
        private static readonly int[] PresetMinutes = { 5, 10, 25, 45 };

        public TimerCanvas()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            TabStop = true;
            Font = new Font("Segoe UI", 10F);
            ticker.Tick += delegate { Invalidate(); CheckCompletion(); };
            MouseUp += HandleMouseUp;
            KeyDown += HandleKeyDown;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            DrawHeader(e.Graphics);
            DrawSegmentedControl(e.Graphics);
            if (mode == TimerMode.Timer) DrawTimer(e.Graphics); else DrawStopwatch(e.Graphics);
            DrawActions(e.Graphics);
        }

        private void DrawHeader(Graphics g)
        {
            DrawText(g, "Focus", 26F, FontStyle.Bold, Palette.Primary, new Rectangle(28, 22, 250, 42), StringAlignment.Near);
            DrawText(g, mode == TimerMode.Timer ? "专注计时器" : "精准秒表", 9.5F, FontStyle.Regular, Palette.Secondary, new Rectangle(30, 59, 220, 24), StringAlignment.Near);
            soundRect = new Rectangle(414, 28, 34, 34);
            pinRect = new Rectangle(462, 28, 34, 34);
            DrawCircleButton(g, soundRect, Palette.Surface, soundEnabled ? "♪" : "×", soundEnabled ? Palette.Primary : Palette.Secondary, 13F);
            DrawCircleButton(g, pinRect, pinned ? Palette.OrangeDim : Palette.Surface, "⌖", pinned ? Palette.Orange : Palette.Secondary, 13F);
        }

        private void DrawSegmentedControl(Graphics g)
        {
            var bounds = new Rectangle(28, 94, Width - 56, 44);
            FillRoundRect(g, bounds, 13, Palette.Surface);
            timerTab = new Rectangle(bounds.X + 4, bounds.Y + 4, bounds.Width / 2 - 4, bounds.Height - 8);
            stopwatchTab = new Rectangle(bounds.X + bounds.Width / 2, bounds.Y + 4, bounds.Width / 2 - 4, bounds.Height - 8);
            Rectangle selected = mode == TimerMode.Timer ? timerTab : stopwatchTab;
            FillRoundRect(g, selected, 10, Palette.SurfaceRaised);
            DrawText(g, "计时器", 10F, FontStyle.Bold, mode == TimerMode.Timer ? Palette.Primary : Palette.Secondary, timerTab, StringAlignment.Center);
            DrawText(g, "秒表", 10F, FontStyle.Bold, mode == TimerMode.Stopwatch ? Palette.Primary : Palette.Secondary, stopwatchTab, StringAlignment.Center);
        }

        private void DrawTimer(Graphics g)
        {
            long remaining = RemainingMilliseconds;
            var dial = new Rectangle(83, 166, Width - 166, Width - 166);
            DrawProgressRing(g, dial, durationMilliseconds == 0 ? 0F : 1F - (float)remaining / durationMilliseconds, Palette.Orange);
            string time = FormatTimer(remaining);
            DrawText(g, time, time.Length > 5 ? 44F : 56F, FontStyle.Regular, Palette.Primary, new Rectangle(dial.X + 18, dial.Y + 105, dial.Width - 36, 82), StringAlignment.Center);
            DrawText(g, StateText(), 9.5F, FontStyle.Regular, state == TimerState.Finished ? Palette.Red : Palette.Secondary, new Rectangle(dial.X, dial.Y + 194, dial.Width, 26), StringAlignment.Center);

            int chipWidth = 74;
            int gap = 10;
            int totalWidth = chipWidth * 4 + gap * 3;
            int startX = (Width - totalWidth) / 2;
            for (int i = 0; i < PresetMinutes.Length; i++)
            {
                presetRects[i] = new Rectangle(startX + i * (chipWidth + gap), 535, chipWidth, 36);
                bool selected = durationMilliseconds == PresetMinutes[i] * 60L * 1000L && state == TimerState.Ready;
                FillRoundRect(g, presetRects[i], 18, selected ? Palette.OrangeDim : Palette.Surface);
                DrawText(g, PresetMinutes[i] + " 分钟", 9F, FontStyle.Bold, selected ? Palette.Orange : Palette.Secondary, presetRects[i], StringAlignment.Center);
            }
            customDurationRect = new Rectangle((Width - 132) / 2, 500, 132, 26);
            DrawText(g, "点击自定义时长  ›", 9F, FontStyle.Regular, Palette.Secondary, customDurationRect, StringAlignment.Center);
        }

        private void DrawStopwatch(Graphics g)
        {
            long elapsed = CurrentElapsedMilliseconds;
            DrawText(g, FormatStopwatch(elapsed), 58F, FontStyle.Regular, Palette.Primary, new Rectangle(30, 184, Width - 60, 112), StringAlignment.Center);
            DrawText(g, state == TimerState.Running ? "计时中" : state == TimerState.Paused ? "已暂停" : "准备就绪", 9.5F, FontStyle.Regular, Palette.Secondary, new Rectangle(30, 286, Width - 60, 24), StringAlignment.Center);
            DrawLapList(g, elapsed);
        }

        private void DrawLapList(Graphics g, long elapsed)
        {
            int y = 340;
            DrawText(g, "计次", 9F, FontStyle.Bold, Palette.Secondary, new Rectangle(42, y, 100, 24), StringAlignment.Near);
            DrawText(g, "本圈", 9F, FontStyle.Bold, Palette.Secondary, new Rectangle(200, y, 100, 24), StringAlignment.Center);
            DrawText(g, "总计", 9F, FontStyle.Bold, Palette.Secondary, new Rectangle(360, y, 110, 24), StringAlignment.Far);
            y += 32;
            using (var separator = new Pen(Palette.Separator, 1F)) g.DrawLine(separator, 38, y, Width - 38, y);
            int shown = Math.Min(4, laps.Count + (state == TimerState.Running || state == TimerState.Paused ? 1 : 0));
            for (int row = 0; row < shown; row++)
            {
                int lapIndex = laps.Count - row;
                bool current = lapIndex == laps.Count;
                long total = current ? elapsed : laps[lapIndex];
                long previous = lapIndex == 0 ? 0 : laps[lapIndex - 1];
                long lap = total - previous;
                y += 39;
                DrawText(g, "计次 " + (lapIndex + 1), 10F, FontStyle.Regular, Palette.Primary, new Rectangle(42, y - 18, 100, 26), StringAlignment.Near);
                DrawText(g, FormatLap(lap), 10F, FontStyle.Regular, Palette.Primary, new Rectangle(170, y - 18, 150, 26), StringAlignment.Center);
                DrawText(g, FormatLap(total), 10F, FontStyle.Regular, Palette.Primary, new Rectangle(340, y - 18, 138, 26), StringAlignment.Far);
                using (var separator = new Pen(Palette.Separator, 1F)) g.DrawLine(separator, 38, y + 13, Width - 38, y + 13);
            }
            if (shown == 0)
                DrawText(g, "开始后可记录计次", 10F, FontStyle.Regular, Palette.Secondary, new Rectangle(38, 405, Width - 76, 40), StringAlignment.Center);
        }

        private void DrawActions(Graphics g)
        {
            int y = Height - 94;
            secondaryAction = new Rectangle(44, y, 68, 68);
            primaryAction = new Rectangle(Width - 112, y, 68, 68);
            string secondaryText = mode == TimerMode.Stopwatch && state == TimerState.Running ? "计次" : "复位";
            bool canSecondary = state != TimerState.Ready;
            DrawCircleButton(g, secondaryAction, canSecondary ? Palette.SurfaceRaised : Palette.Surface, secondaryText, canSecondary ? Palette.Primary : Palette.Secondary, 9F);
            bool running = state == TimerState.Running;
            DrawCircleButton(g, primaryAction, running ? Palette.OrangeDim : Palette.GreenDim, running ? "暂停" : "开始", running ? Palette.Orange : Palette.Green, 10F);
        }

        private void DrawProgressRing(Graphics g, Rectangle bounds, float progress, Color accent)
        {
            using (var track = new Pen(Palette.SurfaceRaised, 12F))
            using (var active = new Pen(accent, 12F))
            {
                track.StartCap = track.EndCap = LineCap.Round;
                active.StartCap = active.EndCap = LineCap.Round;
                var inset = Rectangle.Inflate(bounds, -12, -12);
                g.DrawArc(track, inset, -90, 360);
                if (progress > 0.001F) g.DrawArc(active, inset, -90, Math.Min(359.8F, progress * 360F));
            }
        }

        private void HandleMouseUp(object? sender, MouseEventArgs e)
        {
            Focus();
            if (timerTab.Contains(e.Location)) { SwitchMode(TimerMode.Timer); return; }
            if (stopwatchTab.Contains(e.Location)) { SwitchMode(TimerMode.Stopwatch); return; }
            if (soundRect.Contains(e.Location)) { soundEnabled = !soundEnabled; Invalidate(); return; }
            if (pinRect.Contains(e.Location)) { pinned = !pinned; FindForm()!.TopMost = pinned; Invalidate(); return; }
            if (primaryAction.Contains(e.Location)) { ToggleRunning(); return; }
            if (secondaryAction.Contains(e.Location) && state != TimerState.Ready)
            {
                if (mode == TimerMode.Stopwatch && state == TimerState.Running) AddLap(); else Reset();
                return;
            }
            if (mode == TimerMode.Timer && state == TimerState.Ready)
            {
                for (int i = 0; i < presetRects.Length; i++)
                    if (presetRects[i].Contains(e.Location)) { durationMilliseconds = PresetMinutes[i] * 60L * 1000L; accumulatedMilliseconds = 0; Invalidate(); return; }
                if (customDurationRect.Contains(e.Location)) ConfigureDuration();
            }
        }

        private void HandleKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { ToggleRunning(); e.Handled = true; }
            else if (e.KeyCode == Keys.R) Reset();
            else if (e.KeyCode == Keys.D1) SwitchMode(TimerMode.Timer);
            else if (e.KeyCode == Keys.D2) SwitchMode(TimerMode.Stopwatch);
            else if (e.KeyCode == Keys.L && mode == TimerMode.Stopwatch && state == TimerState.Running) AddLap();
        }

        private void SwitchMode(TimerMode newMode)
        {
            ticker.Stop();
            mode = newMode;
            state = TimerState.Ready;
            accumulatedMilliseconds = 0;
            lastLapMilliseconds = 0;
            laps.Clear();
            Invalidate();
        }

        private void ToggleRunning()
        {
            if (state == TimerState.Running)
            {
                accumulatedMilliseconds = CurrentElapsedMilliseconds;
                ticker.Stop();
                state = TimerState.Paused;
            }
            else
            {
                if (mode == TimerMode.Timer && RemainingMilliseconds <= 0) accumulatedMilliseconds = 0;
                runStartedAt = DateTime.UtcNow;
                ticker.Start();
                state = TimerState.Running;
            }
            Invalidate();
        }

        private void Reset()
        {
            ticker.Stop();
            accumulatedMilliseconds = 0;
            lastLapMilliseconds = 0;
            laps.Clear();
            state = TimerState.Ready;
            Invalidate();
        }

        private void AddLap()
        {
            long elapsed = CurrentElapsedMilliseconds;
            if (elapsed <= lastLapMilliseconds) return;
            laps.Add(elapsed);
            lastLapMilliseconds = elapsed;
            Invalidate();
        }

        private void CheckCompletion()
        {
            if (mode != TimerMode.Timer || state != TimerState.Running || RemainingMilliseconds > 0) return;
            accumulatedMilliseconds = durationMilliseconds;
            ticker.Stop();
            state = TimerState.Finished;
            Invalidate();
            if (soundEnabled) SystemSounds.Exclamation.Play();
            MessageBox.Show(FindForm(), "时间到。休息一下，再继续。", "Focus Timer", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private long CurrentElapsedMilliseconds
        {
            get
            {
                if (state != TimerState.Running) return accumulatedMilliseconds;
                return accumulatedMilliseconds + Math.Max(0, (long)(DateTime.UtcNow - runStartedAt).TotalMilliseconds);
            }
        }

        private long RemainingMilliseconds => Math.Max(0, durationMilliseconds - CurrentElapsedMilliseconds);

        private string StateText()
        {
            if (state == TimerState.Running) return "专注中";
            if (state == TimerState.Paused) return "已暂停";
            if (state == TimerState.Finished) return "已完成";
            return "轻点开始，保持专注";
        }

        private static string FormatTimer(long milliseconds)
        {
            long totalSeconds = (long)Math.Ceiling(milliseconds / 1000D);
            long hours = totalSeconds / 3600;
            long minutes = totalSeconds % 3600 / 60;
            long seconds = totalSeconds % 60;
            return hours > 0 ? string.Format("{0}:{1:00}:{2:00}", hours, minutes, seconds) : string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        private static string FormatStopwatch(long milliseconds)
        {
            long minutes = milliseconds / 60000;
            long seconds = milliseconds % 60000 / 1000;
            long hundredths = milliseconds % 1000 / 10;
            return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, hundredths);
        }

        private static string FormatLap(long milliseconds)
        {
            long minutes = milliseconds / 60000;
            long seconds = milliseconds % 60000 / 1000;
            long hundredths = milliseconds % 1000 / 10;
            return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, hundredths);
        }

        private static void DrawCircleButton(Graphics g, Rectangle bounds, Color background, string text, Color foreground, float fontSize)
        {
            using (var brush = new SolidBrush(background)) g.FillEllipse(brush, bounds);
            DrawText(g, text, fontSize, FontStyle.Bold, foreground, bounds, StringAlignment.Center);
        }

        private static void FillRoundRect(Graphics g, Rectangle bounds, int radius, Color color)
        {
            using (var path = RoundedPath(bounds, radius))
            using (var brush = new SolidBrush(color)) g.FillPath(brush, path);
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
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

        private static void DrawText(Graphics g, string text, float size, FontStyle style, Color color, Rectangle bounds, StringAlignment alignment)
        {
            using (var font = new Font("Segoe UI", size, style, GraphicsUnit.Point))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                g.DrawString(text, font, brush, bounds, format);
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
            ClientSize = new Size(380, 214);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Palette.Surface;
            ForeColor = Palette.Primary;
            Font = new Font("Segoe UI", 10F);
            hours = CreateInput(totalSeconds / 3600, 0, 23, 28);
            minutes = CreateInput(totalSeconds % 3600 / 60, 0, 59, 142);
            seconds = CreateInput(totalSeconds % 60, 0, 59, 256);
            Controls.AddRange(new Control[]
            {
                CreateLabel("小时", 28), CreateLabel("分钟", 142), CreateLabel("秒", 256), hours, minutes, seconds
            });
            var cancel = CreateDialogButton("取消", DialogResult.Cancel, new Point(192, 155), Palette.SurfaceRaised, Palette.Primary);
            var confirm = CreateDialogButton("完成", DialogResult.OK, new Point(282, 155), Palette.Orange, Color.Black);
            Controls.AddRange(new Control[] { cancel, confirm });
            AcceptButton = confirm;
            CancelButton = cancel;
        }

        private NumericUpDown CreateInput(decimal value, decimal minimum, decimal maximum, int x) => new NumericUpDown
        {
            Value = value, Minimum = minimum, Maximum = maximum, Location = new Point(x, 68), Size = new Size(96, 42),
            Font = new Font("Segoe UI", 17F), TextAlign = HorizontalAlignment.Center, BackColor = Palette.SurfaceRaised, ForeColor = Palette.Primary,
            BorderStyle = BorderStyle.FixedSingle
        };

        private Label CreateLabel(string text, int x) => new Label
        {
            Text = text, Location = new Point(x, 39), Size = new Size(96, 24), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Palette.Secondary
        };

        private Button CreateDialogButton(string text, DialogResult result, Point location, Color background, Color foreground) => new Button
        {
            Text = text, DialogResult = result, Location = location, Size = new Size(78, 38), FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 }, BackColor = background, ForeColor = foreground, Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
    }
}
