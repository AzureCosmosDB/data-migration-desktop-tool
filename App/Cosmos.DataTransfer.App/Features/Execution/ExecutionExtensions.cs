using Cosmos.DataTransfer.Ui.Common;
using Cosmos.DataTransfer.Ui.MessageOutput;
using MediatR;

namespace Cosmos.DataTransfer.App.Features.Execution;
public static class ExecutionExtensions
{
    public static void ThenReset(this Task task, IMediator mediator)
    {
        task.ContinueWith(t =>
        {
            mediator.Send(new ExecutionState.CancelExecutionAction(true));
        });
    }

    public static async void Log(this IMediator mediator, LogMessage message)
    {
        await mediator.Send(new ExecutionState.AddLogMessageAction(message));
    }
}
