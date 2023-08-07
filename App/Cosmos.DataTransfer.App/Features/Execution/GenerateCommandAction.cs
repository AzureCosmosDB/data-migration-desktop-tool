using BlazorState;
using Cosmos.DataTransfer.App.Features.Settings;
using Cosmos.DataTransfer.Ui;
using Cosmos.DataTransfer.Ui.Common;
using Cosmos.DataTransfer.Ui.MessageOutput;
using MediatR;

namespace Cosmos.DataTransfer.App.Features.Execution;

public partial class ExecutionState
{
    public record GenerateCommandAction(SettingsState Settings) : IAction;

    public class GenerateCommandHandler : StateActionHandler<ExecutionState, GenerateCommandAction>
    {
        private readonly IAppDataService _dataService;
        private readonly IMediator _mediator;
        public GenerateCommandHandler(IStore aStore, IAppDataService dataService, IMediator mediator) : base(aStore)
        {
            _dataService = dataService;
            _mediator = mediator;
        }

        public override async Task Handle(GenerateCommandAction action, CancellationToken cancellationToken)
        {
            State.CurrentExecutionAction = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            State.IsExecuting = true;

            Execute(action.Settings, State.CurrentExecutionAction.Token)
                .ThenReset(_mediator);
        }

        private async Task Execute(SettingsState settingsState, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var output = await _dataService.BuildCommandAsync(settingsState.SelectedSource ?? throw new InvalidOperationException("No Source selected"), 
                                       settingsState.SelectedSink ?? throw new InvalidOperationException("No Sink selected"), 
                                       settingsState.SourceSettings?.Settings,
                                       settingsState.SinkSettings?.Settings);
                _mediator.Log(LogMessage.Data(output));
            }
            catch (Exception ex)
            {
                _mediator.Log(LogMessage.Error(ex.Message));
            }
        }
    }
}
