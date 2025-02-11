using BlazorState;
using Cosmos.DataTransfer.Ui.Common;
using Cosmos.DataTransfer.Ui.MessageOutput;

namespace Cosmos.DataTransfer.App.Features.Execution;

public partial class ExecutionState
{
    public record AddLogMessageAction(LogMessage Message) : IAction;

    public class AddLogMessageHandler : StateActionHandler<ExecutionState, AddLogMessageAction>
    {
        public AddLogMessageHandler(IStore aStore) : base(aStore) { }

        public override async Task Handle(AddLogMessageAction action, CancellationToken cancellationToken)
        {
            State._logs.Add(action.Message);
        }
    }
}
