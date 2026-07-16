using System.Text;

namespace SteamBibliotekRydder.Services;

public sealed class VdfNode
{
    private readonly Dictionary<string, VdfNode> _children =
        new(StringComparer.OrdinalIgnoreCase);

    public VdfNode(string name, string? value = null)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public string? Value { get; }

    public IEnumerable<VdfNode> Children => _children.Values;

    public VdfNode? this[string name] =>
        _children.TryGetValue(name, out var child) ? child : null;

    public void Add(VdfNode child) => _children[child.Name] = child;
}

public static class VdfTextParser
{
    public static VdfNode ParseFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return Parse(reader.ReadToEnd());
    }

    public static VdfNode Parse(string input)
    {
        var tokens = Tokenize(input);
        var index = 0;
        var root = new VdfNode("__root__");

        while (index < tokens.Count)
        {
            var name = tokens[index++];
            if (name == "}")
            {
                throw new InvalidDataException("Uventet avsluttende klammeparentes i VDF-filen.");
            }

            if (index >= tokens.Count)
            {
                throw new InvalidDataException($"Mangler verdi for VDF-nøkkelen '{name}'.");
            }

            root.Add(ParseValue(name, tokens, ref index));
        }

        return root;
    }

    private static VdfNode ParseValue(string name, IReadOnlyList<string> tokens, ref int index)
    {
        var token = tokens[index++];
        if (token != "{")
        {
            return new VdfNode(name, token);
        }

        var node = new VdfNode(name);
        while (index < tokens.Count && tokens[index] != "}")
        {
            var childName = tokens[index++];
            if (index >= tokens.Count)
            {
                throw new InvalidDataException($"Mangler verdi for VDF-nøkkelen '{childName}'.");
            }

            node.Add(ParseValue(childName, tokens, ref index));
        }

        if (index >= tokens.Count || tokens[index] != "}")
        {
            throw new InvalidDataException($"VDF-blokken '{name}' er ikke avsluttet.");
        }

        index++;
        return node;
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var index = 0;

        while (index < input.Length)
        {
            SkipWhitespaceAndComments(input, ref index);
            if (index >= input.Length)
            {
                break;
            }

            var current = input[index];
            if (current is '{' or '}')
            {
                tokens.Add(current.ToString());
                index++;
                continue;
            }

            if (current == '"')
            {
                tokens.Add(ReadQuoted(input, ref index));
                continue;
            }

            var start = index;
            while (index < input.Length &&
                   !char.IsWhiteSpace(input[index]) &&
                   input[index] is not '{' and not '}')
            {
                index++;
            }

            tokens.Add(input[start..index]);
        }

        return tokens;
    }

    private static string ReadQuoted(string input, ref int index)
    {
        index++;
        var builder = new StringBuilder();

        while (index < input.Length)
        {
            var current = input[index++];
            if (current == '"')
            {
                return builder.ToString();
            }

            if (current == '\\' && index < input.Length)
            {
                var escaped = input[index++];
                builder.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => escaped
                });
                continue;
            }

            builder.Append(current);
        }

        throw new InvalidDataException("En sitert VDF-streng er ikke avsluttet.");
    }

    private static void SkipWhitespaceAndComments(string input, ref int index)
    {
        while (index < input.Length)
        {
            if (char.IsWhiteSpace(input[index]))
            {
                index++;
                continue;
            }

            if (input[index] == '/' &&
                index + 1 < input.Length &&
                input[index + 1] == '/')
            {
                index += 2;
                while (index < input.Length && input[index] is not '\r' and not '\n')
                {
                    index++;
                }

                continue;
            }

            break;
        }
    }
}
