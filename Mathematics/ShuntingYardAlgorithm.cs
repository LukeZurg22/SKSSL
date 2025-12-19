namespace SKSSL.Mathematics;

/// <summary>
/// Algorithmically calculates values based on string input. This class was written to align with the
/// <a href="https://en.wikipedia.org/wiki/Shunting_yard_algorithm">Shunting Yard Algorithm</a>.
/// <remarks>This algorithm was manually implemented. I have no clue if this will work, or is most performant!</remarks>
/// </summary>
public abstract partial class ShuntingYardAlgorithm
{
    internal Stack<int> QUEUE = new();
    internal Stack<int> OPERATIONS = new();

    // Consider dynamically loading variables that have been pre-emptively parsed.

    // ERR: Flawed, expecting the user's input to be perfect. It also can't handle parenthesis or variable-binding.
    //  In fact, this method will likely need to be extracted, or generalized to a satisfactory degree.
    public void Calculate(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
       
        // WIP: This area is an active work-in-progress. Parsing for formulae are going to be handled in the registry system.
        
        // Declare input stack and fill it with collected strings.
        //var enumerable = possibleVariables as string[] ?? possibleVariables.ToArray();
        Stack<string> inputStack = new(/*parts.Length*/ 0);
        
        
        foreach (var part in parts) inputStack.Push(part);

        List<object> tokens = [];

        // Limitations to 255 possible brackets. Nobody is going to use that many in one formula!
        //  Should be ZERO by the time this is done parsing. 
        byte unclosedOpenBrackets = 0;

        // Whilst there are strings in the input stack to deal with
        while (inputStack.TryPeek(out _))
        {
            // Get the string part (Example: "word" or "-25" or "83" or '(' / ')' or "+ - * /"
            var part = inputStack.Pop();

            int partLength = part.Length;

            // If it's a single character
            if (partLength == 1)
            {
                var character = part[0];

                // This is vital to keeping track of brackets.
                switch (character)
                {
                    case '(':
                        unclosedOpenBrackets++;
                        tokens.Add(part);
                        continue;
                    case ')':
                    {
                        if (unclosedOpenBrackets == 0)
                        {
                            DustLogger.Log($"Invalid bracket count for input {input}", 1);
                        }

                        unclosedOpenBrackets--;
                        tokens.Add(part);
                        continue;
                    }
                }

                // Single-characters are fine. There isn't any special handling for lone operators here, so this
                //  part will rely on user-accuracy. Because of how this works, this supports spaces between brackets.
                if (character is '-' or '+' or '*' or '/' || char.IsNumber(character))
                    tokens.Add(part); // Numbers are okay!
                else if (char.IsLetter(character))
                {
                    DustLogger.Log(
                        $"Single-character variable detected \"{character}\" in \"{input}\"." +
                        $"This is pushing to stack anyway!", 1);
                    tokens.Add(part);
                }
            }
            // This part contains multiple characters, which should be parsed.
            else
            {
                // Clear out the first character.
                if (char.IsNumber(part[0]))
                {
                    DustLogger.Log($"First-character of a token part is a number, which is invalid syntax in {input}",
                        1);
                    return;
                }

                // Loop over all characters.
                for (int characterIndex = 0; characterIndex < partLength; characterIndex++)
                {
                }

                continue;
            }
        }

        Stack<object> intermediateStack = new();
    }

    void ProcessPart()
    {
        // Read each character.
        int strLength = 0;
        string part = "";
        List<object> tokens = [];
        for (int i = 0; i < strLength; i++)
        {
            char character = part[i];
            // If brackets, add to the stack. Algorithm will handle it later.
            if (character is '(' or ')')
            {
                tokens.Add(part);
            }
            // If character is a negative sign, make sure it's not a unary operator.
            else if (character == '-')
            {
                // Get next part
                var nextIndex = part.IndexOf(character, i + 1);
                if (nextIndex == -1) // There is no next part.
                {
                    var nextCharacter = part[i + 1];
                }
            }
            else if (float.TryParse([character], out var num))
            {
                Console.Write("");
            }
            else if (true)
            {
                Console.Write("");
            }


            //var number = double.Parse(climateString, numberFormat);
            //Operate(tokens.ToArray());
        }
    }

    bool IsUnaryMinus(int index, List<string> tokens)
    {
        if (index == 0) return true;
        var prev = tokens.LastOrDefault();
        return prev is "(" or "+" or "-" or "*" or "/";
    }

    public static bool GetValueFromString(string input, out float value)
    {
        value = 0f;
        return true;
    }

    /// <summary>
    /// This implementation does not implement composite functions, functions with a variable number of arguments, or unary operators.
    /// </summary>
    public void Operate(Stack<object> tokens)
    {
        Queue<object> OUTPUT_QUEUE = new Queue<object>();
        Stack<object> OPERATOR_STACK = new Stack<object>();
        // https://en.wikipedia.org/wiki/Shunting_yard_algorithm

        // while there are tokens to be read
        while (tokens.TryPeek(out var token))
        {
            /*
             read a token
             if the token is:
             - a number:
                 put it into the output queue
             - a function:
                 push it onto the operator stack
             - an operator o1:
                 while (
                     there is an operator o2 at the top of the operator stack which is not a left parenthesis,
                     and (o2 has greater precedence than o1 or (o1 and o2 have the same precedence and o1 is left-associative))
                 ):
                     pop o2 from the operator stack into the output queue
                 push o1 onto the operator stack
             - a ",":
                 while the operator at the top of the operator stack is not a left parenthesis:
                      pop the operator from the operator stack into the output queue
             - a left parenthesis (i.e. "("):
                 push it onto the operator stack
             - a right parenthesis (i.e. ")"):
                 while the operator at the top of the operator stack is not a left parenthesis:
                     {assert the operator stack is not empty}
                     /* If the stack runs out without finding a left parenthesis, then there are mismatched parentheses. * /
                     pop the operator from the operator stack into the output queue
                 {assert there is a left parenthesis at the top of the operator stack}
                 pop the left parenthesis from the operator stack and discard it
                 if there is a function token at the top of the operator stack, then:
                     pop the function from the operator stack into the output queue
             */
        }


        /*
:
    read a token
    if the token is:
    - a number:
        put it into the output queue
    - a function:
        push it onto the operator stack
    - an operator o1:
        while (
            there is an operator o2 at the top of the operator stack which is not a left parenthesis,
            and (o2 has greater precedence than o1 or (o1 and o2 have the same precedence and o1 is left-associative))
        ):
            pop o2 from the operator stack into the output queue
        push o1 onto the operator stack
    - a ",":
        while the operator at the top of the operator stack is not a left parenthesis:
             pop the operator from the operator stack into the output queue
    - a left parenthesis (i.e. "("):
        push it onto the operator stack
    - a right parenthesis (i.e. ")"):
        while the operator at the top of the operator stack is not a left parenthesis:
            {assert the operator stack is not empty}
            /* If the stack runs out without finding a left parenthesis, then there are mismatched parentheses. * /
            pop the operator from the operator stack into the output queue
        {assert there is a left parenthesis at the top of the operator stack}
        pop the left parenthesis from the operator stack and discard it
        if there is a function token at the top of the operator stack, then:
            pop the function from the operator stack into the output queue
/* After the while loop, pop the remaining items from the operator stack into the output queue. * /
while there are tokens on the operator stack:
    /* If the operator token on the top of the stack is a parenthesis, then there are mismatched parentheses. * /
    {assert the operator on top of the stack is not a (left) parenthesis}
    pop the operator from the operator stack onto the output queue
         */
    }

    static Dictionary<string, double> variables =
        new Dictionary<string, double>
        {
            ["example_statistic"] = 4
        };

    static double GetValue(string token)
    {
        if (double.TryParse(token, out var number))
        {
            return number;
        }

        if (variables.TryGetValue(token, out var val))
        {
            return val;
        }

        throw new Exception($"Unknown token {token}");
    }

    public static double
        Evaluate(string[] tokens) // ERR: This AI-Slop is invalid hot garbage. But it's inspirational.
    {
        var stack = new Stack<double>();
        foreach (var token in tokens)
        {
            if (token is "+" or "-" or "*" or "/") // IF TOKEN IS OPERATOR
            {
                double right = stack.Pop();
                double left = stack.Pop();
                stack.Push(token switch
                {
                    "+" => left + right,
                    "-" => left - right,
                    "*" => left * right,
                    "/" => left / right,
                    _ => throw new Exception("Invalid operator")
                });
            }
            else
            {
                stack.Push(GetValue(token)); // IF TOKEN IS NOT OPERATOR (CONSIDER BRACKETS & UNARY OPERATIONS)
            }
        }

        if (stack.Count != 1)
        {
            throw new InvalidOperationException("Invalid RPN expression");
        }

        return stack.Pop();
    }

}