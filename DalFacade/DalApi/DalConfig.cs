namespace DalApi;
using System.Xml.Linq;
 
/// <summary>
/// Represents the dal config component in this layer.
/// </summary>
static class DalConfig
{
    /// <summary>
    /// internal PDS class
    /// </summary>
    internal record DalImplementation
    (
        string Package,   // package/dll name
        string Namespace, // namespace where DAL implementation class is contained in
        string Class   // DAL implementation class name
    );

    /// <summary>
    /// Stores the s dal name value.
    /// </summary>
    internal static string s_dalName;
    internal static Dictionary<string, DalImplementation> s_dalPackages;

    static DalConfig()
    {
        XElement dalConfig = XElement.Load(@"..\xml\dal-config.xml") ?? throw new DalConfigException("dal-config.xml file is not found");

        s_dalName = dalConfig.Element("dal")?.Value ?? throw new DalConfigException("<dal> element is missing");

        var packages = dalConfig.Element("dal-packages")?.Elements() ?? throw new DalConfigException("<dal-packages> element is missing");
        s_dalPackages = (from item in packages
                         let pkg = item.Value
                         let ns = item.Attribute("namespace")?.Value ?? "Dal"
                         let cls = item.Attribute("class")?.Value ?? pkg
                         select (item.Name, new DalImplementation(pkg, ns, cls))
                        ).ToDictionary(p => "" + p.Name, p => p.Item2);
    }
}
 
[Serializable]
/// <summary>
/// Represents the dal config exception component in this layer.
/// </summary>
public class DalConfigException : Exception
{
	public DalConfigException(string msg) : base(msg) { }
	public DalConfigException(string msg, Exception ex) : base(msg, ex) { }
}
