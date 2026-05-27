namespace DeskPet.Engine;

public enum PetState
{
    Idle,
    Walk,
    Jump,
    Stretch,
    Sleep,
    Interact,
    Drag,
    Bounce,
    Chat
}

public class StateMachine
{
    public PetState CurrentState { get; private set; } = PetState.Idle;
    public PetState PreviousState { get; private set; } = PetState.Idle;
    public event Action<PetState, PetState>? OnStateChanged;

    public void TransitionTo(PetState newState)
    {
        if (newState == CurrentState) return;
        PreviousState = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(PreviousState, CurrentState);
    }

    public void RevertToPrevious()
    {
        TransitionTo(PreviousState);
    }
}
