namespace BO;

[Serializable]
public class BLTemporaryNotAvailableException : Exception
{
    public BLTemporaryNotAvailableException(string? message) : base(message) { }
    public BLTemporaryNotAvailableException(string message,Exception innerException) : base(message, innerException) { }
}
