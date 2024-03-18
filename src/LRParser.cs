namespace codecrafters_redis;

/// <summary>
/// Simple left-to-right parser (no look ahead) using a span
/// </summary>
/// <param name="line"></param>
public ref struct LrParser(string line)
{
    private ReadOnlySpan<char> _line = line.AsSpan();

    public bool EOF => _line.Length == 0;

    public bool TryMatch(char match)
    {
        if (!EOF && _line[0] == match)
        {
            _line = _line[1..];
            return true;
        }
        return false;
    }
    
    public void SkipWhitespace()
    {
        while (!EOF &&  char.IsWhiteSpace(_line[0]))
            _line = _line[1..];
    }

    public char EatChar()
    {
        var c = _line[0];
        _line = _line[1..];
        return c;
    }

    public string EatToEnd() => _line.ToString();
}