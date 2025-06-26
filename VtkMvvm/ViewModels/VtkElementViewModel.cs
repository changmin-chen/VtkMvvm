using System.ComponentModel;
using System.Runtime.CompilerServices;
using Kitware.VTK;

namespace VtkMvvm.ViewModels;

/// <summary>
///     Abstract base ViewModel that can be put into RenderWindow control
/// </summary>
public abstract class VtkElementViewModel : INotifyPropertyChanged, IDisposable
{
    public abstract vtkProp Actor { get; }

    public bool Visible
    {
        get => Actor.GetVisibility() == 1;
        set
        {
            bool current = Actor.GetVisibility() == 1;
            if (current == value) return;
            Actor.SetVisibility(value ? 1 : 0);
            Actor.Modified();
            OnPropertyChanged();
            OnModified();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    public event PropertyChangedEventHandler? PropertyChanged;

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

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Actor.Dispose();
        }
    }


    #region Notify RenderWindow to render the scene

    public event EventHandler<EventArgs>? Modified;

    protected void OnModified()
    {
        Modified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Force RenderWindow to render the scene. Generally used when the mutatable pipeline component has been modified
    ///     externally.
    /// </summary>
    public void ForceRender() => OnModified();

    #endregion
}