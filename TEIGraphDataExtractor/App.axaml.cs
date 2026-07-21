using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TEIGraphDataExtractor.Services;
using TEIGraphDataExtractor.Services.Export;
using TEIGraphDataExtractor.Utils;
using TEIGraphDataExtractor.ViewModels;
using TEIGraphDataExtractor.Views;

namespace TEIGraphDataExtractor;

public partial class App : Application
{
    public static ServiceProvider? Services {get; private set; }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();

        collection.AddSingleton<ICoordinateConverter, CoordinateConverter>();
        collection.AddSingleton<IGraphDataService, GraphDataService>();
        collection.AddSingleton<IExportStrategy, CsvExportStrategy>();
        collection.AddTransient<MainWindowViewModel>();

        Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}