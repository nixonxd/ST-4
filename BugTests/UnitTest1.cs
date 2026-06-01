using BugPro;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BugTests;

[TestClass]
public class BugStateMachineTests
{
    [TestMethod]
    public void FreshBugHasDefaultMetadata()
    {
        var bug = new Bug();

        Assert.AreEqual(Bug.State.New, bug.CurrentState);
        Assert.AreEqual("Bug without title", bug.Summary);
        Assert.AreEqual("unassigned", bug.Owner);
        Assert.AreEqual(0, bug.History.Count);
    }

    [TestMethod]
    public void CaptureNormalizesSummaryAndOwner()
    {
        var bug = new Bug();

        bug.Capture("  Lost focus after login  ", "  ivan.petrov  ");

        Assert.AreEqual("Lost focus after login", bug.Summary);
        Assert.AreEqual("ivan.petrov", bug.Owner);
    }

    [TestMethod]
    public void RegisterMovesBugFromNewToRegistered()
    {
        var bug = new Bug();

        bug.Register();

        Assert.AreEqual(Bug.State.Registered, bug.CurrentState);
    }

    [TestMethod]
    public void InvestigatingFlowCanBeStartedFromRegistered()
    {
        var bug = CreateRegisteredBug();

        bug.TakeToWork();

        Assert.AreEqual(Bug.State.Investigating, bug.CurrentState);
    }

    [TestMethod]
    public void AskingReporterMovesBugToWaitingForReporter()
    {
        var bug = CreateRegisteredBug();

        bug.AskReporter();

        Assert.AreEqual(Bug.State.WaitingForReporter, bug.CurrentState);
    }

    [TestMethod]
    public void ReporterAnswerReturnsBugToRegisteredQueue()
    {
        var bug = CreateRegisteredBug();
        bug.AskReporter();

        bug.ReporterAnswered();

        Assert.AreEqual(Bug.State.Registered, bug.CurrentState);
    }

    [TestMethod]
    public void BugCanBeSentToBacklogFromRegistered()
    {
        var bug = CreateRegisteredBug();

        bug.PutIntoBacklog();

        Assert.AreEqual(Bug.State.Scheduled, bug.CurrentState);
    }

    [TestMethod]
    public void PullFromBacklogReturnsBugToRegistered()
    {
        var bug = CreateScheduledBug();

        bug.PullFromBacklog();

        Assert.AreEqual(Bug.State.Registered, bug.CurrentState);
    }

    [TestMethod]
    public void DeclineMovesBugToDeclined()
    {
        var bug = CreateRegisteredBug();

        bug.Decline();

        Assert.AreEqual(Bug.State.Declined, bug.CurrentState);
    }

    [TestMethod]
    public void DuplicateBranchMarksBugAsFinal()
    {
        var bug = CreateRegisteredBug();

        bug.MarkDuplicate();

        Assert.AreEqual(Bug.State.Duplicate, bug.CurrentState);
        Assert.IsTrue(bug.IsFinalState);
    }

    [TestMethod]
    public void EvidenceRequestMovesBugToNeedMoreEvidence()
    {
        var bug = CreateRegisteredBug();

        bug.RequestEvidence();

        Assert.AreEqual(Bug.State.NeedMoreEvidence, bug.CurrentState);
        Assert.IsFalse(bug.IsFinalState);
    }

    [TestMethod]
    public void EvidenceBranchCanBeArchived()
    {
        var bug = CreateEvidenceRequestedBug();

        bug.Archive();

        Assert.AreEqual(Bug.State.Archived, bug.CurrentState);
        Assert.IsTrue(bug.IsFinalState);
    }

    [TestMethod]
    public void InvestigatingBugCanBeSentToQa()
    {
        var bug = CreateInvestigatingBug();

        bug.SendToQa();

        Assert.AreEqual(Bug.State.ReadyForQa, bug.CurrentState);
    }

    [TestMethod]
    public void SuccessfulQaCheckMovesBugToDone()
    {
        var bug = CreateReadyForQaBug();

        bug.CheckFix(true);

        Assert.AreEqual(Bug.State.Done, bug.CurrentState);
    }

    [TestMethod]
    public void FailedQaCheckMovesBugToReopened()
    {
        var bug = CreateReadyForQaBug();

        bug.CheckFix(false);

        Assert.AreEqual(Bug.State.Reopened, bug.CurrentState);
    }

    [TestMethod]
    public void DoneBugCanBeArchived()
    {
        var bug = CreateDoneBug();

        bug.Archive();

        Assert.AreEqual(Bug.State.Archived, bug.CurrentState);
    }

    [TestMethod]
    public void ReopenedBugCanBeReturnedToRegisteredQueue()
    {
        var bug = CreateReopenedBug();

        bug.ReturnToQueue();

        Assert.AreEqual(Bug.State.Registered, bug.CurrentState);
    }

    [TestMethod]
    public void ReopenedBugCanBeTakenDirectlyToWork()
    {
        var bug = CreateReopenedBug();

        bug.TakeToWork();

        Assert.AreEqual(Bug.State.Investigating, bug.CurrentState);
    }

    [TestMethod]
    public void ArchivedBugCanBeReopened()
    {
        var bug = CreateArchivedBug();

        bug.Reopen();

        Assert.AreEqual(Bug.State.Reopened, bug.CurrentState);
    }

    [TestMethod]
    public void DeclinedBugCanBeReopened()
    {
        var bug = CreateRegisteredBug();
        bug.Decline();

        bug.Reopen();

        Assert.AreEqual(Bug.State.Reopened, bug.CurrentState);
    }

    [TestMethod]
    public void DuplicateBugCanBeReopened()
    {
        var bug = CreateRegisteredBug();
        bug.MarkDuplicate();

        bug.Reopen();

        Assert.AreEqual(Bug.State.Reopened, bug.CurrentState);
    }

    [TestMethod]
    public void InvalidTriggerFromNewStateThrows()
    {
        var bug = new Bug();

        Assert.ThrowsException<InvalidOperationException>(() => bug.TakeToWork());
    }

    [TestMethod]
    public void InvalidQaTriggerFromInvestigatingThrows()
    {
        var bug = CreateInvestigatingBug();

        Assert.ThrowsException<InvalidOperationException>(() => bug.CheckFix(true));
    }

    [TestMethod]
    public void InvalidArchiveFromRegisteredThrows()
    {
        var bug = CreateRegisteredBug();

        Assert.ThrowsException<InvalidOperationException>(() => bug.Archive());
    }

    [TestMethod]
    public void ExceptionMessageContainsTriggerName()
    {
        var bug = new Bug();

        var error = Assert.ThrowsException<InvalidOperationException>(() => bug.TakeToWork());

        StringAssert.Contains(error.Message, "TakeToWork");
    }

    [TestMethod]
    public void ExceptionMessageContainsStateName()
    {
        var bug = CreateRegisteredBug();

        var error = Assert.ThrowsException<InvalidOperationException>(() => bug.Archive());

        StringAssert.Contains(error.Message, "Registered");
    }

    [TestMethod]
    public void CanFireReflectsCurrentAvailableActions()
    {
        var bug = CreateRegisteredBug();

        Assert.IsTrue(bug.CanFire(Bug.Trigger.TakeToWork));
        Assert.IsTrue(bug.CanFire(Bug.Trigger.PutIntoBacklog));
        Assert.IsFalse(bug.CanFire(Bug.Trigger.Archive));
    }

    [TestMethod]
    public void AllowedActionsDescriptionListsExpectedTriggers()
    {
        var bug = CreateRegisteredBug();

        var actions = bug.DescribeAllowedActions();

        StringAssert.Contains(actions, "TakeToWork");
        StringAssert.Contains(actions, "Decline");
        Assert.IsFalse(actions.Contains("Archive", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ArchivedStateStillExposesReopenAction()
    {
        var bug = CreateArchivedBug();

        var actions = bug.DescribeAllowedActions();

        StringAssert.Contains(actions, "Reopen");
    }

    [TestMethod]
    public void HistoryTracksBusinessFlowInOrder()
    {
        var bug = new Bug();
        bug.Register();
        bug.TakeToWork();
        bug.SendToQa();
        bug.CheckFix(true);

        Assert.AreEqual(4, bug.History.Count);
        Assert.AreEqual("New --Register--> Registered", bug.History[0]);
        Assert.AreEqual("ReadyForQa --CheckFix--> Done", bug.History[^1]);
    }

    [TestMethod]
    public void ToStringContainsSummaryOwnerAndState()
    {
        var bug = CreateReadyForQaBug();
        bug.Capture("Export fails on csv", "team-a");

        var text = bug.ToString();

        StringAssert.Contains(text, "Export fails on csv");
        StringAssert.Contains(text, "team-a");
        StringAssert.Contains(text, "ReadyForQa");
    }

    [TestMethod]
    public void CreatedAtIsInitialized()
    {
        var before = DateTime.UtcNow.AddSeconds(-2);
        var bug = new Bug();
        var after = DateTime.UtcNow.AddSeconds(2);

        Assert.IsTrue(bug.CreatedAt >= before);
        Assert.IsTrue(bug.CreatedAt <= after);
    }

    private static Bug CreateRegisteredBug()
    {
        var bug = new Bug();
        bug.Register();
        return bug;
    }

    private static Bug CreateInvestigatingBug()
    {
        var bug = CreateRegisteredBug();
        bug.TakeToWork();
        return bug;
    }

    private static Bug CreateScheduledBug()
    {
        var bug = CreateRegisteredBug();
        bug.PutIntoBacklog();
        return bug;
    }

    private static Bug CreateReadyForQaBug()
    {
        var bug = CreateInvestigatingBug();
        bug.SendToQa();
        return bug;
    }

    private static Bug CreateDoneBug()
    {
        var bug = CreateReadyForQaBug();
        bug.CheckFix(true);
        return bug;
    }

    private static Bug CreateReopenedBug()
    {
        var bug = CreateReadyForQaBug();
        bug.CheckFix(false);
        return bug;
    }

    private static Bug CreateArchivedBug()
    {
        var bug = CreateDoneBug();
        bug.Archive();
        return bug;
    }

    private static Bug CreateEvidenceRequestedBug()
    {
        var bug = CreateRegisteredBug();
        bug.RequestEvidence();
        return bug;
    }
}
