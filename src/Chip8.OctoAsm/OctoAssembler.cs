using System;
using System.Collections.Generic;

namespace Chip8.OctoAsm;

public sealed partial class OctoAssembler
{
    const int expansionLimit = 10000;

    static readonly HashSet<string> keywords = new(StringComparer.Ordinal)
    {
        ":=", "+=", "-=", "=-", "|=", "&=", "^=", ">>=", "<<=", "==", "!=", "<", ">", "<=", ">=", "-",
        "key", "-key", "hex", "bighex", "random", "delay", "buzzer", "pitch", "long", "i",
        "if", "then", "begin", "else", "end", "loop", "again", "while",
        "return", ";", "clear", "bcd", "save", "load", "sprite", "jump", "jump0", "native",
        "hires", "lores", "exit", "scroll-up", "scroll-down", "scroll-left", "scroll-right",
        "plane", "audio", "saveflags", "loadflags",
    };

    enum FixupKind
    {
        Address12,
        Word16,
        UnpackHigh,
        UnpackLongHigh,
        UnpackLow,
    }

    readonly struct Fixup
    {
        public Fixup(string name, int address, FixupKind kind, SourcePosition position)
        {
            this.name = name;
            this.address = address;
            this.kind = kind;
            this.position = position;
        }

        public string name { get; }
        public int address { get; }
        public FixupKind kind { get; }
        public SourcePosition position { get; }
    }

    sealed class Macro
    {
        public Macro(IReadOnlyList<string> parameters, IReadOnlyList<OctoToken> body)
        {
            this.parameters = parameters;
            this.body = body;
        }

        public IReadOnlyList<string> parameters { get; }
        public IReadOnlyList<OctoToken> body { get; }
    }

    readonly List<OctoToken> tokens;
    readonly OctoImage image = new();
    readonly Dictionary<string, int> labels = new(StringComparer.Ordinal);
    readonly Dictionary<string, double> constants = new(StringComparer.Ordinal);
    readonly Dictionary<string, int> aliases = new(StringComparer.Ordinal);
    readonly Dictionary<string, Macro> macros = new(StringComparer.Ordinal);
    readonly Dictionary<string, int> breakpoints = new(StringComparer.Ordinal);
    readonly List<Fixup> fixups = new();
    readonly List<int> instructionAddresses = new();
    int cursor;
    int expansions;
    bool entryResolved;

    OctoAssembler(string source)
    {
        tokens = OctoLexer.tokenize(source);
    }

    public static OctoProgram assemble(string source) => new OctoAssembler(source).compile();

    public static byte[] assembleRom(string source) => assemble(source).rom;

    OctoProgram compile()
    {
        while (peek().kind != OctoTokenKind.EndOfFile)
        {
            statement();
        }

        if (controlStack.Count > 0)
        {
            ControlFrame open = controlStack.Peek();
            throw error("'" + open.keyword + "' is never closed", open.position);
        }

        resolveFixups();
        return new OctoProgram(
            image.toRom(),
            new Dictionary<string, int>(labels, StringComparer.Ordinal),
            new Dictionary<string, double>(constants, StringComparer.Ordinal),
            new Dictionary<string, int>(breakpoints, StringComparer.Ordinal),
            instructionAddresses);
    }

    void statement()
    {
        OctoToken token = next();

        if (token.kind == OctoTokenKind.Number)
        {
            emitByte(byteOperand(token), token.position);
            return;
        }

        if (token.kind != OctoTokenKind.Word)
        {
            throw error("unexpected " + token, token.position);
        }

        if (directiveStatement(token) || instructionStatement(token) || controlStatement(token))
        {
            return;
        }

        if (isRegister(token))
        {
            registerStatement(token);
            return;
        }

        if (macros.TryGetValue(token.text, out Macro? macro))
        {
            expandMacro(macro, token);
            return;
        }

        if (constants.ContainsKey(token.text))
        {
            emitByte(byteOperand(token), token.position);
            return;
        }

        if (keywords.Contains(token.text))
        {
            throw error("unexpected '" + token.text + "'", token.position);
        }

        emitAddressInstruction(0x2000, token);
    }

    bool directiveStatement(OctoToken token)
    {
        switch (token.text)
        {
            case ":":
                defineLabel(nextName());
                return true;

            case ":const":
            {
                OctoToken name = nextName();
                defineConstant(name, constantValue(next()), false);
                return true;
            }

            case ":calc":
            {
                OctoToken name = nextName();
                defineConstant(name, braceExpression(), true);
                return true;
            }

            case ":alias":
            {
                OctoToken name = nextName();
                aliases[name.text] = registerIndex(next());
                return true;
            }

            case ":byte":
            {
                int value = peek().kind == OctoTokenKind.OpenBrace
                    ? checkByte((int)braceExpression(), token)
                    : byteOperand(next());
                emitByte(value, token.position);
                return true;
            }

            case ":pointer":
            {
                ensureEntry(token.position);
                int value = addressOperand(next(), FixupKind.Word16, image.here);
                emitWord(value, token.position);
                return true;
            }

            case ":org":
            {
                ensureEntry(token.position);
                double target = peek().kind == OctoTokenKind.OpenBrace ? braceExpression() : constantValue(next());
                if (target < OctoImage.origin || target >= OctoImage.addressLimit)
                {
                    throw error(":org address " + target + " is outside 0x200..0xFFFF", token.position);
                }
                image.here = (int)target;
                return true;
            }

            case ":next":
            {
                OctoToken name = nextName();
                ensureEntry(token.position);
                defineLabelAt(name, image.here + 1);
                return true;
            }

            case ":unpack":
                unpackStatement(token);
                return true;

            case ":call":
                emitAddressInstruction(0x2000, next());
                return true;

            case ":macro":
                defineMacro();
                return true;

            case ":breakpoint":
            {
                OctoToken name = nextName();
                ensureEntry(token.position);
                breakpoints[name.text] = image.here;
                return true;
            }

            case ":assert":
                if (braceExpression() == 0)
                {
                    throw error("assertion failed", token.position);
                }
                return true;

            default:
                if (token.text.Length > 1 && token.text[0] == ':')
                {
                    throw error("unsupported directive '" + token.text + "'", token.position);
                }
                return false;
        }
    }

    void unpackStatement(OctoToken token)
    {
        bool longForm = peek().isWord("long");
        int nibble = 0;
        if (longForm)
        {
            next();
        }
        else
        {
            nibble = nibbleOperand(next());
        }

        ensureEntry(token.position);
        OctoToken target = next();
        if (!tryResolveAddress(target, out int address))
        {
            requireForwardName(target);
            fixups.Add(new Fixup(target.text, image.here + 1, longForm ? FixupKind.UnpackLongHigh : FixupKind.UnpackHigh, target.position));
            fixups.Add(new Fixup(target.text, image.here + 3, FixupKind.UnpackLow, target.position));
        }

        int high = longForm ? (address >> 8) & 0xFF : (nibble << 4) | ((address >> 8) & 0x0F);
        emitInstruction(0x6000 | high, token.position);
        emitInstruction(0x6100 | (address & 0xFF), token.position);
    }

    void defineMacro()
    {
        OctoToken name = nextName();
        var parameters = new List<string>();
        while (peek().kind == OctoTokenKind.Word)
        {
            parameters.Add(next().text);
        }

        OctoToken open = next();
        if (open.kind != OctoTokenKind.OpenBrace)
        {
            throw error("expected '{' to start the body of macro '" + name.text + "'", open.position);
        }

        var body = new List<OctoToken>();
        int depth = 1;
        while (true)
        {
            OctoToken token = next();
            if (token.kind == OctoTokenKind.EndOfFile)
            {
                throw error("macro '" + name.text + "' is missing its closing '}'", name.position);
            }
            if (token.kind == OctoTokenKind.OpenBrace)
            {
                depth++;
            }
            else if (token.kind == OctoTokenKind.CloseBrace && --depth == 0)
            {
                break;
            }
            body.Add(token);
        }

        macros[name.text] = new Macro(parameters, body);
    }

    void expandMacro(Macro macro, OctoToken call)
    {
        if (++expansions > expansionLimit)
        {
            throw error("macro expansion limit reached (recursive macro?)", call.position);
        }

        var arguments = new Dictionary<string, OctoToken>(StringComparer.Ordinal);
        foreach (string parameter in macro.parameters)
        {
            OctoToken argument = next();
            if (argument.kind == OctoTokenKind.EndOfFile)
            {
                throw error("macro '" + call.text + "' expects " + macro.parameters.Count + " argument(s)", call.position);
            }
            arguments[parameter] = argument;
        }

        var expanded = new List<OctoToken>(macro.body.Count);
        foreach (OctoToken token in macro.body)
        {
            expanded.Add(token.kind == OctoTokenKind.Word && arguments.TryGetValue(token.text, out OctoToken argument)
                ? argument
                : token);
        }
        tokens.InsertRange(cursor, expanded);
    }

    void defineLabel(OctoToken name)
    {
        if (name.text == "main" && !entryResolved && image.here == OctoImage.origin)
        {
            entryResolved = true;
        }
        else
        {
            ensureEntry(name.position);
        }
        defineLabelAt(name, image.here);
    }

    void defineLabelAt(OctoToken name, int address)
    {
        if (labels.ContainsKey(name.text) || constants.ContainsKey(name.text))
        {
            throw error("'" + name.text + "' is already defined", name.position);
        }
        labels[name.text] = address;
    }

    void defineConstant(OctoToken name, double value, bool allowRedefinition)
    {
        if (labels.ContainsKey(name.text) || (!allowRedefinition && constants.ContainsKey(name.text)))
        {
            throw error("'" + name.text + "' is already defined", name.position);
        }
        constants[name.text] = value;
    }

    void ensureEntry(SourcePosition where)
    {
        if (entryResolved)
        {
            return;
        }
        entryResolved = true;
        fixups.Add(new Fixup("main", image.here, FixupKind.Address12, where));
        instructionAddresses.Add(image.here);
        image.emitWord(0x1000, where);
    }

    void resolveFixups()
    {
        foreach (Fixup fixup in fixups)
        {
            int target;
            if (labels.TryGetValue(fixup.name, out int address))
            {
                target = address;
            }
            else if (constants.TryGetValue(fixup.name, out double value))
            {
                target = (int)value;
            }
            else
            {
                throw error(fixup.name == "main" ? "program has no 'main' label" : "undefined name '" + fixup.name + "'", fixup.position);
            }

            switch (fixup.kind)
            {
                case FixupKind.Address12:
                    image.patchAddress(fixup.address, checkAddress12(target, fixup.name, fixup.position));
                    break;
                case FixupKind.Word16:
                    image[fixup.address] = (byte)(target >> 8);
                    image[fixup.address + 1] = (byte)target;
                    break;
                case FixupKind.UnpackHigh:
                    image[fixup.address] = (byte)((image[fixup.address] & 0xF0) | ((target >> 8) & 0x0F));
                    break;
                case FixupKind.UnpackLongHigh:
                    image[fixup.address] = (byte)(target >> 8);
                    break;
                case FixupKind.UnpackLow:
                    image[fixup.address] = (byte)target;
                    break;
            }
        }
    }

    double braceExpression()
    {
        OctoToken open = next();
        if (open.kind != OctoTokenKind.OpenBrace)
        {
            throw error("expected '{' to start an expression", open.position);
        }

        int start = cursor;
        int depth = 1;
        while (true)
        {
            OctoToken token = next();
            if (token.kind == OctoTokenKind.EndOfFile)
            {
                throw error("expression is missing its closing '}'", open.position);
            }
            if (token.kind == OctoTokenKind.OpenBrace)
            {
                depth++;
            }
            else if (token.kind == OctoTokenKind.CloseBrace && --depth == 0)
            {
                break;
            }
        }

        return OctoConstEvaluator.evaluate(tokens, start, cursor - 1, resolveName, open.position);
    }

    double resolveName(OctoToken token)
    {
        if (constants.TryGetValue(token.text, out double value))
        {
            return value;
        }
        if (labels.TryGetValue(token.text, out int address))
        {
            return address;
        }
        if (token.text == "HERE")
        {
            ensureEntry(token.position);
            return image.here;
        }
        throw error("undefined name '" + token.text + "' in expression", token.position);
    }

    OctoToken next()
    {
        OctoToken token = tokens[cursor];
        if (token.kind != OctoTokenKind.EndOfFile)
        {
            cursor++;
        }
        return token;
    }

    OctoToken peek() => tokens[cursor];

    OctoToken nextName()
    {
        OctoToken token = next();
        if (token.kind != OctoTokenKind.Word || keywords.Contains(token.text) || isRegister(token) || token.text.StartsWith(":", StringComparison.Ordinal))
        {
            throw error("expected a name but found " + token, token.position);
        }
        return token;
    }

    void expectWord(string word)
    {
        OctoToken token = next();
        if (!token.isWord(word))
        {
            throw error("expected '" + word + "' but found " + token, token.position);
        }
    }

    static OctoCompileException error(string message, SourcePosition where) =>
        new(new OctoDiagnostic(OctoSeverity.Error, message, where));
}
