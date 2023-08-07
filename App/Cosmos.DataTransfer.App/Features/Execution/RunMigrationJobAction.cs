using BlazorState;
using Cosmos.DataTransfer.App.Features.Settings;
using Cosmos.DataTransfer.Ui;
using Cosmos.DataTransfer.Ui.Common;
using Cosmos.DataTransfer.Ui.MessageOutput;
using MediatR;

namespace Cosmos.DataTransfer.App.Features.Execution;

public partial class ExecutionState
{
    public record RunMigrationJobAction(SettingsState Settings) : IAction;

    public class RunMigrationJobHandler : StateActionHandler<ExecutionState, RunMigrationJobAction>
    {
        private readonly IMediator _mediator;
        private readonly IAppDataService _dataService;
        public RunMigrationJobHandler(IStore aStore, IAppDataService dataService, IMediator mediator) : base(aStore)
        {
            _dataService = dataService;
            _mediator = mediator;
        }

        public override async Task Handle(RunMigrationJobAction action, CancellationToken cancellationToken)
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
                bool completed = await _dataService.ExecuteWithSettingsAsync(settingsState.SelectedSource ?? throw new InvalidOperationException("No Source selected"),
                    settingsState.SelectedSink ?? throw new InvalidOperationException("No Sink selected"),
                    settingsState.SourceSettings?.Settings,
                    settingsState.SinkSettings?.Settings,
                    async m => _mediator.Log(m),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _mediator.Log(LogMessage.Error(ex.Message));
            }
        }
    }
}
