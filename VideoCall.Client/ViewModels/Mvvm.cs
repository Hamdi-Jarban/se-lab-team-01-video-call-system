using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace VideoCall.Client.ViewModels;

/// <summary>
/// ÇáßáÇÓ ÇáÃÓÇÓí áÌãíÚ ÇáÜ ViewModels.
/// íØÈŞ æÇÌåÉ INotifyPropertyChanged áÅÔÚÇÑ ÇáæÇÌåÉ (UI) ÈÊÛííÑ Şíã ÇáãÊÛíÑÇÊ.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // ÊÍÏíË ÇáãÊÛíÑ æÅØáÇŞ ÍÏË ÇáÊÛííÑ ÊáŞÇÆíÇğ áÊÊÍÏË ÇáæÇÌåÉ İæÑÇğ
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// ßáÇÓ áÊäİíĞ ÇáÃæÇãÑ ÛíÑ ÇáãÊÒÇãäÉ (Async/Await) æÑÈØåÇ ÈÃÒÑÇÑ ÇáæÇÌåÉ.
/// íãäÚ ÊßÑÇÑ ÇáÊäİíĞ (Double-click) ÃËäÇÁ Úãá ÇáÃãÑ.
/// </summary>
public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private int _running;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public event Action<Exception>? Failed;

    // ÇáÊÍŞŞ ãä ÅãßÇäíÉ ÇáÊäİíĞ (ÃáÇ íßæä ÇáÒÑ ãÚØáÇğ æÃáÇ íßæä ÇáÃãÑ ŞíÏ ÇáÊÔÛíá ÍÇáíÇğ)
    public bool CanExecute(object? parameter) => Volatile.Read(ref _running) == 0 && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;
        Interlocked.Exchange(ref _running, 1);
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await _execute();
        }
        catch (OperationCanceledException)
        {
            // ÊÌÇåá ÎØÃ ÇáÅáÛÇÁ ÇáØÈíÚí ÃËäÇÁ ÅÛáÇŞ ÇáÊØÈíŞ
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// ßáÇÓ áÊäİíĞ ÇáÃæÇãÑ ÇáÚÇÏíÉ ÇáãÊÒÇãäÉ æÑÈØåÇ ÈÇáæÇÌåÉ (ÈÏæä Task).
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    { _execute = execute; _canExecute = canExecute; }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}