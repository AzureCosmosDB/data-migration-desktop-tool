using BlazorState;

namespace Cosmos.DataTransfer.App.Features.Execution;

public partial class ExecutionState
{
    public record CancelExecutionAction(bool Completed) : IAction;

    public class CancelExecutionHandler : StateActionHandler<ExecutionState, CancelExecutionAction>
    {
        public CancelExecutionHandler(IStore aStore) : base(aStore) { }

        public override async Task Handle(CancelExecutionAction action, CancellationToken cancellationToken)
        {
            if (!action.Completed)
            {
                State.CurrentExecutionAction?.Cancel();
            }
            State.CurrentExecutionAction = null;
            State.IsExecuting = false;
        }
    }
}
