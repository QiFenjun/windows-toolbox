using WindowsToolbox.Modules.KeepAwake.Models;

namespace WindowsToolbox.Modules.KeepAwake.Services;

public interface IExecutionStatePlatform
{
    ExecutionStateResult Set(ExecutionState flags);
}
