namespace CNC.Core;

public sealed class GCodeExecutionProgress
{
    readonly HashSet<uint> _completedLineNumbers = [];
    uint _currentLineNumber;
    int _progressVersion;
    bool _hasLineNumberReports;

    public event EventHandler? Changed;

    public uint CurrentLineNumber => _currentLineNumber;

    public uint CurrentExecutingLineNumber => _currentLineNumber;

    public IReadOnlySet<uint> CompletedLineNumbers => _completedLineNumbers;

    public int ProgressVersion => _progressVersion;

    public int CompletedVersion => _progressVersion;

    public bool HasLineNumberReports => _hasLineNumberReports;

    public bool HasCompletedLines => _completedLineNumbers.Count > 0;

    public HashSet<uint> SnapshotCompletedLineNumbers() => new(_completedLineNumbers);

    public void Reset()
    {
        if (_currentLineNumber == 0 && _completedLineNumbers.Count == 0 && !_hasLineNumberReports)
            return;

        _currentLineNumber = 0;
        _completedLineNumbers.Clear();
        _hasLineNumberReports = false;
        _progressVersion++;
        OnChanged();
    }

    public void MarkExecuting(uint lineNumber)
    {
        if (_currentLineNumber == lineNumber && _hasLineNumberReports)
            return;

        _currentLineNumber = lineNumber;
        _hasLineNumberReports = true;
        OnChanged();
    }

    public void AdvanceTo(uint lineNumber)
    {
        if (lineNumber == 0)
            return;

        if (!_hasLineNumberReports)
        {
            _currentLineNumber = lineNumber;
            _hasLineNumberReports = true;
            OnChanged();
            return;
        }

        if (_currentLineNumber == lineNumber)
            return;

        if (_currentLineNumber != 0 && _completedLineNumbers.Add(_currentLineNumber))
        {
            _progressVersion++;
        }

        _currentLineNumber = lineNumber;
        OnChanged();
    }

    public void MarkCompleted(uint lineNumber)
    {
        var changed = _completedLineNumbers.Add(lineNumber);
        if (changed)
            _progressVersion++;

        if (_currentLineNumber == lineNumber)
        {
            _currentLineNumber = 0;
            changed = true;
        }

        if (changed)
            OnChanged();
    }

    void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
