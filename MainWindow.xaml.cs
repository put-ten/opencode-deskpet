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
    private bool _facingRight;
    private double _cursorX;
    private double _cursorY;
    private readonly Dictionary<SpriteSheet, System.Windows.Media.Imaging.WriteableBitmap[]> _frameCache = new();
    private ParticleSystem? _particles;
    private readonly Queue<DateTime> _recentClicks = new();
    private bool _grumpyPlaying;

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
        _click.OnRapidClick += OnRapidClick;
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
            if (ai == null)
            {
                System.Windows.MessageBox.Show(
                    "找不到可用的 OpenCode 模型。\n请确认 ~/.local/share/opencode/auth.json 和 ~/.config/opencode/opencode.jsonc 存在并包含有效 key。\n或打开「设置」手动指定模型。",
                    "Pudding", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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
        _sheets[PetState.Drag] = _sheets[PetState.Idle];
        _sheet = _sheets[PetState.Idle];
        _animator.Sheet = _sheet;
        _animator.Play(0);
        _particles = new ParticleSystem(ParticleCanvas);
    }

    private SpriteSheet LoadSprite(string dir, string name)
    {
        var sheet = new SpriteSheet("cat", 48, 48);
        sheet.Load(System.IO.Path.Combine(dir, name));
        _frameCache[sheet] = _renderer.PreRender(sheet);
        return sheet;
    }

    private void ApplySettings()
    {
        var settings = Settings.Load();
        Opacity = settings.Window.Opacity;
        Topmost = settings.Window.AlwaysOnTop;
        _pet.IdleInterval = settings.Behavior.IdleInterval;
        OpacitySlider.Value = settings.Window.Opacity * 100;
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
        UpdateChaseTarget();
        _pet.CursorX = _cursorX;
        _pet.CursorY = _cursorY;
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
            Top = _taskbarY;
            _pet.Y = Top;
        }
        _pet.X = Left;
    }

    private void UpdateChaseTarget()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var source = PresentationSource.FromVisual(this);
        var scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        _cursorX = cursor.X / scaleX;
        _cursorY = cursor.Y / scaleY;
    }

    private void RenderFrame()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var source = PresentationSource.FromVisual(this);
        var scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var cursorX = cursor.X / scaleX;
        var catCenterX = Left + Width / 2.0;
        if (_pet.SpeedX > 0) _facingRight = true;
        else if (_pet.SpeedX < 0) _facingRight = false;
        else _facingRight = cursorX > catCenterX;
        PetImage.Source = _frameCache.TryGetValue(_sheet, out var frames) && _animator.CurrentFrame < frames.Length
            ? frames[_animator.CurrentFrame]
            : _renderer.RenderFrame(_sheet, _animator.CurrentFrame);
        PetImage.RenderTransform = _facingRight
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

    private void OnPetMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_particles == null) return;
        if (_pet.StateMachine.CurrentState == PetState.Sleep) return;
        _particles.EmitHearts(Width / 2.0, 64, 3);
    }

    private void OnPetMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_particles == null) return;
        if (_pet.StateMachine.CurrentState == PetState.Sleep) return;
        _particles.EmitQuestion(Width / 2.0, 64);
    }

    private void OnRapidClick()
    {
        if (_grumpyPlaying) return;
        if (_pet.StateMachine.CurrentState == PetState.Sleep) return;
        if (_particles != null) _particles.EmitHearts(Width / 2.0, 64, 5);
        _grumpyPlaying = true;
        ShowScaredFace();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _grumpyPlaying = false;
            _sheet = _sheets[PetState.Idle];
            _animator.Sheet = _sheet;
            _pet.StateMachine.TransitionTo(PetState.Idle);
            _animator.Loop = true;
            _animator.Play(0);
        };
        timer.Start();
    }
}
