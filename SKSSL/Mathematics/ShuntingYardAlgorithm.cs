using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using static SKSSL.Extensions.StringHelpers;
using static SKSSL.Mathematics.CharacterExtensions;

namespace SKSSL.Mathematics;

/// <summary>
/// Algorithmically calculates values based on string input. This class was written to align with the
/// <a href="https://en.wikipedia.org/wiki/Shunting_yard_algorithm">Shunting Yard Algorithm</a>.
/// <remarks>This algorithm was manually implemented. This is likely not the most performant implementation!</remarks>
/// </summary>
public static class ShuntingYard
{
    /// P.E.M.D.A.S.: Parenthesis, exponents, multiplication, division, addition, subtraction. 
    private static readonly Dictionary<string, byte> Precedence = new()
        { { "+", 1 }, { "-", 1 }, { "*", 2 }, { "/", 2 }, { "^", 3 }, { "u-", 4 } };

    /// <summary>
    /// Interpret, Parse, and Evaluate a string expression and output a final result, with logging.
    /// </summary>
    /// <param name="expression">string expression to evaluate.</param>
    /// <param name="result">Final expected value.</param>
    /// <param name="source">Optional provided source for tracing.</param>
    /// <returns>true if expression evaluated completely. false if otherwise.</returns>
    /// <remarks>
    /// Loops over an expression more than once. Conglomerating all of the functions into
    /// one large function is easily doable, but does not read very well.
    /// </remarks>
    public static bool Evaluate(string expression, out double result, string source = "")
    {
        result = 0;
        string location = string.IsNullOrEmpty(source) ? "an unknown source" : source;

        // Check to make sure the brackets are good.
        if (!EvaluateDelimiters(expression))
        {
            Log($"Invalid expression \"{expression}\" from {location}. Please check delimiters.", LOG.GENERAL_ERROR);
            return false;
        }

        // Evaluate string variables.
        var parserOutput = ParseExpression(expression);
        if (!EvaluateExpressionStringVariables(parserOutput.StringIndices, parserOutput.Tokens, out var faulty))
        {
            Log($"Failed to evaluate \"{faulty}\" expression \"{expression}\" from {location}.", LOG.GENERAL_ERROR);
            return false;
        }

        // Calculate final result.
        result = Evaluate(parserOutput.Tokens);
        return true;
    }

    private static double Evaluate(string[] tokens)
    {
        if (tokens.Length == 0)
            return 0;

        var output = new Queue<string>(); // RPN output
        var operators = new Stack<string>(); // Operator stack

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];

            // Number
            if (double.TryParse(token, out _)) output.Enqueue(token);
            else ParseSpecialToken(token, i);
        }

        // Pop remaining operators
        while (operators.Count > 0)
        {
            string op = operators.Pop();
            if (op != "(") output.Enqueue(op);
        }

        return EvaluateRPN(output);

        // Parse all proper tokens that aren't immediately recognised as doubles. 
        void ParseSpecialToken(string token, int index)
        {
            switch (token)
            {
                case "(":
                    operators.Push(token);
                    break;
                case ")":
                {
                    while (operators.Count > 0 && operators.Peek() != "(")
                        output.Enqueue(operators.Pop());

                    if (operators.Count > 0 && operators.Peek() == "(")
                        operators.Pop(); // discard '('
                    break;
                }
                default:
                {
                    if (token.IsOperator()) // +, -, *, /, ^, u-
                    {
                        string op = token;

                        // Handle unary.
                        if (op is "-" && (index == 0 || IsUnaryOperator(tokens[index - 1])))
                        {
                            op = "u-";
                        }

                        while (operators.Count > 0 &&
                               operators.Peek() != "(" &&
                               ShouldPop(operators.Peek(), op))
                        {
                            output.Enqueue(operators.Pop());
                        }

                        operators.Push(op);
                    }

                    break;
                }
            }
        }
    }

    private static bool ShouldPop(string top, string current)
    {
        if (!Precedence.TryGetValue(top, out byte topValue) || !Precedence.TryGetValue(current, out byte currentValue))
            return false;

        if (current is "^" or "u-") // right-associative
            return topValue > currentValue;

        return topValue >= currentValue;
    }

    /// <summary>
    /// Parse an expression into a series of tokens, and a set of indices of detected string-variables.
    /// </summary>
    private static (string[] Tokens, List<int> StringIndices) ParseExpression(string expression)
    {
        List<string> tokens = [];
        List<int> stringVarIndices = [];

        //  Split string expression into parts.
        for (int i = 0; i < expression.Length; i++)
        {
            // For every character
            char character = expression[i];

            // If character is an expected, add to stack of string operators,
            if (char.IsDigit(character) || character == '.')
            {
                int start = i;
                bool hasDecimal = character == '.';

                while (i + 1 < expression.Length)
                {
                    char next = expression[i + 1];
                    if (char.IsDigit(next)) i++;
                    else if (next == '.' && !hasDecimal)
                    {
                        hasDecimal = true;
                        i++;
                    }
                    else
                        break;
                }

                string number = expression[start..(i + 1)];
                tokens.Add(number);
            }
            // Character is an operator
            else if (character.IsOperator())
            {
                bool isUnary = false;

                // Determine if this is a unary operator
                if (i == 0) isUnary = true; // Start of expression
                else
                {
                    char prev = expression[i - 1];
                    if (prev == '(' || prev.IsOperator()) isUnary = true;
                }

                if (isUnary)
                {
                    // Consume the unary operator
                    string unary = "u" + character;
                    tokens.Add(unary);
                }
                else
                {
                    // Binary operator
                    tokens.Add(character.ToString());
                }
            }
            // Brackets are special characters.
            else if (character.IsBracket())
            {
                tokens.Add(character.ToString());
            }
            // Character belongs to a string variable.
            else
            {
                int start = i;
                int tokenIndex = tokens.Count;

                // Continue reading the entire string, and hope that it is:
                //  - not cut off early
                //  - not an operator
                //  - not a bracket
                //  - not a number (this forces all string variables to be Alphabetic-only!)
                while (i + 1 < expression.Length &&
                       !expression[i + 1].IsOperator() &&
                       !expression[i + 1].IsBracket() &&
                       !char.IsDigit(expression[i + 1]))
                {
                    i++;
                }

                string variable = expression[start..(i + 1)];
                tokens.Add(variable);

                // Store token index to work on later.
                stringVarIndices.Add(tokenIndex);

                // TODO: STEPS
                //  When reading a character that -is- expected, attempt to evalulate the string.
                //  If the string evaluates, pop it from its little stack.
            }
        }

        return (tokens.ToArray(), stringVarIndices);
    }

    /// Evaluates a set of tokens using a set of indices pointing to tokens that happen to be string variables.
    /// <returns>true if all string variables were evaluated correctly; false if otherwise.</returns>
    private static bool EvaluateExpressionStringVariables(
        List<int> stringVarIndices, string[] tokens, out string faultyVariables)
    {
        StringBuilder stringBuilder = new();
        faultyVariables = string.Empty;
        int stringVarCount = stringVarIndices.Count; // Counter for string builder.

        // Evaluate all string variable indices.
        var indices = stringVarIndices.ToImmutableArray();
        foreach (int stringIndex in indices)
        {
            --stringVarCount; // Decrement the "handled these" counter.
            string variable = tokens[stringIndex];

            if (GetStringVariableValue(variable, out double value))
            {
                // Replace original variable's token entry with numerical value.
                tokens[stringIndex] = value.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                // Add to "faulty variables" list. Can clutter the console / logs if there are too many. How can this
                //  be fixed you may ask? Simple! Don't feed this thing stupid expressions with hundreds of undefined
                //  variables, and you will be fine! -Z
                stringBuilder.Append(variable);
                if (stringVarCount > 0) stringBuilder.Append(", ");
            }
        }

        faultyVariables = stringBuilder.ToString();
        return string.IsNullOrEmpty(faultyVariables);
    }

    /// Using the <see cref="StatisticsVariables"/> class, this attempts to get a value from a string.
    private static bool GetStringVariableValue(string variable, out double value)
    {
        if (StatisticsVariables.Statistics.TryGetValue(variable, out value))
            return true;
        value = 0;
        return false;
    }

    /// Read a set tokens, organize into a mathematically calculable stack ordered by importance, and evaluate.
    private static double EvaluateRPN(Queue<string> tokenQueue)
    {
        var stack = new Stack<double>();

        while (tokenQueue.Count > 0)
        {
            string token = tokenQueue.Dequeue();

            if (double.TryParse(token, out double num))
            {
                stack.Push(num);
            }
            else
            {
                switch (token)
                {
                    case "u-":
                        stack.Push(-stack.Pop());
                        break;
                    case "u+":
                        // no-op
                        break;
                    default:
                    {
                        double b = stack.Pop();
                        double a = stack.Pop();

                        stack.Push(token switch
                        {
                            "+" => a + b,
                            "-" => a - b,
                            "*" => a * b,
                            "/" => a / b,
                            "^" => Math.Pow(a, b),
                            _ => throw new ArgumentException($"Unknown operator: {token}")
                        });
                        break;
                    }
                }
            }
        }

        return stack.Pop();
    }
}