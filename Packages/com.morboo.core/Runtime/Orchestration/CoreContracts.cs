public interface IStateReporter
{
    StateSnapshot ReportState();
}

public interface IIntentReceiver
{
    void ReceiveIntent(Intent intent);
}

public interface IInstructionReceiver
{
    void ReceiveInstruction(Instruction instruction);
}

public interface IInstructionProvider
{
    bool TryGetInstruction(out Instruction instruction);
}
