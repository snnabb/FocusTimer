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

    internal sealed class TimerForm : Form
    {
        private readonly Label timeLabel;
        private readonly NumericUpDown minutesInput;
        private readonly NumericUpDown secondsInput;
        private readonly Button startPauseButton;
        private readonly Button resetButton;
        private readonly CheckBox topMostCheckBox;
        private readonly Timer tickTimer;
        private int remainingSeconds;
        private bool isRunning;
        private DateTime endTime;

        public TimerForm()
        {
            Text = "专注计时器";
            ClientSize = new Size(440, 360);
            MinimumSize = new Size(456, 399);
            BackColor = Color.FromArgb(20, 27, 45);
            ForeColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10F);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            var titleLabel = new Label
            {
                Text = "专注计时器",
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(221, 231, 255),
                Location = new Point(34, 25),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "为此刻留出一段不被打扰的时间",
                ForeColor = Color.FromArgb(151, 166, 201),
                Location = new Point(36, 60),
                AutoSize = true
            };

            timeLabel = new Label
            {
                Text = "25:00",
                Font = new Font("Consolas", 64F, FontStyle.Bold),
                ForeColor = Color.FromArgb(122, 162, 247),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(26, 90),
                Size = new Size(388, 105)
            };

            var minutesLabel = CreateCaption("分钟", new Point(84, 205));
            var secondsLabel = CreateCaption("秒", new Point(237, 205));

            minutesInput = CreateNumberInput(25, 0, 999, new Point(84, 229));
            secondsInput = CreateNumberInput(0, 0, 59, new Point(237, 229));
            minutesInput.ValueChanged += delegate { ResetPreview(); };
            secondsInput.ValueChanged += delegate { ResetPreview(); };

            startPauseButton = CreateButton("开始", Color.FromArgb(73, 113, 205), new Point(84, 288), new Size(128, 44));
            startPauseButton.Click += StartPauseButton_Click;

            resetButton = CreateButton("重置", Color.FromArgb(48, 59, 87), new Point(228, 288), new Size(128, 44));
            resetButton.Click += delegate { ResetTimer(); };

            topMostCheckBox = new CheckBox
            {
                Text = "窗口置顶",
                ForeColor = Color.FromArgb(151, 166, 201),
                BackColor = BackColor,
                AutoSize = true,
                Location = new Point(337, 35)
            };
            topMostCheckBox.CheckedChanged += delegate { TopMost = topMostCheckBox.Checked; };

            tickTimer = new Timer { Interval = 250 };
            tickTimer.Tick += TickTimer_Tick;

            Controls.AddRange(new Control[]
            {
                titleLabel, subtitleLabel, topMostCheckBox, timeLabel,
                minutesLabel, secondsLabel, minutesInput, secondsInput,
                startPauseButton, resetButton
            });
        }

        private Label CreateCaption(string text, Point location)
        {
            return new Label
            {
                Text = text,
                ForeColor = Color.FromArgb(151, 166, 201),
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
                Font = new Font("Consolas", 14F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                Location = location,
                Size = new Size(118, 30),
                BackColor = Color.FromArgb(35, 45, 69),
                ForeColor = Color.White
            };
        }

        private Button CreateButton(string text, Color color, Point location, Size size)
        {
            var button = new Button
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
            return button;
        }

        private void StartPauseButton_Click(object? sender, EventArgs e)
        {
            if (isRunning)
            {
                PauseTimer();
                return;
            }

            if (remainingSeconds == 0)
            {
                remainingSeconds = GetInputSeconds();
            }

            if (remainingSeconds == 0)
            {
                MessageBox.Show("请设置大于 0 的计时时长。", "专注计时器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isRunning = true;
            endTime = DateTime.UtcNow.AddSeconds(remainingSeconds);
            tickTimer.Start();
            startPauseButton.Text = "暂停";
            minutesInput.Enabled = false;
            secondsInput.Enabled = false;
        }

        private void PauseTimer()
        {
            remainingSeconds = Math.Max(0, (int)Math.Ceiling((endTime - DateTime.UtcNow).TotalSeconds));
            isRunning = false;
            tickTimer.Stop();
            startPauseButton.Text = "继续";
            UpdateTimeDisplay();
        }

        private void TickTimer_Tick(object? sender, EventArgs e)
        {
            remainingSeconds = Math.Max(0, (int)Math.Ceiling((endTime - DateTime.UtcNow).TotalSeconds));
            UpdateTimeDisplay();

            if (remainingSeconds > 0)
            {
                return;
            }

            tickTimer.Stop();
            isRunning = false;
            startPauseButton.Text = "开始";
            minutesInput.Enabled = true;
            secondsInput.Enabled = true;
            SystemSounds.Exclamation.Play();
            MessageBox.Show("时间到，休息一下吧。", "专注计时器", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ResetTimer()
        {
            tickTimer.Stop();
            isRunning = false;
            minutesInput.Enabled = true;
            secondsInput.Enabled = true;
            startPauseButton.Text = "开始";
            remainingSeconds = GetInputSeconds();
            UpdateTimeDisplay();
        }

        private void ResetPreview()
        {
            if (!isRunning)
            {
                remainingSeconds = GetInputSeconds();
                UpdateTimeDisplay();
            }
        }

        private int GetInputSeconds()
        {
            return (int)minutesInput.Value * 60 + (int)secondsInput.Value;
        }

        private void UpdateTimeDisplay()
        {
            var minutes = remainingSeconds / 60;
            var seconds = remainingSeconds % 60;
            timeLabel.Text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
}
