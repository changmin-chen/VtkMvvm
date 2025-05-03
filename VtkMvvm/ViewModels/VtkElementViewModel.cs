using System.ComponentModel;
using System.Runtime.CompilerServices;
using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

public abstract class VtkElementViewModel(vtkImageData image) : INotifyPropertyChanged, IDisposable
{
    public abstract vtkProp Actor { get; }
    public ImageModel ImageModel { get; } = ImageModel.Create(image);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;


    // Notify SceneControl to render the scene
    public event EventHandler<EventArgs>? Modified;

    protected void OnModified()
    {
        Modified?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    // Disposal
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Actor.Dispose();
        }
    }
}