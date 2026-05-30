using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using static SKSSL.Extensions.StringHelpers;

namespace SKSSL.Mathematics;

/// <summary>
/// Algorithmically calculates values based on string input. This class was written to align with the
/// <a href="https://en.wikipedia.org/wiki/Shunting_yard_algorithm">Shunting Yard Algorithm</a>.
/// <remarks>This algorithm was manually implemented. I have no clue if this will work, or is most performant!</remarks>
/// </summary>
public static class ShuntingYard
{
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
    public static bool Evaluate(string expression, out float result, string source = "")
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

        // Calculate.


        return true;
    }

    private static (List<string> Tokens, List<int> StringIndices) ParseExpression(string expression)
    {
        List<string> tokens = [];
        List<int> stringVarIndices = [];

        //  Split string expression into parts.
        for (int i = 0; i < expression.Length; i++)
        {
            // For every character
            char character = expression[i];

            // If character is an expected, add to stack of string operators,
            if (char.IsDigit(character))
            {
                int start = i;

                while (i + 1 < expression.Length && char.IsDigit(expression[i + 1]))
                {
                    i++;
                }

                string number = expression[start..(i + 1)];
                tokens.Add(number);
            }
            // Character is an operator
            else if (character.IsOperator())
            {
                // If the next character is also an operator, then this is a unary / special operator.
                //  This handles unary operators in pairs. A set of four operators should be two unary ones.
                if (i + 1 < expression.Length && expression[i + 1].IsOperator())
                {
                    i++;
                    string unary = $"u{expression[i]}";
                    tokens.Add(unary);
                }
                else
                {
                    // Add operator plainly.
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
                //  TODO: Add support for variables that can have numbers so long as they are after a '_' character. 
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

        return (tokens, stringVarIndices);
    }

    /// Evaluates a set of tokens using a set of indices pointing to tokens that happen to be string variables.
    /// <returns>true if all string variables were evaluated correctly; false if otherwise.</returns>
    private static bool EvaluateExpressionStringVariables(
        List<int> stringVarIndices,
        List<string> tokens,
        out string faultyVariables)
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

            if (EvaluateVariableString(variable, out int value))
            {
                // Replace original variable's token entry with numerical value.
                tokens[stringIndex] = value.ToString();
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

    private static bool EvaluateVariableString(string variable, out int i)
    {
        // WIP: Complete this. Indexing game statistics. Probably retroactive w. Prototyping beforehand.
        i = -1;
        return false;
    }

    public static List<string> ToPostfix(string[] tokens)
    {
        var output = new List<string>();
        var operators = new Stack<string>();

        string? previous = null;

        foreach (var rawToken in tokens)
        {
            string token = rawToken;
            // WIP: Make sure this isn't buggy as all hell. Unaries are already evaluated earlier in the
            //  call-chain anyway.
            if (token == "-" && (previous == null || previous == "(" || previous.IsOperator()))
            {
                // Unary minus
                token = "u-";
            }

            if (double.TryParse(token, out _))
            {
                output.Add(token);
            }
            else
            {
                switch (token)
                {
                    case "(":
                        operators.Push(token);
                        break;
                    case ")":
                    {
                        while (operators.Count > 0 && operators.Peek() != "(")
                        {
                            output.Add(operators.Pop());
                        }

                        if (operators.Count == 0)
                            throw new Exception("Mismatched parentheses");

                        operators.Pop(); // Remove '('
                        break;
                    }
                    default:
                    {
                        if (Precedence.TryGetValue(token, out byte precedence))
                        {
                            while (
                                operators.Count > 0 &&
                                operators.Peek() != "(" &&
                                Precedence.TryGetValue(operators.Peek(), out byte p) &&
                                p >= precedence
                            )
                            {
                                output.Add(operators.Pop());
                            }

                            operators.Push(token);
                        }
                        else
                        {
                            throw new Exception($"Unknown token: {token}");
                        }

                        break;
                    }
                }
            }

            previous = token;
        }

        while (operators.Count > 0)
        {
            if (operators.Peek() == "(")
                throw new Exception("Mismatched parentheses");

            output.Add(operators.Pop());
        }

        return output;
    }
}