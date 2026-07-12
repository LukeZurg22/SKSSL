using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using SKSSL.ECS;
using static SKSSL.Mathematics.CharacterExtensions;

namespace SKSSL.Mathematics;

/// <summary>
/// Algorithmically calculates values based on string input. This class was written to align with the
/// <a href="https://en.wikipedia.org/wiki/Shunting_yard_algorithm">Shunting Yard Algorithm</a>.
/// <remarks>This algorithm was manually implemented. This is likely not the most performant implementation!</remarks>
/// </summary>
public static class ShuntingYard
{
    /// <summary>
    /// Interpret, Parse, and Evaluate a string expression and output a final result, with logging.
    /// </summary>
    /// <param name="expression">string expression to evaluate.</param>
    /// <param name="result">Final expected value.</param>
    /// <param name="source">Optional provided source for tracing.</param>
    /// <param name="statistics"></param>
    /// <returns>true if expression evaluated completely. false if otherwise.</returns>
    /// <remarks>
    /// Loops over an expression more than once. Conglomerating all of the functions into
    /// one large function is easily doable, but does not read very well.
    /// </remarks>
    public static bool Evaluate(
        string expression,
        out double result,
        string source = "",
        StatisticsList? statistics = null)
    {
        result = 0;
        string location = string.IsNullOrEmpty(source) ? "an unknown source" : source;

        // Check to make sure the brackets are good.
        if (!CheckDelimiters())
            throw new EvaluateException($"Invalid expression \"{expression}\" from {location}. Check delimiters.");

        // Parse an expression into a series of tokens, and a set of indices of detected string-variables.

        #region Parse Tokens

        // Evaluate string variables.
        List<string> tokens = [];

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

                // Create the full string variable.
                string variable = expression[start..(i + 1)];
                
                // If this variable isn't in the statistics provided, then what can one do but explode?
                if (statistics == null)
                    throw new NullReferenceException(
                        $"Shunting Yard found variable \'{variable}\', but no statistics to emplace a value!");
                tokens.Add(statistics.GetValue(variable).ToString(CultureInfo.InvariantCulture));
            }
        }

        #endregion

        // Calculate final result.
        result = EvaluateTokens(tokens.ToArray());
        return true;

        // Determines if a provided string expression contains an adequate number of brackets of any kind.
        bool CheckDelimiters()
        {
            var stack = new Stack<char>();
            foreach (char c in expression)
            {
                if (!c.IsBracket())
                    continue;

                switch (c)
                {
                    case '(':
                    case '[':
                    case '{':
                        stack.Push(c);
                        break;

                    case ')':
                        if (stack.Count == 0 || stack.Pop() != '(')
                            return false;
                        break;

                    case ']':
                        if (stack.Count == 0 || stack.Pop() != '[')
                            return false;
                        break;

                    case '}':
                        if (stack.Count == 0 || stack.Pop() != '{')
                            return false;
                        break;
                }
            }

            return stack.Count == 0;
        }
    }

    // P.E.M.D.A.S.: Parenthesis, exponents, multiplication, division, addition, subtraction. 
    private static readonly (string OPERATOR, byte PRECEDENCE)[] Precedences =
    [
        ("+", 1),
        ("-", 1),
        ("*", 2),
        ("/", 2),
        ("^", 3),
        ("u-", 4)
    ];

    // WIP: Merge this into CreateTokens for one big single-pass algorithm.
    private static double EvaluateTokens(string[] tokens)
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
            else ParseToken(token, i);
        }

        // Pop remaining operators
        while (operators.Count > 0)
        {
            string op = operators.Pop();
            if (op != "(") output.Enqueue(op);
        }

        return EvaluateRPN(output);

        // Parse all proper tokens that aren't immediately recognised as doubles. 
        void ParseToken(string token, int index)
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

            return;

            bool ShouldPop(string top, string current)
            {
                bool topFound = false, currentFound = false;
                var topValue = 0;
                var currentValue = 0;

                // Loop through the entire list once, and mark them as we find them.
                foreach ((string OPERATOR, byte PRECEDENCE) precedence in Precedences)
                {
                    if (precedence.OPERATOR.Equals(top))
                    {
                        topFound = true;
                        topValue = precedence.PRECEDENCE;
                        if (currentFound) break; // Short-Circuit.
                    }

                    if (!precedence.OPERATOR.Equals(current)) continue; // Short-Circuit.
                    currentFound = true;
                    currentValue = precedence.PRECEDENCE;
                    if (topFound) break; // Short-Circuit.
                }

                return topFound switch
                {
                    // If not found either value, simply return false.
                    false when !currentFound => false,
                    _ => current switch
                    {
                        // Right-Associative return that top precedence is above the current in matters of special unary
                        // operators.
                        "^" or "u-" => topValue > currentValue,
                        _ => topValue >= currentValue
                    }
                };
            }
        }
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