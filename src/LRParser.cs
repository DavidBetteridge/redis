namespace codecrafters_redis;

/// <summary>
/// Simple left-to-right parser (no look ahead) using a span
/// </summary>
/// <param name="line"></param>
public ref struct LrParser
{
    private ReadOnlySpan<char> _line;

    public LrParser(string line)
    {
        _line = line.AsSpan();
    }
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
    
    public bool TryMatch(string match)
    {
        if (!EOF && _line.StartsWith(match))
        {
            _line = _line[(match.Length)..];
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

    public int EatNumber()
    {
        var value = "";
        while (!EOF && char.IsDigit(_line[0]))
        {
            value += _line[0];
            _line = _line[1..];
        }

        return int.Parse(value);
    }

    public string EatString(int length)
    {
        var str = _line[..length];
        _line = _line[length..];
        return str.ToString();
    }
}