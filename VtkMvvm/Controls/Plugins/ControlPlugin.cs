using System.Windows;

namespace VtkMvvm.Controls.Plugins;

/// <summary>
/// VTK controls plugin
/// </summary>
public static class ControlPlugin
{
    // --- OrientationCube Attached Property ---
    public static readonly DependencyProperty OrientationCubeProperty =
        DependencyProperty.RegisterAttached(
            "OrientationCube",
            typeof(bool),
            typeof(ControlPlugin),
            new PropertyMetadata(false, OnOrientationCubeChanged));

    public static void SetOrientationCube(DependencyObject element, bool value) => element.SetValue(OrientationCubeProperty, value);

    public static bool GetOrientationCube(DependencyObject element) => (bool)element.GetValue(OrientationCubeProperty);

    private static void OnOrientationCubeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VtkObliqueImageSceneControl sceneControl)
        {
            if ((bool)e.NewValue) sceneControl.AddOrientationCube();
            else sceneControl.RemoveOrientationCube();
        }
    }

    // --- OrientationLabels Attached Property ---
    public static readonly DependencyProperty OrientationLabelsProperty =
        DependencyProperty.RegisterAttached(
            "OrientationLabels",
            typeof(bool),
            typeof(ControlPlugin),
            new PropertyMetadata(false, OnOrientationLabelsChanged));

    public static void SetOrientationLabels(DependencyObject element, bool value) => element.SetValue(OrientationLabelsProperty, value);

    public static bool GetOrientationLabels(DependencyObject element) => (bool)element.GetValue(OrientationLabelsProperty);

    private static void OnOrientationLabelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VtkObliqueImageSceneControl sceneControl)
        {
            if ((bool)e.NewValue) sceneControl.AddOrientationLabels();
            else sceneControl.RemoveOrientationLabels();
        }
    }
}