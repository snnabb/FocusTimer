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

    internal enum TimerMode
    {
        Countdown,
        Stopwatch
    }

    internal sealed class TimerForm : Form
    {
        private readonly Label modeLabel;
        private readonly Label timeLabel;
        private readonly Label inputHintLabel;
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
            Text = "专注计时器";
            ClientSize = new Size(520, 430);
            MinimumSize = new Size(536, 469);
            BackColor = Color.FromArgb(15, 23, 42);
            ForeColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10F);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            var titleLabel = new Label
            {
                Text = "专注计时器",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(38, 25),
                AutoSize = true
            };

            topMostCheckBox = new CheckBox
            {
                Text = "窗口置顶",
                ForeColor = Color.FromArgb(203, 213, 225),
                BackColor = BackColor,
                AutoSize = true,
                Location = new Point(405, 36)
            };
            topMostCheckBox.CheckedChanged += delegate { TopMost = topMostCheckBox.Checked; };

            countdownButton = CreateButton("倒计时", Color.FromArgb(37, 99, 235), new Point(38, 78), new Size(210, 42));
            countdownButton.Click += delegate { SelectMode(TimerMode.Countdown); };
            stopwatchButton = CreateButton("正计时", Color.FromArgb(51, 65, 85), new Point(272, 78), new Size(210, 42));
            stopwatchButton.Click += delegate { SelectMode(TimerMode.Stopwatch); };

            modeLabel = new Label
            {
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(191, 219, 254),
                Location = new Point(38, 142),
                Size = new Size(444, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };

            timeLabel = new Label
            {
                Font = new Font("Cascadia Mono", 72F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(22, 169),
                Size = new Size(476, 108),
                UseCompatibleTextRendering = false
            };

            inputHintLabel = new Label
            {
                Text = "设置倒计时长度",
                ForeColor = Color.FromArgb(203, 213, 225),
                Location = new Point(118, 289),
                AutoSize = true
            };
            minutesLabel = CreateCaption("分钟", new Point(211, 289));
            secondsLabel = CreateCaption("秒", new Point(352, 289));

            minutesInput = CreateNumberInput(25, 0, 999, new Point(186, 314));
            secondsInput = CreateNumberInput(0, 0, 59, new Point(327, 314));
            minutesInput.ValueChanged += delegate { PreviewCountdown(); };
            secondsInput.ValueChanged += delegate { PreviewCountdown(); };

            startPauseButton = CreateButton("开始", Color.FromArgb(22, 163, 74), new Point(118, 365), new Size(137, 45));
            startPauseButton.Click += StartPauseButton_Click;
            resetButton = CreateButton("重置", Color.FromArgb(71, 85, 105), new Point(265, 365), new Size(137, 45));
            resetButton.Click += delegate { ResetTimer(); };

            tickTimer = new Timer { Interval = 100 };
            tickTimer.Tick += TickTimer_Tick;

            Controls.AddRange(new Control[]
            {
                titleLabel, topMostCheckBox, countdownButton, stopwatchButton, modeLabel, timeLabel,
                inputHintLabel, minutesLabel, secondsLabel, minutesInput, secondsInput,
                startPauseButton, resetButton
            });

            SelectMode(TimerMode.Countdown);
        }

        private Label CreateCaption(string text, Point location)
        {
            return new Label
            {
                Text = text,
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = location,
                AutoSize = true
            };
        }

        private NumericUpDown CreateNumberInput(decimal value, decimal minimum, decimal maximum, Point location)
        {
            return new NumericUpDown
            {
                Value = value,
                Minimum = minimum,
                Maximum = maximum,
                Font = new Font("Cascadia Mono", 14F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                Location = location,
                Size = new Size(96, 32),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(15, 23, 42)
            };
        }

        private Button CreateButton(string text, Color color, Point location, Size size)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
        }

        private void SelectMode(TimerMode newMode)
        {
            if (mode == newMode && isRunning)
            {
                return;
            }

            tickTimer.Stop();
            isRunning = false;
            mode = newMode;
            startPauseButton.Text = "开始";
            startPauseButton.BackColor = Color.FromArgb(22, 163, 74);

            bool isCountdown = mode == TimerMode.Countdown;
            countdownButton.BackColor = isCountdown ? Color.FromArgb(37, 99, 235) : Color.FromArgb(51, 65, 85);
            stopwatchButton.BackColor = isCountdown ? Color.FromArgb(51, 65, 85) : Color.FromArgb(37, 99, 235);
            inputHintLabel.Visible = isCountdown;
            minutesLabel.Visible = isCountdown;
            secondsLabel.Visible = isCountdown;
            minutesInput.Visible = isCountdown;
            secondsInput.Visible = isCountdown;

            if (isCountdown)
            {
                displayedSeconds = GetInputSeconds();
                modeLabel.Text = "倒计时 · 完成后会提醒你";
            }
            else
            {
                displayedSeconds = 0;
                modeLabel.Text = "正计时 · 记录你的专注时长";
            }

            UpdateTimeDisplay();
        }

        private void StartPauseButton_Click(object? sender, EventArgs e)
        {
            if (isRunning)
            {
                PauseTimer();
                return;
            }

            if (mode == TimerMode.Countdown && displayedSeconds == 0)
            {
                displayedSeconds = GetInputSeconds();
            }

            if (mode == TimerMode.Countdown && displayedSeconds == 0)
            {
                MessageBox.Show("请设置大于 0 的倒计时时长。", "专注计时器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isRunning = true;
            runStartedAt = DateTime.UtcNow;
            tickTimer.Start();
            startPauseButton.Text = "暂停";
            startPauseButton.BackColor = Color.FromArgb(217, 119, 6);
            minutesInput.Enabled = false;
            secondsInput.Enabled = false;
        }

        private void PauseTimer()
        {
            UpdateRunningTime();
            isRunning = false;
            tickTimer.Stop();
            startPauseButton.Text = "继续";
            startPauseButton.BackColor = Color.FromArgb(22, 163, 74);
            minutesInput.Enabled = mode == TimerMode.Countdown;
            secondsInput.Enabled = mode == TimerMode.Countdown;
        }

        private void TickTimer_Tick(object? sender, EventArgs e)
        {
            UpdateRunningTime();

            if (mode != TimerMode.Countdown || displayedSeconds > 0)
            {
                return;
            }

            tickTimer.Stop();
            isRunning = false;
            startPauseButton.Text = "开始";
            startPauseButton.BackColor = Color.FromArgb(22, 163, 74);
            minutesInput.Enabled = true;
            secondsInput.Enabled = true;
            SystemSounds.Exclamation.Play();
            MessageBox.Show("时间到，休息一下吧。", "专注计时器", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateRunningTime()
        {
            int elapsed = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - runStartedAt).TotalSeconds));
            if (elapsed == 0)
            {
                return;
            }

            displayedSeconds = mode == TimerMode.Countdown
                ? Math.Max(0, displayedSeconds - elapsed)
                : displayedSeconds + elapsed;
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
            startPauseButton.BackColor = Color.FromArgb(22, 163, 74);
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

        private int GetInputSeconds()
        {
            return (int)minutesInput.Value * 60 + (int)secondsInput.Value;
        }

        private void UpdateTimeDisplay()
        {
            int hours = displayedSeconds / 3600;
            int minutes = displayedSeconds % 3600 / 60;
            int seconds = displayedSeconds % 60;
            timeLabel.Text = hours > 0
                ? string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds)
                : string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
}
