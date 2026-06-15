namespace CNC.Core;

public sealed class GCodeExecutionProgress
{
    readonly HashSet<uint> _completedLineNumbers = [];
    uint _currentLineNumber;
    uint _startLineNumber;
    int _completedEntryIndex = -1;
    int _progressVersion;
    bool _hasLineNumberReports;

    public event EventHandler? Changed;

    public uint CurrentLineNumber => _currentLineNumber;

    public uint CurrentExecutingLineNumber => _currentLineNumber;

    public uint StartLineNumber => _startLineNumber;

    public IReadOnlySet<uint> CompletedLineNumbers => _completedLineNumbers;

    public int CompletedEntryIndex => _completedEntryIndex;

    public int ProgressVersion => _progressVersion;

    public int CompletedVersion => _progressVersion;

    public bool HasLineNumberReports => _hasLineNumberReports;

    public bool HasCompletedLines => _completedLineNumbers.Count > 0 ||
        _completedEntryIndex >= 0 ||
        (_hasLineNumberReports && _startLineNumber != 0 && _currentLineNumber != 0 && _currentLineNumber != _startLineNumber);

    public HashSet<uint> SnapshotCompletedLineNumbers() => new(_completedLineNumbers);

    public void Reset() => Reset(0);

    public void Reset(uint startLineNumber)
    {
        if (_currentLineNumber == 0 && _startLineNumber == startLineNumber && _completedLineNumbers.Count == 0 && !_hasLineNumberReports)
            return;

        _currentLineNumber = 0;
        _startLineNumber = startLineNumber;
        _completedEntryIndex = -1;
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
            if (_startLineNumber != 0 && _startLineNumber != lineNumber)
                _progressVersion++;
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
        if (_startLineNumber != 0)
            _progressVersion++;
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

    public void SetCompletedEntryBoundary(int entryIndex)
    {
        if (entryIndex <= _completedEntryIndex)
            return;

        _completedEntryIndex = entryIndex;
        _progressVersion++;
        OnChanged();
    }

    void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
