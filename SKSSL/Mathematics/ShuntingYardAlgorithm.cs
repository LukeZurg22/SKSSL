using System;
using System.Collections.Generic;

namespace SKSSL.Mathematics;

/// <summary>
/// Algorithmically calculates values based on string input. This class was written to align with the
/// <a href="https://en.wikipedia.org/wiki/Shunting_yard_algorithm">Shunting Yard Algorithm</a>.
/// <remarks>This algorithm was manually implemented. I have no clue if this will work, or is most performant!</remarks>
/// </summary>

public static class ShuntingYard
{
    private static readonly Dictionary<string, byte> Precedence = new()
    {
        { "+", 1 },
        { "-", 1 },
        { "*", 2 },
        { "/", 2 },
        { "^", 3 }
    };

    public static string Evaluate(string[] expression)
    {
        

        return "";
    }

    private static void ParseStringToken()
    {
        
    }
    
    public static List<string> ToPostfix(string[] tokens)
    {
        var output = new List<string>();
        var operators = new Stack<string>();

        foreach (var token in tokens)
        {
            if (double.TryParse(token, out _))
            {
                output.Add(token);
            }
            else switch (token)
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

        while (operators.Count > 0)
        {
            if (operators.Peek() == "(")
                throw new Exception("Mismatched parentheses");

            output.Add(operators.Pop());
        }

        return output;
    }
}