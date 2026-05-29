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

    private readonly Random _random = new();
    private int _idleTimer;
    private int _inactivityTimer;
    private int _bounceDir;
    private int _idleIntervalMs = 3000;
    private int _sleepTimeoutMs = 20000;

    public void SetAnimator(SpriteAnimator animator) => Animator = animator;

    public void Update(int deltaMs, double screenWidth, double screenHeight, int petSize)
    {
        switch (StateMachine.CurrentState)
        {
            case PetState.Idle:
                UpdateIdle(deltaMs);
                break;
            case PetState.Walk:
                UpdateWalk(deltaMs, screenWidth, screenHeight, petSize);
                break;
            case PetState.Jump:
                UpdateTimedAction(deltaMs);
                break;
            case PetState.Stretch:
            case PetState.Interact:
                UpdateTimedAction(deltaMs);
                break;
            case PetState.Bounce:
                UpdateTimedAction(deltaMs);
                break;
            case PetState.Drag:
                break;
            case PetState.Sleep:
                UpdateSleep(deltaMs);
                break;
        }
    }

    private void UpdateIdle(int deltaMs)
    {
        _idleTimer += deltaMs;
        _inactivityTimer += deltaMs;

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
            if (_random.Next(4) == 0)
            {
                StateMachine.TransitionTo(PetState.Jump);
                if (Animator != null) { Animator.Loop = false; Animator.Play(0, null); }
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

    private void UpdateSleep(int deltaMs)
    {
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
