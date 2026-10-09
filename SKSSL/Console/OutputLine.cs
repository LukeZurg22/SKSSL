namespace SKSSL.Console
{
    internal enum OutputLineType
    {
        Command,
        Output
    }

    internal class OutputLine
    {
        public string Output { get; set; }
        public OutputLineType Type { get; set; }

        public OutputLine(string output, OutputLineType type)
        {
            Output = output;
            Type = type;
        }

        public override string ToString() => Output;
    }
}
