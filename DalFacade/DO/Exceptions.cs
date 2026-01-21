namespace DO;

[Serializable]
/// <summary>
/// Represents the dal does not exist exception component in this layer.
/// </summary>
public class DalDoesNotExistException : Exception
{
    public DalDoesNotExistException(string? message) : base(message) { }
}

[Serializable]
/// <summary>
/// Represents the dal already exists exception component in this layer.
/// </summary>
public class DalAlreadyExistsException : Exception
{
    public DalAlreadyExistsException(string? message) : base(message) { }
}

[Serializable]
/// <summary>
/// Represents the dal null reference exception component in this layer.
/// </summary>
public class DalNullReferenceException : Exception
{
    public DalNullReferenceException(string? message) : base(message) { }
}

[Serializable]
/// <summary>
/// Represents the dal XML file load create exception component in this layer.
/// </summary>
public class DalXMLFileLoadCreateException : Exception
{
    public DalXMLFileLoadCreateException(string? message) : base(message) { }
}

[Serializable]
/// <summary>
/// Represents the dal format exception component in this layer.
/// </summary>
public class DalFormatException : Exception
{
    public DalFormatException(string? message) : base(message) { }
}
