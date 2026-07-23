using System.Diagnostics;
using System.IO;
using System.Text.Json;
using WindowsToolbox.Modules.Shutdown.Models;

namespace WindowsToolbox.Modules.Shutdown.Services;

/// <summary>封装 Windows shutdown.exe，并负责持久化本应用创建的计划。</summary>
public sealed class ShutdownService : IShutdownService
{
    private readonly string _stateDirectory;
    private readonly string _stateFile;
    private readonly string _legacyStateFile;

    public DateTime? ScheduledTime { get; private set; }

    public ShutdownService()
    {
        _stateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsToolbox");
        _stateFile = Path.Combine(_stateDirectory, "shutdown-plan.json");
        _legacyStateFile = Path.Combine(_stateDirectory, "shutdown-time.txt");
        LoadState();
    }

    public ShutdownOperationResult ValidateShutdownTime(DateTime shutdownTime)
    {
        TimeSpan remaining = shutdownTime - DateTime.Now;
        if (remaining.TotalSeconds < 60)
            return ShutdownOperationResult.Failure(ShutdownError.InvalidTime, "请选择至少 1 分钟后的时间。");

        if (remaining.TotalDays > 3650)
            return ShutdownOperationResult.Failure(ShutdownError.InvalidTime, "关机时间不能超过 10 年。");

        return ShutdownOperationResult.Success("时间有效。", shutdownTime);
    }

    public async Task<ShutdownOperationResult> ScheduleShutdownAsync(
        DateTime shutdownTime,
        CancellationToken cancellationToken = default)
    {
        ShutdownOperationResult validation = ValidateShutdownTime(shutdownTime);
        if (!validation.IsSuccess)
            return validation;

        if (ScheduledTime is DateTime existing && existing > DateTime.Now)
        {
            return ShutdownOperationResult.Failure(
                ShutdownError.PlanAlreadyExists,
                $"已有一个关机计划（{existing:yyyy年MM月dd日 HH:mm}）。请先取消后再重新设置。");
        }

        long seconds = Math.Max(60, (long)Math.Floor((shutdownTime - DateTime.Now).TotalSeconds));
        CommandResult command = await RunShutdownCommandAsync(
            ["/s", "/t", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            cancellationToken).ConfigureAwait(false);

        if (!command.IsSuccess)
            return MapCommandFailure(command, isCancel: false);

        ScheduledTime = DateTime.Now.AddSeconds(seconds);
        await SaveStateAsync(cancellationToken).ConfigureAwait(false);
        return ShutdownOperationResult.Success("关机计划已设置。关闭软件不会取消该计划。", ScheduledTime);
    }

    public async Task<ShutdownOperationResult> CancelShutdownAsync(CancellationToken cancellationToken = default)
    {
        CommandResult command = await RunShutdownCommandAsync(["/a"], cancellationToken).ConfigureAwait(false);
        if (!command.IsSuccess)
        {
            if (ScheduledTime is null || ScheduledTime <= DateTime.Now)
            {
                ClearState();
                return ShutdownOperationResult.Failure(
                    ShutdownError.NoActivePlan,
                    "当前没有可取消的关机计划。");
            }

            return MapCommandFailure(command, isCancel: true);
        }

        ClearState();
        return ShutdownOperationResult.Success("已取消 Windows 关机计划。");
    }

    public TimeSpan? GetRemainingTime()
    {
        if (ScheduledTime is not DateTime target)
            return null;

        TimeSpan remaining = target - DateTime.Now;
        if (remaining <= TimeSpan.Zero)
        {
            ClearState();
            return null;
        }

        return remaining;
    }

    private static async Task<CommandResult> RunShutdownCommandAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            string executable = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "shutdown.exe");

            ProcessStartInfo startInfo = new()
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using Process? process = Process.Start(startInfo);
            if (process is null)
                return new CommandResult(false, -1, string.Empty);

            string standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            string standardError = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new CommandResult(
                process.ExitCode == 0,
                process.ExitCode,
                string.Join(Environment.NewLine, standardOutput, standardError).Trim());
        }
        catch (UnauthorizedAccessException)
        {
            return new CommandResult(false, 5, "Access denied");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception ||
            exception is IOException ||
            exception is InvalidOperationException)
        {
            return new CommandResult(false, -1, exception.Message);
        }
    }

    private static ShutdownOperationResult MapCommandFailure(CommandResult command, bool isCancel)
    {
        if (command.ExitCode == 5)
        {
            return ShutdownOperationResult.Failure(
                ShutdownError.PermissionDenied,
                "Windows 拒绝了该操作。请检查当前账户权限后重试。");
        }

        if (isCancel)
        {
            return ShutdownOperationResult.Failure(
                ShutdownError.NoActivePlan,
                "未能取消关机计划。当前可能没有待执行的计划。");
        }

        return ShutdownOperationResult.Failure(
            ShutdownError.CommandFailed,
            "Windows 未能创建关机计划。若已有计划，请先取消后再重试。");
    }

    private void LoadState()
    {
        try
        {
            if (File.Exists(_stateFile))
            {
                ShutdownPlanState? state = JsonSerializer.Deserialize<ShutdownPlanState>(File.ReadAllText(_stateFile));
                if (state?.ScheduledTime > DateTime.Now)
                    ScheduledTime = state.ScheduledTime;
                else
                    ClearState();
                return;
            }

            // 迁移 v1 WinForms 保存的 ticks 状态文件。
            if (File.Exists(_legacyStateFile) &&
                long.TryParse(File.ReadAllText(_legacyStateFile), out long ticks))
            {
                DateTime legacyTime = new(ticks, DateTimeKind.Local);
                if (legacyTime > DateTime.Now)
                    ScheduledTime = legacyTime;
            }
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is JsonException ||
            exception is ArgumentOutOfRangeException)
        {
            ScheduledTime = null;
        }
    }

    private async Task SaveStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_stateDirectory);
            await using FileStream stream = File.Create(_stateFile);
            await JsonSerializer.SerializeAsync(
                stream,
                new ShutdownPlanState { ScheduledTime = ScheduledTime!.Value },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException)
        {
            // shutdown.exe 已成功接受计划；状态保存失败不能反向撤销系统计划。
        }
    }

    private void ClearState()
    {
        ScheduledTime = null;
        try
        {
            if (File.Exists(_stateFile))
                File.Delete(_stateFile);
            if (File.Exists(_legacyStateFile))
                File.Delete(_legacyStateFile);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException)
        {
            // 状态文件仅用于界面显示，不影响 shutdown.exe 的执行结果。
        }
    }

    private sealed record CommandResult(bool IsSuccess, int ExitCode, string Output);
}
