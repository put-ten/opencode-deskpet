using DeskPet.Behavior;

namespace DeskPet.Interaction;

public class DragHandler
{
    private readonly System.Windows.Window _window;
    private readonly PetBehavior _pet;
    private System.Windows.Point _dragStart;
    private bool _isDragging;

    public event Action? OnDropped;
    public event Action? OnDragStarted;

    public DragHandler(System.Windows.Window window, PetBehavior pet)
    {
        _window = window;
        _pet = pet;
        _window.MouseLeftButtonDown += OnMouseDown;
        _window.MouseMove += OnMouseMove;
        _window.MouseLeftButtonUp += OnMouseUp;
    }

    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(_window);
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        var pos = e.GetPosition(_window);
        var dx = pos.X - _dragStart.X;
        var dy = pos.Y - _dragStart.Y;
        if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
        {
                if (!_isDragging)
                {
                    _isDragging = true;
                    _pet.OnDragStart();
                    OnDragStarted?.Invoke();
                }
            _window.Left += dx;
            _window.Top += dy;
        }
    }

    private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _pet.OnDragEnd();
            OnDropped?.Invoke();
        }
        _isDragging = false;
    }

    public bool IsDragging => _isDragging;
}
