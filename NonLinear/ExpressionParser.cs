using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LPR381Project.NonLinear
{
    /// <summary>Thrown when a function cannot be parsed.</summary>
    public class ParseException : Exception
    {
        public int Position { get; }

        public ParseException(string message, int position) : base(message)
        {
            Position = position;
        }
    }

    /// <summary>Parses arithmetic expressions with one or more variables.</summary>
    public static class ExpressionParser
    {
        /// <summary>Parses text and returns a callable function.</summary>
        public static ObjectiveFunction Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ParseException("The function is empty.", 0);
            }

            List<Token> tokens = Tokenize(text);
            var state = new ParserState(tokens, text);

            ExpressionNode root = ParseExpression(state);

            // Check that the full expression was parsed.
            if (!state.AtEnd)
            {
                throw new ParseException(
                    string.Format("Unexpected '{0}' after a complete expression.", state.Current.Text),
                    state.Current.Position);
            }

            List<string> variables = OrderVariables(state.SeenVariables);
            if (variables.Count == 0)
            {
                throw new ParseException("That expression has no variables in it, so there is nothing to optimise.", 0);
            }

            return new ObjectiveFunction(text.Trim(), root, variables);
        }

        /// <summary>
        /// Evaluates text that has to come out as a single number - "pi/2", "2^-4",
        /// "-sqrt(2)". The prompts that ask for an interval, a starting point or a
        /// tolerance use this, so anything writable in a function is writable there too.
        /// </summary>
        /// <remarks>
        /// Returns false rather than throwing, because every caller has a default to
        /// fall back on and none of them wants to handle an exception. Text that still
        /// mentions a variable is rejected: a starting point of "x" is a question the
        /// caller cannot answer. A NaN or infinite result is rejected for the same
        /// reason - "1/0" is not a starting point either.
        /// </remarks>
        public static bool TryParseConstant(string text, out double value)
        {
            value = 0.0;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            ExpressionNode root;
            ParserState state;

            try
            {
                state = new ParserState(Tokenize(text), text);
                root = ParseExpression(state);
            }
            catch (ParseException)
            {
                return false;
            }

            if (!state.AtEnd || state.SeenVariables.Count > 0)
            {
                return false;
            }

            // No variables, so an empty slot table and an empty point are enough.
            root.Bind(new Dictionary<string, int>());
            double result = root.Evaluate(Array.Empty<double>());

            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                return false;
            }

            value = result;
            return true;
        }

        // Grammar rules in precedence order.

        /// <summary>Parses addition and subtraction.</summary>
        private static ExpressionNode ParseExpression(ParserState state)
        {
            ExpressionNode left = ParseTerm(state);

            while (state.CurrentIs("+") || state.CurrentIs("-"))
            {
                char op = state.Current.Text[0];
                state.Advance();
                ExpressionNode right = ParseTerm(state);
                left = new BinaryNode(op, left, right);
            }

            return left;
        }

        /// <summary>Parses multiplication and division.</summary>
        private static ExpressionNode ParseTerm(ParserState state)
        {
            ExpressionNode left = ParseUnary(state);

            while (state.CurrentIs("*") || state.CurrentIs("/"))
            {
                char op = state.Current.Text[0];
                state.Advance();
                ExpressionNode right = ParseUnary(state);
                left = new BinaryNode(op, left, right);
            }

            return left;
        }

        /// <summary>Parses leading positive and negative signs.</summary>
        private static ExpressionNode ParseUnary(ParserState state)
        {
            if (state.CurrentIs("-"))
            {
                state.Advance();
                return new NegateNode(ParseUnary(state));
            }

            // Ignore a leading plus.
            if (state.CurrentIs("+"))
            {
                state.Advance();
                return ParseUnary(state);
            }

            // The tokenizer turns a bare root sign into this, so it binds exactly as
            // tightly as a sign does: the root covers the value it sits in front of and
            // nothing after it, which is how it reads on paper.
            if (state.CurrentIs("sqrt"))
            {
                state.Advance();
                return new FunctionNode("sqrt", Math.Sqrt, ParseUnary(state));
            }

            return ParseFactor(state);
        }

        /// <summary>Parses powers.</summary>
        private static ExpressionNode ParseFactor(ParserState state)
        {
            ExpressionNode baseNode = ParseAtom(state);

            if (state.CurrentIs("^"))
            {
                state.Advance();
                ExpressionNode exponent = ParseUnary(state);
                return new BinaryNode('^', baseNode, exponent);
            }

            return baseNode;
        }

        /// <summary>Parses numbers, variables, functions, and brackets.</summary>
        private static ExpressionNode ParseAtom(ParserState state)
        {
            if (state.AtEnd)
            {
                throw new ParseException("The expression ends early - a value was expected here.",
                    state.EndPosition);
            }

            Token token = state.Current;

            if (token.Kind == TokenKind.Number)
            {
                state.Advance();
                return new ConstantNode(double.Parse(token.Text, CultureInfo.InvariantCulture));
            }

            if (token.Kind == TokenKind.Identifier)
            {
                state.Advance();

                // Treat a name followed by '(' as a function call.
                if (state.CurrentIs("("))
                {
                    string name = token.Text.ToLowerInvariant();
                    if (!UnaryFunctions.ContainsKey(name))
                    {
                        throw new ParseException(
                            string.Format("Unknown function '{0}'. Known functions: {1}.",
                                token.Text, string.Join(", ", UnaryFunctions.Keys.OrderBy(k => k))),
                            token.Position);
                    }

                    state.Advance();
                    ExpressionNode argument = ParseExpression(state);
                    Expect(state, ")");
                    return new FunctionNode(name, UnaryFunctions[name], argument);
                }

                if (MathConstants.TryGetValue(token.Text, out double constant))
                {
                    return new ConstantNode(constant);
                }

                state.SeenVariables.Add(token.Text);
                return new VariableNode(token.Text);
            }

            if (token.Text == "(")
            {
                state.Advance();
                ExpressionNode inner = ParseExpression(state);
                Expect(state, ")");
                return inner;
            }

            throw new ParseException(string.Format("Unexpected symbol '{0}'.", token.Text), token.Position);
        }

        /// <summary>Checks for the expected next token.</summary>
        private static void Expect(ParserState state, string text)
        {
            if (state.AtEnd)
            {
                throw new ParseException(string.Format("Expected '{0}' but the expression ended.", text),
                    state.EndPosition);
            }

            if (!state.CurrentIs(text))
            {
                throw new ParseException(
                    string.Format("Expected '{0}' but found '{1}'.", text, state.Current.Text),
                    state.Current.Position);
            }

            state.Advance();
        }

        // Tokenizer.

        private static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (char.IsDigit(c) || c == '.')
                {
                    int start = i;
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                    {
                        i++;
                    }

                    // Scientific notation. The 'e' is only swallowed when a real
                    // exponent follows, so a bare 'e' after a number stays Euler's
                    // constant rather than being eaten as a broken exponent.
                    if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
                    {
                        int peek = i + 1;
                        if (peek < text.Length && (text[peek] == '+' || text[peek] == '-'))
                        {
                            peek++;
                        }

                        if (peek < text.Length && char.IsDigit(text[peek]))
                        {
                            i = peek;
                            while (i < text.Length && char.IsDigit(text[i]))
                            {
                                i++;
                            }
                        }
                    }

                    string number = text.Substring(start, i - start);
                    if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        throw new ParseException(string.Format("'{0}' is not a valid number.", number), start);
                    }

                    tokens.Add(new Token(TokenKind.Number, number, start));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                    {
                        i++;
                    }

                    tokens.Add(new Token(TokenKind.Identifier, text.Substring(start, i - start), start));
                    continue;
                }

                // The symbols people paste out of a document or type on a phone
                // keyboard. They are mapped here, each at its own position, rather than
                // by rewriting the input first - a rewrite would shift every later
                // character and leave the caret in a parse error pointing at the wrong
                // column.
                if (c == '\u2212' || c == '\u00D7' || c == '\u22C5'
                    || c == '\u00B7' || c == '\u00F7')
                {
                    // U+2212 minus, U+00F7 division sign; the rest are all multiply.
                    string ascii = c == '\u2212' ? "-" : c == '\u00F7' ? "/" : "*";
                    tokens.Add(new Token(TokenKind.Symbol, ascii, i));
                    i++;
                    continue;
                }

                // A bare root sign. ParseUnary picks this up as a prefix operator.
                if (c == '\u221A')
                {
                    tokens.Add(new Token(TokenKind.Symbol, "sqrt", i));
                    i++;
                    continue;
                }

                // A superscript is a power with the '^' left out, so emit both halves
                // of it. Both carry the superscript's own position, which is the column
                // the reader would point at anyway.
                int superscript = "\u00B2\u00B3\u2074\u2075\u2076".IndexOf(c);
                if (superscript >= 0)
                {
                    tokens.Add(new Token(TokenKind.Symbol, "^", i));
                    tokens.Add(new Token(TokenKind.Number,
                        (superscript + 2).ToString(CultureInfo.InvariantCulture), i));
                    i++;
                    continue;
                }

                if ("+-*/^()".IndexOf(c) >= 0)
                {
                    tokens.Add(new Token(TokenKind.Symbol, c.ToString(), i));
                    i++;
                    continue;
                }

                throw new ParseException(
                    string.Format("'{0}' is not a symbol this parser understands.", c), i);
            }

            return tokens;
        }

        /// <summary>Sorts variables into a consistent order.</summary>
        private static List<string> OrderVariables(HashSet<string> names)
        {
            List<string> list = names.ToList();

            bool allIndexed = list.Count > 0 && list.All(n => n.Length > 1
                && char.IsLetter(n[0])
                && n.Skip(1).All(char.IsDigit));

            if (allIndexed)
            {
                return list
                    .OrderBy(n => n[0])
                    .ThenBy(n => int.Parse(n.Substring(1), CultureInfo.InvariantCulture))
                    .ToList();
            }

            return list.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Built-in functions and constants.

        private static readonly Dictionary<string, Func<double, double>> UnaryFunctions =
            new Dictionary<string, Func<double, double>>(StringComparer.OrdinalIgnoreCase)
            {
                { "sin", Math.Sin },
                { "cos", Math.Cos },
                { "tan", Math.Tan },
                { "exp", Math.Exp },
                { "ln", Math.Log },
                { "log", Math.Log10 },
                { "sqrt", Math.Sqrt },
                { "abs", Math.Abs }
            };

        // 'e' is safe to reserve here only because the number scanner above consumes
        // the 'e' in 1e-6 as part of the number, so the two never collide. It does mean
        // 'e' cannot be used as a variable name; x, y and x1..xn all still can.
        private static readonly Dictionary<string, double> MathConstants =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "pi", Math.PI },
                { "\u03C0", Math.PI },
                { "e", Math.E }
            };

        // Token helpers.

        private enum TokenKind { Number, Identifier, Symbol }

        private sealed class Token
        {
            public TokenKind Kind { get; }
            public string Text { get; }
            public int Position { get; }

            public Token(TokenKind kind, string text, int position)
            {
                Kind = kind;
                Text = text;
                Position = position;
            }
        }

        /// <summary>Tracks the current parser position.</summary>
        private sealed class ParserState
        {
            private readonly List<Token> _tokens;
            private int _index;

            public HashSet<string> SeenVariables { get; } = new HashSet<string>();
            public int EndPosition { get; }

            public ParserState(List<Token> tokens, string source)
            {
                _tokens = tokens;
                EndPosition = source.Length;
            }

            public bool AtEnd => _index >= _tokens.Count;
            public Token Current => _tokens[_index];
            public void Advance() => _index++;
            public bool CurrentIs(string text) => !AtEnd && _tokens[_index].Text == text;
        }
    }

    // Parsed objective function.

    /// <summary>Stores a parsed objective function.</summary>
    public class ObjectiveFunction
    {
        private readonly ExpressionNode _root;
        private readonly List<string> _variables;

        public ObjectiveFunction(string text, ExpressionNode root, List<string> variables)
        {
            Text = text;
            _root = root;
            _variables = variables;

            var slots = new Dictionary<string, int>();
            for (int i = 0; i < variables.Count; i++)
            {
                slots[variables[i]] = i;
            }

            _root.Bind(slots);
        }

        /// <summary>Original expression.</summary>
        public string Text { get; }

        /// <summary>Variable names in evaluation order.</summary>
        public IReadOnlyList<string> Variables => _variables;

        /// <summary>Number of variables.</summary>
        public int Dimension => _variables.Count;

        /// <summary>Number of function evaluations.</summary>
        public int EvaluationCount { get; private set; }

        /// <summary>Evaluates the function at a point.</summary>
        public double Evaluate(double[] point)
        {
            if (point.Length != _variables.Count)
            {
                throw new ArgumentException(
                    string.Format("Expected {0} value(s) but got {1}.", _variables.Count, point.Length),
                    nameof(point));
            }

            EvaluationCount++;
            return _root.Evaluate(point);
        }

        /// <summary>Evaluates a single-variable function.</summary>
        public double Evaluate(double x) => Evaluate(new[] { x });

        /// <summary>Formats the function for display.</summary>
        public string Signature() => string.Format("f({0}) = {1}", string.Join(", ", _variables), Text);
    }

    // Expression tree.

    /// <summary>Base class for expression tree nodes.</summary>
    public abstract class ExpressionNode
    {
        /// <summary>Evaluates this node.</summary>
        public abstract double Evaluate(double[] point);

        /// <summary>Binds variable nodes to array positions.</summary>
        public abstract void Bind(IReadOnlyDictionary<string, int> slots);
    }

    /// <summary>Constant value node.</summary>
    public sealed class ConstantNode : ExpressionNode
    {
        private readonly double _value;

        public ConstantNode(double value)
        {
            _value = value;
        }

        public override double Evaluate(double[] point) => _value;

        public override void Bind(IReadOnlyDictionary<string, int> slots)
        {
            // No variables to bind.
        }
    }

    /// <summary>Variable node.</summary>
    public sealed class VariableNode : ExpressionNode
    {
        private readonly string _name;
        private int _slot = -1;

        public VariableNode(string name)
        {
            _name = name;
        }

        public override double Evaluate(double[] point) => point[_slot];

        public override void Bind(IReadOnlyDictionary<string, int> slots)
        {
            if (!slots.TryGetValue(_name, out _slot))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture,
                        "Variable '{0}' was never registered with the function.", _name));
            }
        }
    }

    /// <summary>Binary operator node.</summary>
    public sealed class BinaryNode : ExpressionNode
    {
        private readonly char _op;
        private readonly ExpressionNode _left;
        private readonly ExpressionNode _right;

        public BinaryNode(char op, ExpressionNode left, ExpressionNode right)
        {
            _op = op;
            _left = left;
            _right = right;
        }

        public override double Evaluate(double[] point)
        {
            double a = _left.Evaluate(point);
            double b = _right.Evaluate(point);

            switch (_op)
            {
                case '+': return a + b;
                case '-': return a - b;
                case '*': return a * b;

                // Division by zero returns infinity.
                case '/': return a / b;

                // Invalid powers return NaN.
                case '^': return Math.Pow(a, b);

                default:
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, "Unknown operator '{0}'.", _op));
            }
        }

        public override void Bind(IReadOnlyDictionary<string, int> slots)
        {
            _left.Bind(slots);
            _right.Bind(slots);
        }
    }

    /// <summary>Negation node.</summary>
    public sealed class NegateNode : ExpressionNode
    {
        private readonly ExpressionNode _inner;

        public NegateNode(ExpressionNode inner)
        {
            _inner = inner;
        }

        public override double Evaluate(double[] point) => -_inner.Evaluate(point);

        public override void Bind(IReadOnlyDictionary<string, int> slots) => _inner.Bind(slots);
    }

    /// <summary>Single-argument function node.</summary>
    public sealed class FunctionNode : ExpressionNode
    {
        private readonly string _name;
        private readonly Func<double, double> _fn;
        private readonly ExpressionNode _argument;

        public FunctionNode(string name, Func<double, double> fn, ExpressionNode argument)
        {
            _name = name;
            _fn = fn;
            _argument = argument;
        }

        public override double Evaluate(double[] point) => _fn(_argument.Evaluate(point));

        public override void Bind(IReadOnlyDictionary<string, int> slots) => _argument.Bind(slots);
    }
}
