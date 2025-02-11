using BlazorState;

namespace Cosmos.DataTransfer.App.Features;
public abstract class StateActionHandler<TState, TAction> : ActionHandler<TAction>
    where TAction : IAction
{
    protected StateActionHandler(IStore aStore) : base(aStore) { }
    protected TState State => Store.GetState<TState>();
}
