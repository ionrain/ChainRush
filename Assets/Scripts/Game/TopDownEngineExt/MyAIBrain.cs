using MoreMountains.Tools;

public class MyAIBrain : AIBrain {
    public delegate void AIBrainStateHandler();
    public AIBrainStateHandler OnStateChange;

    public override void TransitionToState(string newStateName)
    {
        if (CurrentState == null) {
            CurrentState = FindState(newStateName);
            if (CurrentState != null) {
                CurrentState.EnterState();
                OnStateChange?.Invoke();
            }
            return;
        }
        if (newStateName != CurrentState.StateName) {
            CurrentState.ExitState();
            OnExitState();

            CurrentState = FindState(newStateName);
            if (CurrentState != null) {
                CurrentState.EnterState();
                OnStateChange?.Invoke();
            }
        }
    }
}
