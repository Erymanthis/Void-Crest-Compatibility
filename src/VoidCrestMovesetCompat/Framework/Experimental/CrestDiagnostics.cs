using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VoidCrestMovesetCompat.Framework.Experimental;

public enum CrestDiagnosticSeverity
{
    Trace,
    Information,
    Warning,
    Error
}

public sealed class CrestDiagnosticEvent
{
    public CrestDiagnosticEvent(
        CrestDiagnosticSeverity severity,
        string category,
        string message,
        string? operationId = null,
        string? featureId = null,
        Exception? exception = null,
        IDictionary<string, string>? properties = null)
    {
        Severity = severity;
        Category = category;
        Message = message;
        OperationId = operationId;
        FeatureId = featureId;
        Exception = exception;
        Properties = new ReadOnlyDictionary<string, string>(
            properties != null
                ? new Dictionary<string, string>(properties, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal));
    }

    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    public CrestDiagnosticSeverity Severity { get; }

    public string Category { get; }

    public string Message { get; }

    public string? OperationId { get; }

    public string? FeatureId { get; }

    public Exception? Exception { get; }

    public IReadOnlyDictionary<string, string> Properties { get; }
}

public interface ICrestDiagnosticSink
{
    void Write(CrestDiagnosticEvent diagnosticEvent);
}

public sealed class NullCrestDiagnosticSink : ICrestDiagnosticSink
{
    public static NullCrestDiagnosticSink Instance { get; } = new();

    private NullCrestDiagnosticSink()
    {
    }

    public void Write(CrestDiagnosticEvent diagnosticEvent)
    {
    }
}

public sealed class DelegateCrestDiagnosticSink : ICrestDiagnosticSink
{
    private readonly Action<CrestDiagnosticEvent> writer;

    public DelegateCrestDiagnosticSink(Action<CrestDiagnosticEvent> writer)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void Write(CrestDiagnosticEvent diagnosticEvent)
    {
        writer(diagnosticEvent);
    }
}
