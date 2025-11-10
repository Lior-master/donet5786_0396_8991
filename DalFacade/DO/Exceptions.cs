using System.Text.Json.Serialization;

namespace DO;
[Serializable]
public class DalDoesNotExistException : Exception
{
    public DalDoesNotExistException(string? message) : base(message) { }
}

[Serializable]
public class DalAlreadyExistsException : Exception
{
    public DalAlreadyExistsException(string? message) : base(message) { }
}

[Serializable]
public class DalNullReferenceException : Exception
{
    public DalNullReferenceException(string? message) : base(message) { }
}
