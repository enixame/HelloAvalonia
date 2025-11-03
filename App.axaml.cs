using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace HelloAvalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Composition Root - Configuration de toutes les dépendances
            var mainWindow = new MainWindow();
            
            // Configurer le ServiceLocator avec la fenêtre principale
            Services.ServiceLocator.Instance.ConfigureDialogService(mainWindow);
            
            // Créer et assigner le ViewModel
            mainWindow.DataContext = Services.ServiceLocator.Instance.CreateMainWindowViewModel();
            
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}