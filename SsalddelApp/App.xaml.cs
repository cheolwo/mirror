namespace SsalddelApp;

public partial class App : Application
{
    private readonly Services.NeighborhoodNativeMapBridge _mapBridge;
    public App(Services.NeighborhoodNativeMapBridge mapBridge)
	{
        _mapBridge = mapBridge;
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage(_mapBridge)) { Title = "살뜰" };
	}
}
