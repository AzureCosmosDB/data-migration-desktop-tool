using Cosmos.DataTransfer.Ui.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Cosmos.DataTransfer.Ui.MessageOutput;

public partial class MessageLog
{
    [Inject]
    public IJSRuntime JS { get; set; } = null!;

    [Parameter]
    public IEnumerable<LogMessage>? Messages { get; set; }

    private ElementReference _scrollAreaRef;

    private IJSObjectReference? _module;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Cosmos.DataTransfer.Ui/MessageOutput/MessageLog.razor.js");
        await base.OnAfterRenderAsync(firstRender);
    }

    protected override Task OnParametersSetAsync()
    {
        if (_module != null)
        {
            Task.Delay(50).ContinueWith(async t =>
            {
                await _module.InvokeVoidAsync("scrollToEnd", new object[] { _scrollAreaRef });
            });
        }

        return base.OnParametersSetAsync();
    }
}
