namespace BO;

[Serializable]
public class BLTemporaryNotAvailableException : Exception
{
    public BLTemporaryNotAvailableException(string? message) : base(message) { }
    public BLTemporaryNotAvailableException(string message,Exception innerException) : base(message, innerException) { }
}

[Serializable]
public class BLAlreadyExistsException : Exception
{
    public BLAlreadyExistsException(string? message) : base(message) { }
    public BLAlreadyExistsException(string message, Exception innerException) : base(message, innerException) { }
}

[Serializable]
public class BLNotFoundException : Exception
{
    public BLNotFoundException(string? message) : base(message) { }
    public BLNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}