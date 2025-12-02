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
    

    // Accept nullable parameters and return null if any parameter is null.
    public static double BirdDistance(double? lat1, double? lon1, double? lat2, double? lon2)
    {
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue)
            return -1;

        const double R = 6371; // earth radius in kilometers

        double dLat = ToRadians(lat2.Value - lat1.Value);
        double dLon = ToRadians(lon2.Value - lon1.Value);

        double la1 = ToRadians(lat1.Value);
        double la2 = ToRadians(lat2.Value);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(la1) * Math.Cos(la2) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c; // Distance in kilometers
    }

    private static double ToRadians(double angle)
    {
        return angle * Math.PI / 180.0;
    }

}
