using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DeskPet.Behavior;
using DeskPet.Chat;
using DeskPet.Config;
using DeskPet.Engine;
using DeskPet.Interaction;
using DeskPet.Tray;

namespace DeskPet;

public partial class MainWindow : Window
{
    private readonly PetBehavior _pet = new();
    private readonly Dictionary<PetState, SpriteSheet> _sheets = new();
    private SpriteSheet _sheet;
    private readonly SpriteAnimator _animator;
    private readonly PixelRenderer _renderer = new(2);
    private readonly DragHandler _drag;
    private readonly ClickHandler _click;
    private readonly TrayIcon _tray;
    private readonly DispatcherTimer _timer = new();
    private DateTime _lastTick = DateTime.UtcNow;
    private double _screenWidth;
    private double _screenHeight;
    private double _taskbarY;
    private ChatWindow? _chatWindow;
    private SettingsWindow? _settingsWindow;
    private bool _isDocked;
    private bool _isDropping;

    public MainWindow()
    {
        InitializeComponent();
        _sheet = new SpriteSheet("cat", 48, 48);
        _animator = new SpriteAnimator(_sheet, 200, true);
        _pet.SetAnimator(_animator);
        _drag = new DragHandler(this, _pet);
        _drag.OnDragStarted += ShowScaredFace;
        _drag.OnDropped += OnPetDropped;
        _click = new ClickHandler(this, _pet, _drag);
        _tray = new TrayIcon(this);
        _click.OnDoubleClickAction += OpenChat;
        _pet.StateMachine.OnStateChanged += OnPetStateChanged;
        SetupWindow();
        _pet.SetPosition(Left, Top);
        SetupRendering();
        LoadSprites();
        ApplySettings();
    }

    private void OpenChat()
    {
        if (_chatWindow == null || !_chatWindow.IsVisible)
        {
            var ai = Config.Settings.ResolveAi(Config.Settings.Load().SelectedModel);
            _chatWindow = new ChatWindow(ai);
            _chatWindow.Closed += (_, _) => _chatWindow = null;
            _chatWindow.Show();
        }
        _chatWindow?.Activate();
    }

    private void ShowScaredFace()
    {
        if (_sheets.TryGetValue(PetState.Bounce, out var scared))
        {
            _sheet = scared;
            _animator.Sheet = scared;
            _animator.Loop = false;
            _animator.Play(0);
        }
    }

    private void OnPetDropped()
    {
        _isDropping = true;
        ShowScaredFace();
        var groundY = _taskbarY;
        var velocity = 0.0;
        var gravity = 2.5;
        var bounce = 0;

        var dropTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        dropTimer.Tick += (_, _) =>
        {
            velocity += gravity;
            Top += velocity;

            if (Top >= groundY)
            {
                Top = groundY;
                bounce++;
                if (bounce >= 3 || Math.Abs(velocity) < 1.5)
                {
                    Top = groundY;
                    _isDropping = false;
                    _pet.StateMachine.TransitionTo(PetState.Idle);
                    _animator.Play(0);
                    dropTimer.Stop();
                    return;
                }
                velocity = -velocity * 0.4;
            }
        };
        dropTimer.Start();
    }

    private void SetupWindow()
    {
        _screenWidth = SystemParameters.PrimaryScreenWidth;
        _screenHeight = SystemParameters.PrimaryScreenHeight;
        var workArea = SystemParameters.WorkArea;
        _taskbarY = workArea.Bottom - Height;
        Left = _screenWidth - Width;
        Top = _taskbarY;

        Loaded += (_, _) =>
        {
            _taskbarY = SystemParameters.WorkArea.Bottom - ActualHeight;
            Top = _taskbarY;
        };
    }

    private void SetupRendering()
    {
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void LoadSprites()
    {
        var dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sprites");
        _sheets[PetState.Idle] = LoadSprite(dir, "cat_idle.png");
        _sheets[PetState.Walk] = LoadSprite(dir, "cat_walk.png");
        _sheets[PetState.Stretch] = LoadSprite(dir, "cat_stretch.png");
        _sheets[PetState.Sleep] = LoadSprite(dir, "cat_sleep.png");
        _sheets[PetState.Bounce] = LoadSprite(dir, "cat_bounce.png");
        _sheets[PetState.Interact] = _sheets[PetState.Stretch];
        _sheets[PetState.Jump] = LoadSprite(dir, "cat_jump.png");
        _sheets[PetState.Drag] = _sheets[PetState.Idle];
        _sheets[PetState.Chat] = _sheets[PetState.Idle];
        _sheet = _sheets[PetState.Idle];
        _animator.Sheet = _sheet;
        _animator.Play(0);
    }

    private static SpriteSheet LoadSprite(string dir, string name)
    {
        var sheet = new SpriteSheet("cat", 48, 48);
        sheet.Load(System.IO.Path.Combine(dir, name));
        return sheet;
    }

    private void ApplySettings()
    {
        var settings = Settings.Load();
        Opacity = settings.Window.Opacity;
        Topmost = settings.Window.AlwaysOnTop;
        _pet.IdleInterval = settings.Behavior.IdleInterval;
    }

    private void OpenSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsVisible)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        _settingsWindow?.Activate();
    }

    private void OnPetStateChanged(PetState oldState, PetState newState)
    {
        if (_sheets.TryGetValue(newState, out var newSheet) && newSheet != _animator.Sheet)
        {
            _animator.Sheet = newSheet;
            _animator.CurrentFrame = 0;
            _sheet = newSheet;
            var isLooped = newState is PetState.Idle or PetState.Walk or PetState.Sleep;
            if (isLooped != _animator.Loop)
                _animator.Loop = isLooped;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        try
        {
            var now = DateTime.UtcNow;
            var delta = (int)(now - _lastTick).TotalMilliseconds;
            _lastTick = now;
            if (delta > 100) delta = 16;

            _animator.Update(delta);
            _pet.Update(delta, _screenWidth, _screenHeight, (int)Width);

            UpdatePosition();
            RenderFrame();
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "deskpet_crash.log"),
                $"Tick crash: {ex.Message}\n{ex.StackTrace}"
            );
        }
    }

    private void UpdatePosition()
    {
        Left += _pet.SpeedX;
        if (!_drag.IsDragging && !_isDropping)
        {
            var jumpOff = _pet.StateMachine.CurrentState == PetState.Jump
                ? JumpOffset(_animator.CurrentFrame)
                : 0;
            Top = _taskbarY - jumpOff;
            _pet.Y = Top;
        }
        _pet.X = Left;
    }

    private static double JumpOffset(int frame)
    {
        // Frames 0-4: going up, max height at frame 4
        // Frames 5-9: coming down, back to 0 at frame 9
        var t = frame / 9.0;
        return Math.Sin(t * Math.PI) * 60;
    }

    private void RenderFrame()
    {
        var frame = _renderer.RenderFrame(_sheet, _animator.CurrentFrame);
        PetImage.Source = frame;
        var flip = _pet.SpeedX > 0;
        if (_pet.StateMachine.CurrentState == PetState.Bounce)
            flip = _pet.X > _screenWidth * 0.5;
        PetImage.RenderTransform = flip
            ? new System.Windows.Media.ScaleTransform { ScaleX = -1, CenterX = 48 }
            : null;
    }

    private void OnMenuModeToggle(object sender, RoutedEventArgs e)
    {
        _isDocked = !_isDocked;
        _pet.Docked = _isDocked;
        if (_isDocked) { _pet.SpeedX = 0; _pet.SpeedY = 0; }
        MenuMode.Header = _isDocked ? "模式: 停靠" : "模式: 自由漫游";
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = OpacitySlider.Value / 100.0;
    }

    private void OnMenuSettings(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void OnMenuExit(object sender, RoutedEventArgs e)
    {
        _tray.Dispose();
        Close();
    }
}
