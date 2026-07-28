using System;
using System.Collections.Generic;
using System.Globalization;
using SKSSL.ECS;
using SyntaxErrorException = System.Data.SyntaxErrorException;

namespace SKSSL.Mathematics;

/// <inheritdoc />
public class EmptyStatisticsListException(string s) : Exception(s);

/// <inheritdoc />
public class MissingStatisticException(string s) : Exception(s);

/// <inheritdoc />
public class RecursiveEvaluateException(string s) : Exception(s);

/// <summary>
/// Algorithmically calculates values based on string input. This class was written to align with the
/// <a href="https://en.wikipedia.org/wiki/Shunting_yard_algorithm">Shunting Yard Algorithm</a>.
/// <remarks>This algorithm was manually implemented. This is likely not the most performant implementation!</remarks>
/// </summary>
public class ShuntingYard
{
    private readonly StatisticsList Statistics;
    public ShuntingYard(StatisticsList? statistics = null) => Statistics = statistics ?? new StatisticsList();

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

    /// <summary>
    /// Interpret, Parse, and Evaluate a string expression and output a final result, with logging.
    /// </summary>
    /// <param name="expression">string expression to evaluate.</param>
    /// <param name="parent">Optional owning context that allows accurate statistic indexing.</param>
    /// <param name="visited">Tracking visited Uids to avoid recursion.</param>
    /// <returns>true if expression evaluated completely. false if otherwise.</returns>
    /// <remarks>
    /// Loops over an expression more than once. Conglomerating all of the functions into
    /// one large function is easily doable, but does not read very well.
    /// </remarks>
    public double Evaluate(
        string expression,
        PackableUid? parent = null,
        HashSet<PackableUid>? visited = null)
    {
        string trace = (parent is null ? "an unknown source" : parent.ToString())!;
        var output = new Queue<string>(); // RPN output
        var operators = new Stack<string>(); // Operator stack

        // Parse an expression into a series of tokens, and a set of indices of detected string-variables.

        #region Parse Delimiters & Tokens

        // Evaluate string variables.
        var delimiterStack = new Stack<char>();

        //  Split string expression into parts.
        for (int i = 0; i < expression.Length; i++)
        {
            // For every character
            char character = expression[i]; // TEMP: Consider replacing this with a byte read, instead.

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
                if (double.TryParse(number, out _))
                    output.Enqueue(number);
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
                    ParseToken(unary, i, ref operators, ref output);
                }
                else
                {
                    // Binary operator
                    ParseToken(character.ToString(), i, ref operators, ref output);
                }
            }
            // Brackets are special characters.
            else if (character.IsBracket())
            {
                // This checks the brackets to the bracket stack, which is used to verify that the appropriate number of
                //  brackets are present in the expression.
                switch (character)
                {
                    // Determine if expression contains an adequate number of brackets of any kind.
                    case '(':
                    case '[':
                    case '{':
                        delimiterStack.Push(character);
                        break;
                    case ')':
                        if (delimiterStack.Count == 0 || delimiterStack.Pop() != '(')
                            throw new SyntaxErrorException(
                                $"Invalid \'()\' delimiters in \"{expression}\" from {trace}.");
                        break;
                    case ']':
                        if (delimiterStack.Count == 0 || delimiterStack.Pop() != '[')
                            throw new SyntaxErrorException(
                                $"Invalid \'[]\' delimiters in \"{expression}\" from {trace}.");
                        break;
                    case '}':
                        if (delimiterStack.Count == 0 || delimiterStack.Pop() != '{')
                            throw new SyntaxErrorException(
                                $"Invalid \'{{}}\' delimiters in \"{expression}\" from {trace}.");
                        break;
                }

                ParseToken(character.ToString(), i, ref operators, ref output);
            }
            // Character belongs to a string variable.
            // This will read the entire variable, turn it into a token, then will attempt to match
            //  this token with any handles in the Statistics List provided. This being done on-the-fly is for
            //  performance; avoiding the overhead of string.Replace().
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

                // Exception - Statistics list is empty. Don't even bother.
                if (Statistics.Entries.Count == 0)
                    throw new EmptyStatisticsListException(
                        $"Variable \'{variable}\' exists in expression \'{expression}\' from {trace}, " +
                        $"but there are no statistics stored in the internal statistics list!");

                // Detecting if the {source} does NOT equal the {variable} here. Infinite looping is terrible, and a
                //  variable who has a modifier that reflects on the variable itself will inevitably cause issues
                //  with recursive calls. Do not confuse this with {trace}, which is for tracing paths, or from the
                //  confidence of another expression. This isn't exactly -perfect-, though! The edge case is that of
                //  a source being named the same as a variable within an expression. Since the source could potentially
                //  be a folder... it means one cannot name a variable the same name as its own folder, assuming that
                //  whatever passes this call also passes a file name without an extension. It's not too dangerous, but
                //  it is noted for whenever this comes up in the future. The solution would be to add some peripheral
                // checks against the source, or demanding a full Uri path, instead.
                // Attempt to nab a statistic. May not be 100% reliable without context.
                var statistic = Statistics.GetStatistic(variable, parent);

                // Exception - Failed to find statistic.
                if (statistic is null)
                    throw new MissingStatisticException(
                        $"Variable \'{variable}\', has no matching statistics from {trace} to get a value of!");

                // Calculate the statistic value.
                var number = Statistics.CalculateStatisticValue(statistic.Value, parent, visited);
                output.Enqueue(number.ToString(CultureInfo.InvariantCulture));
            }
        }

        #endregion

        // Final checkup on delimiters.
        if (delimiterStack.Count > 0)
            throw new SyntaxErrorException($"Invalid Delimiters for expression \'{expression}\'!");

        // Pop remaining operators
        while (operators.Count > 0)
        {
            string op = operators.Pop();
            if (op != "(") output.Enqueue(op);
        }

        // Calculate final result.
        return EvaluateRPN(output);
    }

    /// Parse all proper tokens that aren't immediately recognised as doubles. 
    private static void ParseToken(string token, int index, ref Stack<string> operators, ref Queue<string> output)
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
                    // If operator is -, but index is also zero, then it is obviously a unary.
                    if (op is "-" && index == 0)
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

    private static bool ShouldPop(string top, string current)
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