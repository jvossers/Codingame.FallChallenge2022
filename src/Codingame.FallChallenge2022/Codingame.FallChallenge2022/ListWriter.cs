namespace Codingame.FallChallenge2022;

public class ListWriter
{
    public int Position;
    public List<string> InputLines { get; }

    public ListWriter(List<string> inputLines)
    {
        InputLines = inputLines;
        Position = 0;
    }

    public string ReadLine()
    {
        var line = InputLines[Position];
        Position++;
        return line;
    }
}