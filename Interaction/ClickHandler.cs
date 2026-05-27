using System.Windows.Input;
using DeskPet.Behavior;

namespace DeskPet.Interaction;

public class ClickHandler
{
    private readonly System.Windows.Window _window;
    private readonly PetBehavior _pet;
    private readonly DragHandler? _drag;
    private DateTime _lastClick = DateTime.MinValue;
    private const int DoubleClickMs = 400;

    public event Action? OnDoubleClickAction;

    public ClickHandler(System.Windows.Window window, PetBehavior pet, DragHandler? drag = null)
    {
        _window = window;
        _pet = pet;
        _drag = drag;
        _window.MouseDown += OnMouseDown;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        if (_drag != null && _drag.IsDragging) return;
        var now = DateTime.UtcNow;
        if ((now - _lastClick).TotalMilliseconds <= DoubleClickMs)
        {
            HandleDoubleClick();
            _lastClick = DateTime.MinValue;
        }
        else
        {
            _lastClick = now;
            var captured = now;
            Task.Delay(DoubleClickMs + 50).ContinueWith(_ =>
            {
                if (_lastClick == captured && (_drag == null || !_drag.IsDragging))
                    _window.Dispatcher.Invoke(HandleSingleClick);
            });
        }
    }

    private void HandleSingleClick()
    {
        _pet.OnClick();
    }

    private void HandleDoubleClick()
    {
        _pet.WakeUp();
        OnDoubleClickAction?.Invoke();
    }
}
