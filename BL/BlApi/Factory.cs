namespace BlApi;

/// <summary>
/// Represents the factory component in this layer.
/// </summary>
public static class Factory
{
    public static IBl Get() => new BlImplementation.Bl();
}
