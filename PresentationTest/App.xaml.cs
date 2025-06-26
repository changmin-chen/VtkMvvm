using System.Windows;

namespace PresentationTest;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : PrismApplication
{
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    protected override Window CreateShell() =>
        // return Container.Resolve<VtkMvvmTestWindow>();
        Container.Resolve<VtkObliqueSliceTestWindow>();
}