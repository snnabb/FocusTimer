using System;
using System.Drawing;
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
        private static readonly Color GoogleBlue = Color.FromArgb(26, 115, 232);
        private static readonly Color GoogleRed = Color.FromArgb(217, 48, 37);
        private static readonly Color PrimaryText = Color.FromArgb(32, 33, 36);
        private static readonly Color Muted = Color.FromArgb(95, 99, 104);
        private static readonly Color Border = Color.FromArgb(218, 220, 224);
        private static readonly Font Ui = new Font("Segoe UI", 10F);
        private readonly Label modeLabel;
        private readonly Label timeLabel;
        private readonly Label durationLabel;
        private readonly NumericUpDown minutesInput;
        private readonly NumericUpDown secondsInput;
        private readonly Label minutesLabel;
        private readonly Label secondsLabel;
        private readonly Button countdownButton;
        private readonly Button stopwatchButton;
        private readonly Button startPauseButton;
        private readonly Button resetButton;
        private readonly CheckBox topMostCheckBox;
        private readonly Timer tickTimer;
        private TimerMode mode = TimerMode.Countdown;
        private int displayedSeconds = 25 * 60;
        private bool isRunning;
        private DateTime runStartedAt;

        public TimerForm()
        {
            base.Text = "计时器";
            ClientSize = new Size(600, 480);
            MinimumSize = new Size(616, 519);
            BackColor = Color.White;
            ForeColor = PrimaryText;
            Font = Ui;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            var title = new Label
            {
                Text = "计时器",
                Font = new Font("Segoe UI", 20F),
                ForeColor = PrimaryText,
                Location = new Point(40, 27),
                AutoSize = true
            };
            topMostCheckBox = new CheckBox
            {
                Text = "窗口置顶",
                ForeColor = Muted,
                BackColor = Color.White,
                Location = new Point(472, 35),
                AutoSize = true
            };
            topMostCheckBox.CheckedChanged += delegate { TopMost = topMostCheckBox.Checked; };

            countdownButton = CreateTabButton("倒计时", new Point(40, 82));
            countdownButton.Click += delegate { SelectMode(TimerMode.Countdown); };
            stopwatchButton = CreateTabButton("正计时", new Point(154, 82));
            stopwatchButton.Click += delegate { SelectMode(TimerMode.Stopwatch); };

            var card = new Panel
            {
                Location = new Point(40, 132),
                Size = new Size(520, 298),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            modeLabel = new Label
            {
                Font = new Font("Segoe UI", 11F),
                ForeColor = Muted,
                Location = new Point(18, 22),
                Size = new Size(482, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };
            timeLabel = new Label
            {
                Font = new Font("Segoe UI", 64F),
                ForeColor = PrimaryText,
                Location = new Point(12, 44),
                Size = new Size(494, 111),
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = false
            };

            durationLabel = new Label
            {
                Text = "设置倒计时时长",
                ForeColor = Muted,
                Location = new Point(116, 175),
                AutoSize = true
            };
            minutesInput = CreateNumberInput(25, 0, 999, new Point(254, 166));
            minutesLabel = CreateCaption("分", new Point(330, 177));
            secondsInput = CreateNumberInput(0, 0, 59, new Point(360, 166));
            secondsLabel = CreateCaption("秒", new Point(436, 177));
            minutesInput.ValueChanged += delegate { PreviewCountdown(); };
            secondsInput.ValueChanged += delegate { PreviewCountdown(); };

            startPauseButton = CreateActionButton("开始", GoogleBlue, Color.White, new Point(161, 222), new Size(96, 44));
            startPauseButton.Click += StartPauseButton_Click;
            resetButton = CreateActionButton("重置", Color.White, GoogleBlue, new Point(271, 222), new Size(96, 44));
            resetButton.FlatAppearance.BorderColor = Border;
            resetButton.Click += delegate { ResetTimer(); };

            card.Controls.AddRange(new Control[]
            {
                modeLabel, timeLabel, durationLabel, minutesInput, minutesLabel, secondsInput, secondsLabel,
                startPauseButton, resetButton
            });

            var hint = new Label
            {
                Text = "点击模式切换计时方式。倒计时结束会播放系统提示音。",
                ForeColor = Muted,
                Location = new Point(40, 447),
                AutoSize = true
            };

            tickTimer = new Timer { Interval = 100 };
            tickTimer.Tick += TickTimer_Tick;
            Controls.AddRange(new Control[] { title, topMostCheckBox, countdownButton, stopwatchButton, card, hint });
            SelectMode(TimerMode.Countdown);
        }

        private Button CreateTabButton(string text, Point location)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = new Size(100, 36),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand
            };
        }

        private NumericUpDown CreateNumberInput(decimal value, decimal minimum, decimal maximum, Point location)
        {
            return new NumericUpDown
            {
                Value = value,
                Minimum = minimum,
                Maximum = maximum,
                Location = location,
                Size = new Size(70, 31),
                Font = new Font("Segoe UI", 12F),
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.White,
                ForeColor = PrimaryText
            };
        }

        private Label CreateCaption(string text, Point location)
        {
            return new Label { Text = text, ForeColor = Muted, Location = location, AutoSize = true };
        }

        private Button CreateActionButton(string text, Color backColor, Color foreColor, Point location, Size size)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1 },
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
        }

        private void SelectMode(TimerMode newMode)
        {
            tickTimer.Stop();
            isRunning = false;
            mode = newMode;
            bool countdown = mode == TimerMode.Countdown;
            countdownButton.BackColor = countdown ? Color.FromArgb(232, 240, 254) : Color.White;
            countdownButton.ForeColor = countdown ? GoogleBlue : Muted;
            stopwatchButton.BackColor = countdown ? Color.White : Color.FromArgb(232, 240, 254);
            stopwatchButton.ForeColor = countdown ? Muted : GoogleBlue;
            durationLabel.Visible = countdown;
            minutesInput.Visible = countdown;
            minutesLabel.Visible = countdown;
            secondsInput.Visible = countdown;
            secondsLabel.Visible = countdown;
            minutesInput.Enabled = countdown;
            secondsInput.Enabled = countdown;
            modeLabel.Text = countdown ? "倒计时" : "正计时";
            displayedSeconds = countdown ? GetInputSeconds() : 0;
            startPauseButton.Text = "开始";
            startPauseButton.BackColor = GoogleBlue;
            UpdateTimeDisplay();
        }

        private void StartPauseButton_Click(object? sender, EventArgs e)
        {
            if (isRunning) { PauseTimer(); return; }
            if (mode == TimerMode.Countdown && displayedSeconds == 0) displayedSeconds = GetInputSeconds();
            if (mode == TimerMode.Countdown && displayedSeconds == 0)
            {
                MessageBox.Show("请设置大于 0 的倒计时时长。", "计时器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            isRunning = true;
            runStartedAt = DateTime.UtcNow;
            tickTimer.Start();
            startPauseButton.Text = "暂停";
            startPauseButton.BackColor = GoogleRed;
            minutesInput.Enabled = false;
            secondsInput.Enabled = false;
        }

        private void PauseTimer()
        {
            UpdateRunningTime();
            isRunning = false;
            tickTimer.Stop();
            startPauseButton.Text = "继续";
            startPauseButton.BackColor = GoogleBlue;
            minutesInput.Enabled = mode == TimerMode.Countdown;
            secondsInput.Enabled = mode == TimerMode.Countdown;
        }

        private void TickTimer_Tick(object? sender, EventArgs e)
        {
            UpdateRunningTime();
            if (mode != TimerMode.Countdown || displayedSeconds > 0) return;
            tickTimer.Stop();
            isRunning = false;
            startPauseButton.Text = "开始";
            startPauseButton.BackColor = GoogleBlue;
            minutesInput.Enabled = true;
            secondsInput.Enabled = true;
            SystemSounds.Exclamation.Play();
            MessageBox.Show("时间到。", "计时器", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateRunningTime()
        {
            int elapsed = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - runStartedAt).TotalSeconds));
            if (elapsed == 0) return;
            displayedSeconds = mode == TimerMode.Countdown ? Math.Max(0, displayedSeconds - elapsed) : displayedSeconds + elapsed;
            runStartedAt = runStartedAt.AddSeconds(elapsed);
            UpdateTimeDisplay();
        }

        private void ResetTimer()
        {
            tickTimer.Stop();
            isRunning = false;
            displayedSeconds = mode == TimerMode.Countdown ? GetInputSeconds() : 0;
            minutesInput.Enabled = mode == TimerMode.Countdown;
            secondsInput.Enabled = mode == TimerMode.Countdown;
            startPauseButton.Text = "开始";
            startPauseButton.BackColor = GoogleBlue;
            UpdateTimeDisplay();
        }

        private void PreviewCountdown()
        {
            if (mode == TimerMode.Countdown && !isRunning)
            {
                displayedSeconds = GetInputSeconds();
                UpdateTimeDisplay();
            }
        }

        private int GetInputSeconds() => (int)minutesInput.Value * 60 + (int)secondsInput.Value;

        private void UpdateTimeDisplay()
        {
            int hours = displayedSeconds / 3600;
            int minutes = displayedSeconds % 3600 / 60;
            int seconds = displayedSeconds % 60;
            timeLabel.Text = hours > 0 ? string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds) : string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
}
