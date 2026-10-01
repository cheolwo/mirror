using Microsoft.Extensions.DependencyInjection;

namespace FDriverApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell()) { Title = "살뜰 배달" };
        }
    }
}
