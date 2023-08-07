using BlazorState;
using Cosmos.DataTransfer.Ui.Common;
using Cosmos.DataTransfer.Ui.MessageOutput;

namespace Cosmos.DataTransfer.App.Features.Execution;

public partial class ExecutionState : State<ExecutionState>
{
    private List<LogMessage> _logs = new();
    public IEnumerable<LogMessage>? Logs => _logs;
    public bool IsExecuting { get; private set; }
    public CancellationTokenSource? CurrentExecutionAction { get; private set; }

    public override void Initialize()
    {
        _logs = new List<LogMessage>();
        IsExecuting = false;
        CurrentExecutionAction = null;
    }
}
