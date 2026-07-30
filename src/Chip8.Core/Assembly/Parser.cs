using System;
using System.Collections.Generic;

namespace Chip8.Core.Assembly;

public sealed class Parser
{
    static readonly HashSet<string> directives = new(StringComparer.OrdinalIgnoreCase)
    {
        "DB", "DW", "ORG", "ALIGN",
    };

    readonly List<Token> tokens;
    int index;

    public Parser(List<Token> tokens)
    {
        this.tokens = tokens;
    }

    public List<Statement> parse()
    {
        var statements = new List<Statement>();

        while (current().kind != TokenKind.EndOfFile)
        {
            if (current().kind == TokenKind.EndOfLine)
            {
                index++;
                continue;
            }

            parseLine(statements);
        }

        return statements;
    }

    void parseLine(List<Statement> statements)
    {
        List<Token> line = takeLine();
        int cursor = 0;

        if (line.Count >= 2 && line[0].kind == TokenKind.Identifier && line[1].kind == TokenKind.Colon)
        {
            statements.Add(new LabelStatement(line[0].text, line[0].position));
            cursor = 2;
        }

        if (cursor >= line.Count)
        {
            return;
        }

        Token head = line[cursor];
        if (head.kind != TokenKind.Identifier)
        {
            throw new AsmException("expected an instruction or directive but found '" + head.text + "'", head.position);
        }

        if (cursor + 1 < line.Count && line[cursor + 1].isIdentifier("EQU"))
        {
            List<Token> expression = line.GetRange(cursor + 2, line.Count - cursor - 2);
            if (expression.Count == 0)
            {
                throw new AsmException("EQU needs a value", head.position);
            }
            statements.Add(new ConstantStatement(head.text, expression, head.position));
            return;
        }

        if (directives.Contains(head.text))
        {
            statements.Add(parseDirective(head, line, cursor + 1));
            return;
        }

        var operands = parseOperands(line, cursor + 1);
        statements.Add(new InstructionStatement(head.text, operands, head.position));
    }

    Statement parseDirective(Token head, List<Token> line, int start)
    {
        List<Token> body = line.GetRange(start, line.Count - start);

        if (head.isIdentifier("ORG"))
        {
            requireExpression(body, head.position, "ORG");
            return new OrgStatement(body, head.position);
        }

        if (head.isIdentifier("ALIGN"))
        {
            requireExpression(body, head.position, "ALIGN");
            return new AlignStatement(body, head.position);
        }

        bool word = head.isIdentifier("DW");
        List<IReadOnlyList<Token>> items = splitByComma(body, head.position);
        if (items.Count == 0)
        {
            throw new AsmException(head.text + " needs at least one value", head.position);
        }
        return new DataStatement(word, items, head.position);
    }

    List<Operand> parseOperands(List<Token> line, int start)
    {
        var operands = new List<Operand>();
        if (start >= line.Count)
        {
            return operands;
        }

        List<Token> body = line.GetRange(start, line.Count - start);
        foreach (IReadOnlyList<Token> group in splitByComma(body, line[start].position))
        {
            operands.Add(classifyOperand(group));
        }

        return operands;
    }

    static Operand classifyOperand(IReadOnlyList<Token> group)
    {
        if (group.Count == 0)
        {
            throw new AsmException("empty operand", new SourcePosition(0, 0));
        }

        SourcePosition position = group[0].position;

        if (group.Count == 3 && group[0].kind == TokenKind.LeftBracket
            && group[1].isIdentifier("I") && group[2].kind == TokenKind.RightBracket)
        {
            return Operand.special(OperandKind.IndexIndirect, position);
        }

        if (group.Count == 1 && group[0].kind == TokenKind.Identifier)
        {
            int register = registerIndex(group[0].text);
            if (register >= 0)
            {
                return Operand.register(register, position);
            }

            OperandKind? special = specialRegister(group[0].text);
            if (special.HasValue)
            {
                return Operand.special(special.Value, position);
            }
        }

        if (group.Count == 3 && group[1].kind == TokenKind.Minus
            && group[0].kind == TokenKind.Identifier && group[2].kind == TokenKind.Identifier)
        {
            int start = registerIndex(group[0].text);
            int end = registerIndex(group[2].text);
            if (start >= 0 && end >= 0)
            {
                return Operand.registerRange(start, end, position);
            }
        }

        return Operand.immediate(new List<Token>(group), position);
    }

    static OperandKind? specialRegister(string text) => text.ToUpperInvariant() switch
    {
        "I" => OperandKind.IndexRegister,
        "DT" => OperandKind.DelayTimer,
        "ST" => OperandKind.SoundTimer,
        "K" => OperandKind.KeyReg,
        "F" => OperandKind.FontReg,
        "HF" => OperandKind.BigFontReg,
        "B" => OperandKind.BcdReg,
        "R" => OperandKind.RplReg,
        _ => null,
    };

    static int registerIndex(string text)
    {
        if (text.Length != 2 || (text[0] != 'V' && text[0] != 'v'))
        {
            return -1;
        }
        char digit = text[1];
        if (digit >= '0' && digit <= '9')
        {
            return digit - '0';
        }
        if (digit >= 'A' && digit <= 'F')
        {
            return digit - 'A' + 10;
        }
        if (digit >= 'a' && digit <= 'f')
        {
            return digit - 'a' + 10;
        }
        return -1;
    }

    static List<IReadOnlyList<Token>> splitByComma(List<Token> body, SourcePosition position)
    {
        var groups = new List<IReadOnlyList<Token>>();
        var current = new List<Token>();
        int depth = 0;

        foreach (Token token in body)
        {
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
                if (current.Count == 0)
                {
                    throw new AsmException("empty operand before comma", token.position);
                }
                groups.Add(current);
                current = new List<Token>();
                continue;
            }

            current.Add(token);
        }

        if (current.Count > 0)
        {
            groups.Add(current);
        }

        return groups;
    }

    static void requireExpression(List<Token> body, SourcePosition position, string directive)
    {
        if (body.Count == 0)
        {
            throw new AsmException(directive + " needs an expression", position);
        }
    }

    List<Token> takeLine()
    {
        var line = new List<Token>();
        while (current().kind != TokenKind.EndOfLine && current().kind != TokenKind.EndOfFile)
        {
            line.Add(current());
            index++;
        }
        if (current().kind == TokenKind.EndOfLine)
        {
            index++;
        }
        return line;
    }

    Token current() => tokens[index];
}
