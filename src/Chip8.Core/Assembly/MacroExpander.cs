using System;
using System.Collections.Generic;

namespace Chip8.Core.Assembly;

public sealed class MacroExpander
{
    const int expansionDepthLimit = 256;

    sealed class MacroDefinition
    {
        public string name = "";
        public List<string> parameters = new();
        public List<List<Token>> body = new();
        public SourcePosition position;
    }

    readonly Dictionary<string, MacroDefinition> macros = new(StringComparer.OrdinalIgnoreCase);

    public List<Token> expand(List<Token> tokens)
    {
        List<List<Token>> lines = splitLines(tokens);
        List<List<Token>> withoutDefinitions = collectDefinitions(lines);
        var output = new List<List<Token>>();
        emitLines(withoutDefinitions, output, 0);
        return joinLines(output);
    }

    List<List<Token>> collectDefinitions(List<List<Token>> lines)
    {
        var remaining = new List<List<Token>>();
        int index = 0;

        while (index < lines.Count)
        {
            List<Token> line = lines[index];
            if (line.Count > 0 && line[0].isIdentifier("MACRO"))
            {
                index = readDefinition(lines, index);
            }
            else
            {
                remaining.Add(line);
                index++;
            }
        }

        return remaining;
    }

    int readDefinition(List<List<Token>> lines, int index)
    {
        List<Token> header = lines[index];
        if (header.Count < 2)
        {
            throw new AsmException("MACRO needs a name", header[0].position);
        }

        var definition = new MacroDefinition { name = header[1].text, position = header[0].position };
        for (int p = 2; p < header.Count; p++)
        {
            if (header[p].kind == TokenKind.Comma)
            {
                continue;
            }
            if (header[p].kind != TokenKind.Identifier)
            {
                throw new AsmException("macro parameter must be a name", header[p].position);
            }
            definition.parameters.Add(header[p].text);
        }

        int cursor = index + 1;
        while (cursor < lines.Count)
        {
            List<Token> line = lines[cursor];
            if (line.Count > 0 && (line[0].isIdentifier("ENDM") || line[0].isIdentifier("ENDMACRO")))
            {
                macros[definition.name] = definition;
                return cursor + 1;
            }
            definition.body.Add(line);
            cursor++;
        }

        throw new AsmException("macro '" + definition.name + "' is missing ENDM", definition.position);
    }

    void emitLines(List<List<Token>> lines, List<List<Token>> output, int depth)
    {
        if (depth > expansionDepthLimit)
        {
            throw new AsmException("macro expansion too deep (recursive macro?)", lines.Count > 0 && lines[0].Count > 0 ? lines[0][0].position : new SourcePosition(0, 0));
        }

        foreach (List<Token> original in lines)
        {
            List<Token> line = original;

            if (line.Count >= 2 && line[0].kind == TokenKind.Identifier && line[1].kind == TokenKind.Colon)
            {
                output.Add(new List<Token> { line[0], line[1] });
                line = line.GetRange(2, line.Count - 2);
            }

            if (line.Count > 0 && line[0].kind == TokenKind.Identifier && macros.TryGetValue(line[0].text, out MacroDefinition? definition))
            {
                List<List<Token>> expanded = instantiate(definition, line);
                emitLines(expanded, output, depth + 1);
            }
            else if (line.Count > 0)
            {
                output.Add(line);
            }
        }
    }

    static List<List<Token>> instantiate(MacroDefinition definition, List<Token> call)
    {
        List<List<Token>> arguments = splitArguments(call, 1);
        if (arguments.Count != definition.parameters.Count)
        {
            throw new AsmException(
                "macro '" + definition.name + "' expects " + definition.parameters.Count + " argument(s), got " + arguments.Count,
                call[0].position);
        }

        var binding = new Dictionary<string, List<Token>>(StringComparer.OrdinalIgnoreCase);
        for (int p = 0; p < definition.parameters.Count; p++)
        {
            binding[definition.parameters[p]] = arguments[p];
        }

        var result = new List<List<Token>>();
        foreach (List<Token> bodyLine in definition.body)
        {
            var substituted = new List<Token>();
            foreach (Token token in bodyLine)
            {
                if (token.kind == TokenKind.Identifier && binding.TryGetValue(token.text, out List<Token>? replacement))
                {
                    substituted.AddRange(replacement);
                }
                else
                {
                    substituted.Add(token);
                }
            }
            result.Add(substituted);
        }

        return result;
    }

    static List<List<Token>> splitArguments(List<Token> line, int start)
    {
        var arguments = new List<List<Token>>();
        var current = new List<Token>();
        int depth = 0;

        for (int index = start; index < line.Count; index++)
        {
            Token token = line[index];
            if (token.kind == TokenKind.LeftParen || token.kind == TokenKind.LeftBracket)
            {
                depth++;
            }
            else if (token.kind == TokenKind.RightParen || token.kind == TokenKind.RightBracket)
            {
                depth--;
            }

            if (token.kind == TokenKind.Comma && depth == 0)
            {
                arguments.Add(current);
                current = new List<Token>();
                continue;
            }

            current.Add(token);
        }

        if (current.Count > 0)
        {
            arguments.Add(current);
        }

        return arguments;
    }

    static List<List<Token>> splitLines(List<Token> tokens)
    {
        var lines = new List<List<Token>>();
        var current = new List<Token>();

        foreach (Token token in tokens)
        {
            if (token.kind == TokenKind.EndOfLine || token.kind == TokenKind.EndOfFile)
            {
                if (current.Count > 0)
                {
                    lines.Add(current);
                    current = new List<Token>();
                }
                continue;
            }
            current.Add(token);
        }

        return lines;
    }

    static List<Token> joinLines(List<List<Token>> lines)
    {
        var tokens = new List<Token>();
        foreach (List<Token> line in lines)
        {
            tokens.AddRange(line);
            SourcePosition position = line.Count > 0 ? line[line.Count - 1].position : new SourcePosition(0, 0);
            tokens.Add(new Token(TokenKind.EndOfLine, "\\n", 0, position));
        }
        tokens.Add(new Token(TokenKind.EndOfFile, "", 0, new SourcePosition(0, 0)));
        return tokens;
    }
}
