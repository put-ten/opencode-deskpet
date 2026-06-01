using DeskPet.Engine;

namespace DeskPet.Behavior;

public class PetBehavior
{
    public StateMachine StateMachine { get; } = new();
    public SpriteAnimator? Animator { get; private set; }

    public double SpeedX { get; set; }
    public double SpeedY { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool Docked { get; set; }
    public int IdleInterval
    {
        get => _idleIntervalMs / 1000;
        set => _idleIntervalMs = value * 1000;
    }

    public double? ChaseTargetX { get; set; }
    public bool IsChasing => ChaseTargetX.HasValue;
    private int _chaseDetectTimer;
    private long _chaseCooldownUntilMs;
    public double CursorX { get; set; }
    public double CursorY { get; set; }

    private readonly Random _random = new();
    private int _idleTimer;
    private int _inactivityTimer;
    private int _bounceDir;
    private int _idleIntervalMs = 3000;
    private int _sleepTimeoutMs = 20000;

    public void SetAnimator(SpriteAnimator animator) => Animator = animator;

    private void GoTo(double targetX)
    {
        var catEdge = targetX > X + 48 ? X + 96 : X;
        var dist = targetX - catEdge;
        if (Math.Abs(dist) < 12)
        {
            ChaseTargetX = null;
            return;
        }
        ChaseTargetX = targetX;
        SpeedX = Math.Sign(dist) * (2.0 + _random.NextDouble() * 2.0);
        SpeedY = 0;
        StateMachine.TransitionTo(PetState.Walk);
        Animator?.Play(0);
    }

    private long _nowMs;
    public void Update(int deltaMs, double screenWidth, double screenHeight, int petSize)
    {
        _nowMs += deltaMs;
        switch (StateMachine.CurrentState)
        {
            case PetState.Idle:
                UpdateIdle(deltaMs);
                break;
            case PetState.Walk:
                UpdateWalk(deltaMs, screenWidth, screenHeight, petSize);
                break;
            case PetState.Stretch:
            case PetState.Interact:
                UpdateTimedAction(deltaMs);
                break;
            case PetState.Bounce:
                UpdateTimedAction(deltaMs);
                break;
            case PetState.Drag:
            case PetState.Sleep:
                break;
        }
    }

    private void UpdateIdle(int deltaMs)
    {
        _idleTimer += deltaMs;
        _inactivityTimer += deltaMs;

        // Cursor chase detection using CursorX/CursorY (set every frame by MainWindow)
        if (_nowMs >= _chaseCooldownUntilMs)
        {
            var dx = CursorX - X;
            var dy = CursorY - Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 220 && dist > 24)
            {
                _chaseDetectTimer += deltaMs;
                if (_chaseDetectTimer >= 500)
                {
                    _chaseDetectTimer = 0;
                    GoTo(CursorX);
                    return;
                }
            }
            else
            {
                _chaseDetectTimer = 0;
            }
        }
        else
        {
            _chaseDetectTimer = 0;
        }

        if (_inactivityTimer >= _sleepTimeoutMs)
        {
            StateMachine.TransitionTo(PetState.Sleep);
            Animator?.Play(0);
            return;
        }

        if (_idleTimer >= _idleIntervalMs)
        {
            _idleTimer = 0;
            if (_random.Next(3) == 0)
            {
                StateMachine.TransitionTo(PetState.Stretch);
                if (Animator != null) { Animator.Loop = false; Animator.Play(0, null); }
                return;
            }
            if (ChaseTargetX.HasValue)
            {
                GoTo(ChaseTargetX.Value);
                return;
            }
            if (Docked) return;
            var direction = _random.Next(2) == 0 ? -1 : 1;
            var speed = 1.0 + _random.NextDouble() * 1.5;
            SpeedX = direction * speed;
            SpeedY = 0;
            StateMachine.TransitionTo(PetState.Walk);
            Animator?.Play(0);
        }
    }

    private void UpdateWalk(int deltaMs, double screenWidth, double screenHeight, int petSize)
    {
        // Stop if at chase target
        if (ChaseTargetX.HasValue)
        {
            var catEdge = SpeedX > 0 ? X + 96 : X;
            if (Math.Abs(catEdge - ChaseTargetX.Value) < 12)
            {
                SpeedX = 0;
                ChaseTargetX = null;
                _chaseCooldownUntilMs = _nowMs + 1500;
                StateMachine.TransitionTo(PetState.Idle);
                Animator?.Play(0);
                return;
            }
        }

        X += SpeedX;
        Y += SpeedY;

        var margin = 0;
        var bounced = false;
        if (X < margin) { X = margin; bounced = true; _bounceDir = 1; }
        if (X + petSize > screenWidth - margin) { X = screenWidth - petSize - margin; bounced = true; _bounceDir = -1; }

        if (bounced)
        {
            SpeedX = 0;
            SpeedY = 0;
            StateMachine.TransitionTo(PetState.Bounce);
            if (Animator != null) { Animator.Loop = false; Animator.Play(0); }
            return;
        }

        _idleTimer += deltaMs;
        if (_idleTimer > _idleIntervalMs)
        {
            _idleTimer = 0;
            SpeedX = 0;
            SpeedY = 0;
            StateMachine.TransitionTo(PetState.Idle);
            Animator?.Play(0);
        }
    }

    private void UpdateTimedAction(int deltaMs)
    {
        if (Animator != null && !Animator.IsPlaying)
        {
            Animator.Loop = true;
            var next = PetState.Idle;
            if (StateMachine.PreviousState == PetState.Walk)
            {
                _idleTimer = 0;
                next = PetState.Walk;
                var speed = 1.0 + _random.NextDouble() * 1.5;
                SpeedX = _bounceDir * speed;
                SpeedY = 0;
                Animator?.Play(0);
            }
            else
            {
                SpeedX = 0;
                SpeedY = 0;
                Animator?.Play(0);
            }
            StateMachine.TransitionTo(next);
        }
    }

    public void OnClick()
    {
        _inactivityTimer = 0;
        StateMachine.TransitionTo(PetState.Interact);
        if (Animator != null) { Animator.Loop = false; Animator.Play(0); }
    }

    public void OnDragStart()
    {
        _inactivityTimer = 0;
        StateMachine.TransitionTo(PetState.Drag);
        Animator?.Pause();
    }

    public void OnDragEnd()
    {
        SpeedX = 0;
        SpeedY = 0;
        Animator?.Play(0);
        StateMachine.TransitionTo(PetState.Idle);
    }

    public void SetPosition(double x, double y)
    {
        X = x;
        Y = y;
    }

    public void WakeUp()
    {
        _inactivityTimer = 0;
        if (StateMachine.CurrentState == PetState.Sleep)
        {
            StateMachine.TransitionTo(PetState.Idle);
            Animator?.Play(0);
        }
    }
}
