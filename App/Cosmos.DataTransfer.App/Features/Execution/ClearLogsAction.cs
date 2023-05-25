using BlazorState;

namespace Cosmos.DataTransfer.App.Features.Execution;

public partial class ExecutionState
{
    public record ClearLogsAction() : IAction;

    public class ClearLogsHandler : StateActionHandler<ExecutionState, ClearLogsAction>
    {
        public ClearLogsHandler(IStore aStore) : base(aStore) { }

        public override async Task Handle(ClearLogsAction action, CancellationToken cancellationToken)
        {
            State._logs.Clear();
        }
    }
}
