using System;
using System.Collections.Generic;
using System.Linq;

namespace VoidCrestMovesetCompat.Framework.Experimental;

/// <summary>
/// One reversible mutation in a crest composition. Rollback must tolerate an Apply action
/// that changed only part of its intended state before throwing.
/// </summary>
public sealed class CrestTransactionStep
{
    public CrestTransactionStep(string name, Action apply, Action rollback, string? featureId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A step name is required.", nameof(name));
        }

        Name = name.Trim();
        Apply = apply ?? throw new ArgumentNullException(nameof(apply));
        Rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
        FeatureId = featureId;
    }

    public string Name { get; }

    public string? FeatureId { get; }

    internal Action Apply { get; }

    internal Action Rollback { get; }
}

public sealed class CrestCompositionException : Exception
{
    internal CrestCompositionException(
        string operationId,
        CrestTransactionStep failedStep,
        Exception applyException,
        IReadOnlyCollection<Exception> rollbackExceptions)
        : base(CreateMessage(operationId, failedStep, rollbackExceptions), applyException)
    {
        OperationId = operationId;
        FailedStep = failedStep;
        RollbackExceptions = rollbackExceptions;
    }

    public string OperationId { get; }

    public CrestTransactionStep FailedStep { get; }

    public IReadOnlyCollection<Exception> RollbackExceptions { get; }

    private static string CreateMessage(
        string operationId,
        CrestTransactionStep failedStep,
        IReadOnlyCollection<Exception> rollbackExceptions)
    {
        string rollbackSummary = rollbackExceptions.Count == 0
            ? "Rollback completed."
            : $"Rollback produced {rollbackExceptions.Count} additional error(s).";
        return $"Crest composition operation '{operationId}' failed at '{failedStep.Name}'. {rollbackSummary}";
    }
}

/// <summary>
/// Executes all composition mutations as a unit and rolls them back in reverse order on failure.
/// Unlike the current compatibility plugin's switch path, the failing step itself is rolled back
/// because Apply may have partially mutated Unity objects before throwing.
/// </summary>
public sealed class CrestCompositionTransaction
{
    private readonly List<CrestTransactionStep> steps = new();
    private readonly ICrestDiagnosticSink diagnostics;
    private bool executed;

    public CrestCompositionTransaction(string? operationId = null, ICrestDiagnosticSink? diagnostics = null)
    {
        OperationId = string.IsNullOrWhiteSpace(operationId)
            ? Guid.NewGuid().ToString("N")
            : operationId!.Trim();
        this.diagnostics = diagnostics ?? NullCrestDiagnosticSink.Instance;
    }

    public string OperationId { get; }

    public IReadOnlyList<CrestTransactionStep> Steps => steps.AsReadOnly();

    public CrestCompositionTransaction Add(CrestTransactionStep step)
    {
        if (executed)
        {
            throw new InvalidOperationException("Steps cannot be added after the transaction has executed.");
        }

        steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
        return this;
    }

    public void Execute()
    {
        if (executed)
        {
            throw new InvalidOperationException("A crest composition transaction can execute only once.");
        }

        executed = true;
        List<CrestTransactionStep> attempted = new();
        diagnostics.Write(new CrestDiagnosticEvent(
            CrestDiagnosticSeverity.Information,
            "transaction",
            $"Beginning crest composition with {steps.Count} step(s).",
            OperationId));

        foreach (CrestTransactionStep step in steps)
        {
            // Record before Apply so a partially completed failing step receives rollback.
            attempted.Add(step);
            try
            {
                diagnostics.Write(new CrestDiagnosticEvent(
                    CrestDiagnosticSeverity.Trace,
                    "transaction.apply",
                    step.Name,
                    OperationId,
                    step.FeatureId));
                step.Apply();
            }
            catch (Exception applyException)
            {
                List<Exception> rollbackExceptions = RollBack(attempted);
                diagnostics.Write(new CrestDiagnosticEvent(
                    CrestDiagnosticSeverity.Error,
                    "transaction.failed",
                    $"Composition failed at '{step.Name}'.",
                    OperationId,
                    step.FeatureId,
                    applyException,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["rollbackErrorCount"] = rollbackExceptions.Count.ToString()
                    }));
                throw new CrestCompositionException(
                    OperationId,
                    step,
                    applyException,
                    Array.AsReadOnly(rollbackExceptions.ToArray()));
            }
        }

        diagnostics.Write(new CrestDiagnosticEvent(
            CrestDiagnosticSeverity.Information,
            "transaction.committed",
            "Crest composition committed.",
            OperationId));
    }

    private List<Exception> RollBack(IEnumerable<CrestTransactionStep> attemptedSteps)
    {
        List<Exception> rollbackExceptions = new();
        foreach (CrestTransactionStep step in attemptedSteps.Reverse())
        {
            try
            {
                diagnostics.Write(new CrestDiagnosticEvent(
                    CrestDiagnosticSeverity.Trace,
                    "transaction.rollback",
                    step.Name,
                    OperationId,
                    step.FeatureId));
                step.Rollback();
            }
            catch (Exception rollbackException)
            {
                rollbackExceptions.Add(new InvalidOperationException(
                    $"Rollback failed for crest transaction step '{step.Name}'.",
                    rollbackException));
            }
        }

        return rollbackExceptions;
    }
}
