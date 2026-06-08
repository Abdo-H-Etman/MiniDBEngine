namespace MiniDB.Core.Exceptions;

public class DivisionByZeroException : Exception
{
    public DivisionByZeroException(string message) : base(message)
    {
    }
}