namespace GA.Business.DSL.SourceGen;

using System.Text.RegularExpressions;

/// <summary>
/// Minimal EBNF parser that understands the Grammar subset used by GA:
///   rule = alt1 | alt2 ;
///   rule = term1, term2 ;
///   rule = "literal" | identifier | ( group ) | [ optional ] | { repeat } ;
/// Produces an AST of <see cref="EbnfRule"/> nodes.
/// </summary>
public sealed class EbnfParser
{
    // Strip EBNF comments: (* ... *)
    private static readonly Regex CommentRe = new(@"\(\*.*?\*\)", RegexOptions.Singleline);

    public IReadOnlyList<EbnfRule> Parse(string source)
    {
        var cleaned = CommentRe.Replace(source, " ");
        var rules   = new List<EbnfRule>();

        // Split on ';' — each rule ends with ;
        var ruleStrings = cleaned.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var ruleStr in ruleStrings)
        {
            var trimmed = ruleStr.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0) continue;

            var name = trimmed[..eqIdx].Trim();
            var body = trimmed[(eqIdx + 1)..].Trim();

            if (string.IsNullOrEmpty(name)) continue;
            rules.Add(new EbnfRule(name, ParseExpr(body)));
        }
        return rules;
    }

    private EbnfExpr ParseExpr(string s)
    {
        s = s.Trim();
        // Alternation: split on top-level '|'
        var alts = SplitTopLevel(s, '|');
        if (alts.Count > 1)
            return new EbnfAlt(alts.Select(ParseSeq).ToList());
        return ParseSeq(s);
    }

    private EbnfExpr ParseSeq(string s)
    {
        s = s.Trim();
        var parts = SplitTopLevel(s, ',');
        if (parts.Count > 1)
            return new EbnfSeq(parts.Select(ParseAtom).ToList());
        return ParseAtom(s);
    }

    private EbnfExpr ParseAtom(string s)
    {
        s = s.Trim();

        if (s.StartsWith('"') && s.EndsWith('"'))
            return new EbnfLiteral(s[1..^1]);

        if (s.StartsWith('\'') && s.EndsWith('\''))
            return new EbnfLiteral(s[1..^1]);

        if (s.StartsWith('[') && s.EndsWith(']'))
            return new EbnfOptional(ParseExpr(s[1..^1]));

        if (s.StartsWith('{') && s.EndsWith('}'))
            return new EbnfRepeat(ParseExpr(s[1..^1]));

        if (s.StartsWith('(') && s.EndsWith(')'))
            return ParseExpr(s[1..^1]);

        // Identifier (non-terminal)
        if (Regex.IsMatch(s, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            return new EbnfRef(s);

        // Fallback — treat as opaque literal
        return new EbnfLiteral(s);
    }

    /// <summary>Split on <paramref name="separator"/> while respecting (), [], {}, and "".</summary>
    private static List<string> SplitTopLevel(string s, char separator)
    {
        var result = new List<string>();
        var depth  = 0;
        var inStr  = false;
        var strCh  = '\0';
        var start  = 0;

        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (inStr)
            {
                if (ch == strCh) inStr = false;
                continue;
            }
            if (ch is '"' or '\'') { inStr = true; strCh = ch; continue; }
            if (ch is '(' or '[' or '{') { depth++; continue; }
            if (ch is ')' or ']' or '}') { depth--; continue; }
            if (ch == separator && depth == 0)
            {
                result.Add(s[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(s[start..].Trim());
        return result.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }
}

// ============================================================================
// EBNF AST nodes
// ============================================================================

public sealed record EbnfRule(string Name, EbnfExpr Body);

public abstract record EbnfExpr;
public sealed record EbnfLiteral(string Value) : EbnfExpr;
public sealed record EbnfRef(string Name) : EbnfExpr;
public sealed record EbnfAlt(List<EbnfExpr> Alternatives) : EbnfExpr;
public sealed record EbnfSeq(List<EbnfExpr> Items) : EbnfExpr;
public sealed record EbnfOptional(EbnfExpr Inner) : EbnfExpr;
public sealed record EbnfRepeat(EbnfExpr Inner) : EbnfExpr;
