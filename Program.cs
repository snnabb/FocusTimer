using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Media;
using System.Runtime.InteropServices;
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
        private const int DwmWindowCornerPreference = 33;
        private const int DwmCornerRound = 2;
        private readonly TimerCanvas canvas;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        public TimerForm()
        {
            Text = "Mori Timer";
            ClientSize = new Size(720, 640);
            MinimumSize = new Size(560, 520);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            BackColor = Colors.Window;
            KeyPreview = true;
            try
            {
                string? processPath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(processPath))
                    Icon = Icon.ExtractAssociatedIcon(processPath);
            }
            catch
            {
            }
            canvas = new TimerCanvas { Dock = DockStyle.Fill };
            Controls.Add(canvas);
            Shown += delegate { canvas.Focus(); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
            int preference = DwmCornerRound;
            DwmSetWindowAttribute(Handle, DwmWindowCornerPreference, ref preference, sizeof(int));
        }
    }

    internal sealed class TimerCanvas : Control
    {
        private readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer { Interval = 16 };
        private TimerMode mode = TimerMode.Countdown;
        private TimerState state = TimerState.Ready;
        private long durationMilliseconds = 25 * 60 * 1000;
        private long accumulatedMilliseconds;
        private DateTime runStartedAt;
        private bool pinned;
        private Rectangle countdownTab;
        private Rectangle stopwatchTab;
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
            MouseDown += delegate { Focus(); };
            MouseUp += HandleMouseUp;
            KeyDown += HandleKeyDown;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Colors.Window);
            DrawTitleBar(g);
            DrawModePicker(g);
            DrawMainCard(g);
            DrawBottomActions(g);
        }

        private void DrawTitleBar(Graphics g)
        {
            int pad = Scale(36);
            int pinSize = Math.Max(32, Scale(38));
            DrawLabel(g, "Mori", ScaleF(17F), FontStyle.Bold, Colors.Text, new Rectangle(pad, Scale(25), Math.Max(120, Width / 3), Scale(38)), StringAlignment.Near);
            DrawLabel(g, "少一点干扰，多一点专注", ScaleF(9F), FontStyle.Regular, Colors.Secondary, new Rectangle(pad, Scale(62), Math.Max(180, Width / 2), Scale(24)), StringAlignment.Near);
            pinButton = new Rectangle(Width - pad - pinSize, Scale(32), pinSize, pinSize);
            CircleIcon(g, pinButton, pinned ? Colors.AccentSoft : Colors.Soft, "⌖", pinned ? Colors.Accent : Colors.Secondary, ScaleF(12F));
        }

        private void DrawModePicker(Graphics g)
        {
            int containerWidth = Math.Min(Scale(248), Width - Scale(80));
            int containerHeight = Math.Max(40, Scale(48));
            int containerX = (Width - containerWidth) / 2;
            int containerY = Scale(111);
            var container = new Rectangle(containerX, containerY, containerWidth, containerHeight);
            Shape.Fill(g, container, Math.Max(12, Scale(16)), Colors.Soft);
            int tabWidth = (containerWidth - Scale(8)) / 2;
            int tabHeight = containerHeight - Scale(8);
            countdownTab = new Rectangle(containerX + Scale(4), containerY + Scale(4), tabWidth, tabHeight);
            stopwatchTab = new Rectangle(countdownTab.Right, containerY + Scale(4), tabWidth, tabHeight);
            Shape.Fill(g, mode == TimerMode.Countdown ? countdownTab : stopwatchTab, Math.Max(10, Scale(13)), Colors.Card);
            DrawLabel(g, "倒计时", ScaleF(10F), FontStyle.Bold, mode == TimerMode.Countdown ? Colors.Text : Colors.Secondary, countdownTab, StringAlignment.Center);
            DrawLabel(g, "正计时", ScaleF(10F), FontStyle.Bold, mode == TimerMode.Stopwatch ? Colors.Text : Colors.Secondary, stopwatchTab, StringAlignment.Center);
        }

        private void DrawMainCard(Graphics g)
        {
            int side = Scale(54);
            int top = Scale(188);
            int bottomReserve = Scale(100);
            int cardWidth = Math.Max(280, Width - side * 2);
            int cardHeight = Math.Max(220, Height - top - bottomReserve);
            var card = new Rectangle(side, top, cardWidth, cardHeight);
            int radius = Math.Max(18, Scale(28));
            Shape.Fill(g, new Rectangle(card.X, card.Y + Math.Max(3, Scale(5)), card.Width, card.Height), radius, Color.FromArgb(11, 0, 0, 0));
            Shape.Fill(g, card, radius, Colors.Card);

            int contentX = card.X + Scale(36);
            int contentWidth = card.Width - Scale(72);
            string label = mode == TimerMode.Countdown ? "剩余时间" : "已专注";
            DrawLabel(g, label, ScaleF(10F), FontStyle.Regular, Colors.Secondary, new Rectangle(contentX, card.Y + Scale(28), contentWidth, Scale(28)), StringAlignment.Center);
            int labelBottom = card.Y + Scale(56);

            int zoneTop = labelBottom + Scale(8);
            int timeTop;
            int timeHeight;
            int statusTop;

            if (mode == TimerMode.Countdown)
            {
                int presetHeight = Math.Max(32, Scale(40));
                int presetTop = card.Bottom - presetHeight - Scale(18);
                int trackHeight = Math.Max(5, Scale(7));
                int trackTop = presetTop - trackHeight - Scale(14);
                int zoneBottom = trackTop - Scale(12);
                int zone = Math.Max(40, zoneBottom - zoneTop);
                timeHeight = Math.Max(48, (int)(zone * 0.64));
                timeTop = zoneTop + Math.Max(0, (zone - timeHeight - Scale(26)) / 2);
                statusTop = timeTop + timeHeight + Scale(6);

                var track = new Rectangle(card.X + Scale(60), trackTop, card.Width - Scale(120), trackHeight);
                Shape.Fill(g, track, Math.Max(3, Scale(4)), Colors.Soft);
                float progress = durationMilliseconds == 0 ? 0 : 1F - (float)RemainingMilliseconds / durationMilliseconds;
                if (progress > 0)
                {
                    var fill = new Rectangle(track.X, track.Y, Math.Max(7, (int)(track.Width * Math.Min(1F, progress))), track.Height);
                    Shape.Fill(g, fill, Math.Max(3, Scale(4)), Colors.Accent);
                }
                DrawPresets(g, card, presetTop);
            }
            else
            {
                int hintTop = card.Bottom - Scale(50);
                int zoneBottom = hintTop - Scale(10);
                int zone = Math.Max(40, zoneBottom - zoneTop);
                timeHeight = Math.Max(48, (int)(zone * 0.8));
                timeTop = zoneTop + Math.Max(0, (zone - timeHeight) / 2);
                statusTop = timeTop + timeHeight + Scale(6);
                DrawLabel(g, "精确到百分之一秒", ScaleF(9F), FontStyle.Regular, Colors.Secondary, new Rectangle(contentX, hintTop, contentWidth, Scale(26)), StringAlignment.Center);
            }

            string time = mode == TimerMode.Countdown ? FormatCountdown(RemainingMilliseconds) : FormatStopwatch(CurrentElapsedMilliseconds);
            var timeBounds = new Rectangle(card.X + Scale(22), timeTop, card.Width - Scale(44), timeHeight);
            DrawFittedTime(g, time, timeBounds);
            DrawLabel(g, StatusText(), ScaleF(9.5F), FontStyle.Regular, state == TimerState.Finished ? Colors.Danger : Colors.Secondary, new Rectangle(contentX, statusTop, contentWidth, Scale(27)), StringAlignment.Center);
        }

        private void DrawPresets(Graphics g, Rectangle card, int y)
        {
            int buttonCount = Presets.Length + 1;
            int gap = Scale(12);
            int buttonWidth = Math.Max(64, Math.Min(Scale(94), (card.Width - Scale(80) - gap * (buttonCount - 1)) / buttonCount));
            int buttonHeight = Math.Max(32, Scale(40));
            int totalWidth = buttonWidth * buttonCount + gap * (buttonCount - 1);
            int x = card.X + (card.Width - totalWidth) / 2;
            for (int i = 0; i < Presets.Length; i++)
            {
                presetButtons[i] = new Rectangle(x + i * (buttonWidth + gap), y, buttonWidth, buttonHeight);
                bool selected = state == TimerState.Ready && durationMilliseconds == Presets[i] * 60L * 1000L;
                Shape.Fill(g, presetButtons[i], Math.Max(14, Scale(20)), selected ? Colors.AccentSoft : Colors.Window);
                DrawLabel(g, Presets[i] + " 分", ScaleF(9.5F), FontStyle.Bold, selected ? Colors.Accent : Colors.Secondary, presetButtons[i], StringAlignment.Center);
            }
            customButton = new Rectangle(x + Presets.Length * (buttonWidth + gap), y, buttonWidth, buttonHeight);
            Shape.Fill(g, customButton, Math.Max(14, Scale(20)), Colors.Window);
            DrawLabel(g, "自定义", ScaleF(9.5F), FontStyle.Bold, Colors.Secondary, customButton, StringAlignment.Center);
        }

        private void DrawBottomActions(Graphics g)
        {
            int side = Scale(54);
            int height = Math.Max(44, Scale(52));
            int y = Height - side - height + Scale(8);
            int secondaryWidth = Math.Max(110, Scale(142));
            secondaryButton = new Rectangle(side, y, secondaryWidth, height);
            primaryButton = new Rectangle(secondaryButton.Right + Scale(18), y, Math.Max(180, Width - secondaryButton.Right - Scale(18) - side), height);
            bool secondaryEnabled = state != TimerState.Ready;
            Shape.Fill(g, secondaryButton, Math.Max(14, Scale(20)), secondaryEnabled ? Colors.Soft : Color.FromArgb(241, 241, 243));
            DrawLabel(g, "重置", ScaleF(10F), FontStyle.Bold, secondaryEnabled ? Colors.Secondary : Color.FromArgb(185, 185, 190), secondaryButton, StringAlignment.Center);
            bool running = state == TimerState.Running;
            Shape.Fill(g, primaryButton, Math.Max(14, Scale(20)), running ? Colors.Text : Colors.Accent);
            DrawLabel(g, running ? "暂停" : state == TimerState.Paused ? "继续" : "开始", ScaleF(10.5F), FontStyle.Bold, Color.White, primaryButton, StringAlignment.Center);
        }

        private int Scale(int value) => Math.Max(1, (int)Math.Round(value * Math.Min(Width / 720F, Height / 640F)));
        private float ScaleF(float value) => Math.Max(8F, value * Math.Min(Width / 720F, Height / 640F));



        private void HandleMouseUp(object? sender, MouseEventArgs e)
        {
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

        private string cachedTimeText = "";
        private Rectangle cachedTimeBounds;
        private float cachedTimeSize = -1F;

        private void DrawFittedTime(Graphics g, string value, Rectangle bounds)
        {
            float maximumSize = Math.Max(28F, Math.Min(96F, bounds.Height * 0.72F));
            float minimumSize = Math.Max(14F, bounds.Height * 0.18F);
            const float step = 1F;
            bool useCache = cachedTimeText == value && cachedTimeBounds == bounds && cachedTimeSize > 0;
            using (var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces
            })
            using (var brush = new SolidBrush(Colors.Text))
            {
                float size;
                if (useCache)
                {
                    size = cachedTimeSize;
                }
                else
                {
                    size = minimumSize;
                    for (float candidate = maximumSize; candidate >= minimumSize; candidate -= step)
                    {
                        using (var probe = new Font("Microsoft YaHei UI", candidate, FontStyle.Regular, GraphicsUnit.Point))
                        {
                            SizeF measured = g.MeasureString(value, probe, int.MaxValue, format);
                            if (measured.Width > bounds.Width - 24 || measured.Height > bounds.Height - 16) continue;
                            size = candidate;
                            break;
                        }
                    }
                    cachedTimeText = value;
                    cachedTimeBounds = bounds;
                    cachedTimeSize = size;
                }

                using (var font = new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Point))
                    g.DrawString(value, font, brush, bounds, format);
            }
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
