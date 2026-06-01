using Stateless;

namespace BugPro;

public sealed class Bug
{
    public enum State
    {
        New,
        Registered,
        Investigating,
        WaitingForReporter,
        Scheduled,
        ReadyForQa,
        Done,
        Archived,
        Reopened,
        Declined,
        Duplicate,
        NeedMoreEvidence
    }

    public enum Trigger
    {
        Register,
        TakeToWork,
        AskReporter,
        ReporterAnswered,
        PutIntoBacklog,
        PullFromBacklog,
        SendToQa,
        CheckFix,
        Reopen,
        Archive,
        Decline,
        MarkDuplicate,
        RequestEvidence,
        ReturnToQueue
    }

    private readonly StateMachine<State, Trigger> _workflow;
    private readonly List<string> _history = [];
    private readonly StateMachine<State, Trigger>.TriggerWithParameters<bool> _checkFixTrigger;

    public Bug()
    {
        _workflow = new StateMachine<State, Trigger>(State.New);
        _checkFixTrigger = _workflow.SetTriggerParameters<bool>(Trigger.CheckFix);

        CreatedAt = DateTime.UtcNow;
        Summary = "Bug without title";
        Owner = "unassigned";
        History = _history.AsReadOnly();

        ConfigureWorkflow();
    }

    public string Summary { get; private set; }

    public string Owner { get; private set; }

    public DateTime CreatedAt { get; }

    public IReadOnlyList<string> History { get; }

    public State CurrentState => _workflow.State;

    public bool CanFire(Trigger trigger) => _workflow.CanFire(trigger);

    public bool IsFinalState =>
        CurrentState is State.Archived or State.Declined or State.Duplicate;

    public void Capture(string summary, string owner)
    {
        Summary = string.IsNullOrWhiteSpace(summary) ? "Bug without title" : summary.Trim();
        Owner = string.IsNullOrWhiteSpace(owner) ? "unassigned" : owner.Trim();
    }

    public void Register() => Fire(Trigger.Register);

    public void TakeToWork() => Fire(Trigger.TakeToWork);

    public void AskReporter() => Fire(Trigger.AskReporter);

    public void ReporterAnswered() => Fire(Trigger.ReporterAnswered);

    public void PutIntoBacklog() => Fire(Trigger.PutIntoBacklog);

    public void PullFromBacklog() => Fire(Trigger.PullFromBacklog);

    public void SendToQa() => Fire(Trigger.SendToQa);

    public void CheckFix(bool isAcceptedByQa) => _workflow.Fire(_checkFixTrigger, isAcceptedByQa);

    public void Reopen() => Fire(Trigger.Reopen);

    public void Archive() => Fire(Trigger.Archive);

    public void Decline() => Fire(Trigger.Decline);

    public void MarkDuplicate() => Fire(Trigger.MarkDuplicate);

    public void RequestEvidence() => Fire(Trigger.RequestEvidence);

    public void ReturnToQueue() => Fire(Trigger.ReturnToQueue);

    public string DescribeAllowedActions()
    {
        var allowed = Enum.GetValues<Trigger>()
            .Where(CanFire)
            .Select(trigger => trigger.ToString())
            .ToArray();

        return allowed.Length == 0 ? "No actions available" : string.Join(", ", allowed);
    }

    public override string ToString() =>
        $"[{CurrentState}] {Summary} | owner: {Owner} | final: {IsFinalState} | transitions: {History.Count}";

    private void ConfigureWorkflow()
    {
        _workflow.OnTransitioned(transition => _history.Add(FormatTransition(transition)));

        ConfigureNewState();
        ConfigureRegisteredState();
        ConfigureInvestigatingState();
        ConfigureWaitingForReporterState();
        ConfigureScheduledState();
        ConfigureReadyForQaState();
        ConfigureTerminalStates();
        ConfigureReopenedState();
    }

    private void ConfigureNewState()
    {
        _workflow.Configure(State.New)
            .Permit(Trigger.Register, State.Registered);
    }

    private void ConfigureRegisteredState()
    {
        _workflow.Configure(State.Registered)
            .Permit(Trigger.TakeToWork, State.Investigating)
            .Permit(Trigger.AskReporter, State.WaitingForReporter)
            .Permit(Trigger.PutIntoBacklog, State.Scheduled)
            .Permit(Trigger.Decline, State.Declined)
            .Permit(Trigger.MarkDuplicate, State.Duplicate)
            .Permit(Trigger.RequestEvidence, State.NeedMoreEvidence);
    }

    private void ConfigureInvestigatingState()
    {
        _workflow.Configure(State.Investigating)
            .Permit(Trigger.AskReporter, State.WaitingForReporter)
            .Permit(Trigger.PutIntoBacklog, State.Scheduled)
            .Permit(Trigger.SendToQa, State.ReadyForQa);
    }

    private void ConfigureWaitingForReporterState()
    {
        _workflow.Configure(State.WaitingForReporter)
            .Permit(Trigger.ReporterAnswered, State.Registered)
            .Permit(Trigger.TakeToWork, State.Investigating);
    }

    private void ConfigureScheduledState()
    {
        _workflow.Configure(State.Scheduled)
            .Permit(Trigger.PullFromBacklog, State.Registered);
    }

    private void ConfigureReadyForQaState()
    {
        _workflow.Configure(State.ReadyForQa)
            .PermitIf(_checkFixTrigger, State.Done, isAcceptedByQa => isAcceptedByQa)
            .PermitIf(_checkFixTrigger, State.Reopened, isAcceptedByQa => !isAcceptedByQa)
            .Permit(Trigger.Reopen, State.Reopened);
    }

    private void ConfigureTerminalStates()
    {
        _workflow.Configure(State.NeedMoreEvidence)
            .Permit(Trigger.Archive, State.Archived)
            .Permit(Trigger.Reopen, State.Reopened);

        _workflow.Configure(State.Declined)
            .Permit(Trigger.Reopen, State.Reopened);

        _workflow.Configure(State.Duplicate)
            .Permit(Trigger.Reopen, State.Reopened);

        _workflow.Configure(State.Done)
            .Permit(Trigger.Archive, State.Archived)
            .Permit(Trigger.Reopen, State.Reopened);

        _workflow.Configure(State.Archived)
            .Permit(Trigger.Reopen, State.Reopened);
    }

    private void ConfigureReopenedState()
    {
        _workflow.Configure(State.Reopened)
            .Permit(Trigger.ReturnToQueue, State.Registered)
            .Permit(Trigger.TakeToWork, State.Investigating);
    }

    private static string FormatTransition(StateMachine<State, Trigger>.Transition transition) =>
        $"{transition.Source} --{transition.Trigger}--> {transition.Destination}";

    private void Fire(Trigger trigger) => _workflow.Fire(trigger);
}

public static class Program
{
    public static void Main()
    {
        var bug = new Bug();
        bug.Capture("Crash when user saves profile without avatar", "qa-team");

        Console.WriteLine("Workflow demo for a tracked bug");
        Console.WriteLine(bug);
        Console.WriteLine($"Allowed actions: {bug.DescribeAllowedActions()}");

        bug.Register();
        bug.TakeToWork();
        bug.SendToQa();
        bug.CheckFix(false);
        bug.ReturnToQueue();

        Console.WriteLine(bug);
        foreach (var item in bug.History)
        {
            Console.WriteLine(item);
        }
    }
}
