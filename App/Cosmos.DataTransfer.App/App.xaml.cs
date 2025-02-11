
namespace Cosmos.DataTransfer.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new MainPage();
	}

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        window.Title = "Azure Cosmos DB Desktop Data Migration Tool";
        var windowState = new SavedWindowState("Main");
        
        if (windowState.X != null) window.X = windowState.X.Value;
        if (windowState.Y != null) window.Y = windowState.Y.Value;
        if (windowState.Width != null) window.Width = windowState.Width.Value;
        if (windowState.Height != null) window.Height = windowState.Height.Value;

        window.SizeChanged += (_, _) => { new SavedWindowState("Main", window).Save(); };

        return window;
    }
}
