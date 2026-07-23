using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WindowsToolbox
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Color BackgroundColor = Color.FromArgb(244, 247, 251);
        private readonly Color CardColor = Color.White;
        private readonly Color PrimaryColor = Color.FromArgb(46, 103, 234);
        private readonly Color DangerColor = Color.FromArgb(220, 65, 74);
        private readonly Color TextColor = Color.FromArgb(27, 35, 49);
        private readonly Color MutedColor = Color.FromArgb(101, 112, 133);

        private DateTimePicker shutdownTimePicker;
        private Label statusTitle;
        private Label statusDetail;
        private Label countdownLabel;
        private Panel statusDot;
        private Button scheduleButton;
        private Button cancelButton;
        private Timer countdownTimer;
        private DateTime? scheduledTime;

        private string StateDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsToolbox"); }
        }

        private string StateFile
        {
            get { return Path.Combine(StateDirectory, "shutdown-time.txt"); }
        }

        public MainForm()
        {
            Text = "Windows工具箱";
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = BackgroundColor;
            ForeColor = TextColor;
            ClientSize = new Size(560, 610);
            MinimumSize = new Size(576, 649);
            MaximumSize = new Size(720, 720);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Icon = SystemIcons.Application;

            BuildInterface();
            LoadSavedState();

            countdownTimer = new Timer();
            countdownTimer.Interval = 1000;
            countdownTimer.Tick += delegate { RefreshStatus(); };
            countdownTimer.Start();
        }

        private void BuildInterface()
        {
            Panel header = new Panel
            {
                BackColor = Color.FromArgb(31, 45, 74),
                Dock = DockStyle.Top,
                Height = 116
            };
            Controls.Add(header);

            Label appTitle = new Label
            {
                Text = "Windows工具箱",
                Font = new Font("Microsoft YaHei UI", 19F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, 23)
            };
            header.Controls.Add(appTitle);

            Label appSubtitle = new Label
            {
                Text = "定时关机 · 简单、清楚、可随时取消",
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = Color.FromArgb(190, 203, 228),
                AutoSize = true,
                Location = new Point(32, 68)
            };
            header.Controls.Add(appSubtitle);

            Panel statusCard = CreateCard(new Point(24, 136), new Size(512, 102));
            Controls.Add(statusCard);

            statusDot = new Panel
            {
                BackColor = Color.FromArgb(156, 166, 185),
                Location = new Point(22, 22),
                Size = new Size(10, 10)
            };
            statusCard.Controls.Add(statusDot);

            statusTitle = new Label
            {
                Text = "当前没有关机计划",
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = TextColor,
                AutoSize = true,
                Location = new Point(43, 17)
            };
            statusCard.Controls.Add(statusTitle);

            statusDetail = new Label
            {
                Text = "设置后，即使关闭本软件，Windows 仍会按时关机。",
                ForeColor = MutedColor,
                AutoSize = true,
                Location = new Point(22, 50)
            };
            statusCard.Controls.Add(statusDetail);

            countdownLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = PrimaryColor,
                AutoSize = true,
                Location = new Point(22, 73)
            };
            statusCard.Controls.Add(countdownLabel);

            Panel settingCard = CreateCard(new Point(24, 254), new Size(512, 264));
            Controls.Add(settingCard);

            Label settingTitle = new Label
            {
                Text = "选择关机时间",
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = TextColor,
                AutoSize = true,
                Location = new Point(22, 20)
            };
            settingCard.Controls.Add(settingTitle);

            shutdownTimePicker = new DateTimePicker
            {
                Font = new Font("Microsoft YaHei UI", 12F),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy年MM月dd日  HH:mm",
                Location = new Point(22, 57),
                Size = new Size(468, 36),
                Value = GetRoundedFutureTime(60)
            };
            settingCard.Controls.Add(shutdownTimePicker);

            Label quickLabel = new Label
            {
                Text = "快捷设置",
                ForeColor = MutedColor,
                AutoSize = true,
                Location = new Point(22, 108)
            };
            settingCard.Controls.Add(quickLabel);

            FlowLayoutPanel quickPanel = new FlowLayoutPanel
            {
                Location = new Point(18, 133),
                Size = new Size(478, 40),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            settingCard.Controls.Add(quickPanel);
            quickPanel.Controls.Add(CreateQuickButton("30 分钟后", 30));
            quickPanel.Controls.Add(CreateQuickButton("1 小时后", 60));
            quickPanel.Controls.Add(CreateQuickButton("2 小时后", 120));
            quickPanel.Controls.Add(CreateTonightButton());

            scheduleButton = new Button
            {
                Text = "安排关机",
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = PrimaryColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Location = new Point(22, 195),
                Size = new Size(300, 46)
            };
            scheduleButton.FlatAppearance.BorderSize = 0;
            scheduleButton.Click += ScheduleButton_Click;
            settingCard.Controls.Add(scheduleButton);

            cancelButton = new Button
            {
                Text = "取消关机",
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = DangerColor,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Location = new Point(334, 195),
                Size = new Size(156, 46)
            };
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(238, 176, 180);
            cancelButton.FlatAppearance.BorderSize = 1;
            cancelButton.Click += CancelButton_Click;
            settingCard.Controls.Add(cancelButton);

            Label tip = new Label
            {
                Text = "提示：Windows 会在关机前弹出系统通知，请提前保存正在编辑的文件。",
                ForeColor = MutedColor,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(27, 535),
                Size = new Size(505, 40)
            };
            Controls.Add(tip);

            Label version = new Label
            {
                Text = "Windows工具箱  v1.0",
                ForeColor = Color.FromArgb(145, 153, 169),
                AutoSize = true,
                Location = new Point(27, 584)
            };
            Controls.Add(version);
        }

        private Panel CreateCard(Point location, Size size)
        {
            return new Panel
            {
                BackColor = CardColor,
                Location = location,
                Size = size,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private Button CreateQuickButton(string text, int minutes)
        {
            Button button = CreateSmallButton(text, 106);
            button.Click += delegate
            {
                shutdownTimePicker.Value = GetRoundedFutureTime(minutes);
            };
            return button;
        }

        private Button CreateTonightButton()
        {
            Button button = CreateSmallButton("今晚 23:00", 118);
            button.Click += delegate
            {
                DateTime tonight = DateTime.Today.AddHours(23);
                if (tonight <= DateTime.Now.AddMinutes(1))
                    tonight = tonight.AddDays(1);
                shutdownTimePicker.Value = tonight;
            };
            return button;
        }

        private Button CreateSmallButton(string text, int width)
        {
            Button button = new Button
            {
                Text = text,
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = Color.FromArgb(66, 80, 110),
                BackColor = Color.FromArgb(246, 248, 252),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 0, 4, 0),
                Size = new Size(width, 36)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(218, 224, 235);
            return button;
        }

        private static DateTime GetRoundedFutureTime(int minutes)
        {
            DateTime value = DateTime.Now.AddMinutes(minutes);
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
        }

        private void ScheduleButton_Click(object sender, EventArgs e)
        {
            DateTime target = shutdownTimePicker.Value;
            TimeSpan remaining = target - DateTime.Now;

            if (remaining.TotalSeconds < 60)
            {
                ShowMessage("请选择至少 1 分钟后的时间。", "时间无效", MessageBoxIcon.Warning);
                return;
            }

            if (remaining.TotalDays > 3650)
            {
                ShowMessage("关机时间不能超过 10 年。", "时间无效", MessageBoxIcon.Warning);
                return;
            }

            string confirmation = string.Format(
                CultureInfo.CurrentCulture,
                "确定安排在以下时间关机吗？\r\n\r\n{0:yyyy年MM月dd日  HH:mm}\r\n\r\n请记得保存正在编辑的文件。",
                target);

            if (MessageBox.Show(this, confirmation, "确认定时关机",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.OK)
                return;

            long seconds = (long)Math.Floor(remaining.TotalSeconds);
            CommandResult result = RunShutdownCommand("/s /t " + seconds.ToString(CultureInfo.InvariantCulture));

            if (result.Success)
            {
                scheduledTime = DateTime.Now.AddSeconds(seconds);
                SaveState(scheduledTime.Value);
                RefreshStatus();
                ShowMessage("关机计划已设置成功。\r\n如需更改时间，请先点击“取消关机”。", "设置成功", MessageBoxIcon.Information);
            }
            else
            {
                ShowCommandError(result, "无法设置关机计划");
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            CommandResult result = RunShutdownCommand("/a");
            if (result.Success)
            {
                scheduledTime = null;
                DeleteState();
                RefreshStatus();
                ShowMessage("已取消 Windows 的关机计划。", "取消成功", MessageBoxIcon.Information);
            }
            else
            {
                scheduledTime = null;
                DeleteState();
                RefreshStatus();
                string detail = string.IsNullOrWhiteSpace(result.Output)
                    ? "当前可能没有可取消的关机计划。"
                    : result.Output.Trim();
                ShowMessage(detail, "未取消关机", MessageBoxIcon.Warning);
            }
        }

        private CommandResult RunShutdownCommand(string arguments)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe"),
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.Default,
                    StandardErrorEncoding = Encoding.Default
                };

                using (Process process = Process.Start(startInfo))
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return new CommandResult(process.ExitCode == 0, (stdout + "\r\n" + stderr).Trim());
                }
            }
            catch (Exception ex)
            {
                return new CommandResult(false, ex.Message);
            }
        }

        private void RefreshStatus()
        {
            if (!scheduledTime.HasValue)
            {
                statusDot.BackColor = Color.FromArgb(156, 166, 185);
                statusTitle.Text = "当前没有关机计划";
                statusDetail.Text = "设置后，即使关闭本软件，Windows 仍会按时关机。";
                countdownLabel.Text = "";
                return;
            }

            TimeSpan remaining = scheduledTime.Value - DateTime.Now;
            if (remaining.TotalSeconds <= 0)
            {
                scheduledTime = null;
                DeleteState();
                RefreshStatus();
                return;
            }

            statusDot.BackColor = Color.FromArgb(46, 180, 119);
            statusTitle.Text = "关机计划已生效";
            statusDetail.Text = string.Format("预计关机：{0:yyyy年MM月dd日  HH:mm}", scheduledTime.Value);
            countdownLabel.Text = "剩余 " + FormatRemainingTime(remaining);
        }

        private static string FormatRemainingTime(TimeSpan time)
        {
            if (time.TotalDays >= 1)
                return string.Format("{0} 天 {1:00} 小时 {2:00} 分", (int)time.TotalDays, time.Hours, time.Minutes);
            if (time.TotalHours >= 1)
                return string.Format("{0:00} 小时 {1:00} 分 {2:00} 秒", (int)time.TotalHours, time.Minutes, time.Seconds);
            return string.Format("{0:00} 分 {1:00} 秒", Math.Max(0, time.Minutes), Math.Max(0, time.Seconds));
        }

        private void LoadSavedState()
        {
            try
            {
                if (!File.Exists(StateFile))
                {
                    RefreshStatus();
                    return;
                }

                long ticks;
                if (long.TryParse(File.ReadAllText(StateFile), NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks))
                {
                    DateTime saved = new DateTime(ticks, DateTimeKind.Local);
                    if (saved > DateTime.Now)
                        scheduledTime = saved;
                    else
                        DeleteState();
                }
            }
            catch
            {
                scheduledTime = null;
            }
            RefreshStatus();
        }

        private void SaveState(DateTime value)
        {
            try
            {
                Directory.CreateDirectory(StateDirectory);
                File.WriteAllText(StateFile, value.Ticks.ToString(CultureInfo.InvariantCulture));
            }
            catch
            {
                // 关机命令已经生效；状态文件保存失败不影响系统关机。
            }
        }

        private void DeleteState()
        {
            try
            {
                if (File.Exists(StateFile))
                    File.Delete(StateFile);
            }
            catch
            {
                // 状态文件仅用于界面显示，不影响取消命令。
            }
        }

        private void ShowCommandError(CommandResult result, string title)
        {
            string message = string.IsNullOrWhiteSpace(result.Output)
                ? "Windows 未接受该命令。请检查系统权限后重试。"
                : result.Output.Trim();
            ShowMessage(message, title, MessageBoxIcon.Error);
        }

        private void ShowMessage(string message, string title, MessageBoxIcon icon)
        {
            MessageBox.Show(this, message, title, MessageBoxButtons.OK, icon);
        }

        private sealed class CommandResult
        {
            public bool Success { get; private set; }
            public string Output { get; private set; }

            public CommandResult(bool success, string output)
            {
                Success = success;
                Output = output ?? string.Empty;
            }
        }
    }
}
