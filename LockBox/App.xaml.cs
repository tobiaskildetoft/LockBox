using Microsoft.Extensions.DependencyInjection;

namespace LockBox
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            window.Width = 500;

            return window;
        }
    }
}