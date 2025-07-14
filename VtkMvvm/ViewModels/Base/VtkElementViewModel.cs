using Kitware.VTK;

namespace VtkMvvm.ViewModels.Base;

/// <summary>
///     Abstract base ViewModel that can be put into RenderWindow control
/// </summary>
public abstract class VtkElementViewModel : ViewModelBase, IDisposable
{
    public bool IsDisposed { get; private set; }

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

    /// <summary>
    ///     Force RenderWindow to render the scene. Generally used when the mutatable pipeline component has been modified
    ///     externally.
    /// </summary>
    public void ForceRender() => OnModified();

    public event EventHandler<EventArgs>? Modified;

    /// <summary>
    /// Notify RenderWindow to render the scene
    /// </summary>
    protected void OnModified() => Modified?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~VtkElementViewModel() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;

        if (disposing)
        {
            Actor.RemoveAllObservers();
        }

        // delete native memory (works in both disposing=true/false paths)
        Actor.Dispose();
    }
}