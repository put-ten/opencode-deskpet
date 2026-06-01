using System.Windows.Input;
using DeskPet.Behavior;

namespace DeskPet.Interaction;

public class ClickHandler
{
    private readonly System.Windows.Window _window;
    private readonly PetBehavior _pet;
    private readonly DragHandler? _drag;
    private bool _pendingSingleClick;
    private const int RapidClickWindowMs = 2000;
    private const int RapidClickThreshold = 3;
    private readonly Queue<DateTime> _clickHistory = new();

    public event Action? OnDoubleClickAction;
    public event Action? OnRapidClick;

    public ClickHandler(System.Windows.Window window, PetBehavior pet, DragHandler? drag = null)
    {
        _window = window;
        _pet = pet;
        _drag = drag;
        _window.MouseDown += OnMouseDown;
        _window.MouseDoubleClick += OnMouseDoubleClick;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        if (_drag != null && _drag.IsDragging) return;
        TrackRapidClicks(DateTime.UtcNow);
        if (e.ClickCount >= 2)
        {
            _pendingSingleClick = false;
            HandleDoubleClick();
            return;
        }
        _pendingSingleClick = true;
        var captured = true;
        Task.Delay(System.Windows.Forms.SystemInformation.DoubleClickTime + 50).ContinueWith(_ =>
        {
            if (!captured) return;
            if (!_pendingSingleClick) return;
            _pendingSingleClick = false;
            if (_drag == null || !_drag.IsDragging)
                _window.Dispatcher.Invoke(HandleSingleClick);
        });
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        _pendingSingleClick = false;
    }

    private void TrackRapidClicks(DateTime now)
    {
        _clickHistory.Enqueue(now);
        while (_clickHistory.Count > 0 && (now - _clickHistory.Peek()).TotalMilliseconds > RapidClickWindowMs)
            _clickHistory.Dequeue();
        if (_clickHistory.Count >= RapidClickThreshold)
        {
            _clickHistory.Clear();
            OnRapidClick?.Invoke();
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
