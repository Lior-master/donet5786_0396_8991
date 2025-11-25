namespace Helpers;

internal static class Tools
{
    public static string ToStringProperty<T>(this T t)
    {
        if (t is null)
            return string.Empty;

        var type = typeof(T);
        var props = type.GetProperties();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(type.Name + " {");

        foreach (var prop in props)
        {
            var value = prop.GetValue(t);
            sb.AppendLine($"  {prop.Name} = {value}");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

}
