namespace Dal;

using DO;
using System;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

static class XmlTools
{
    const string s_xmlDir = @"..\xml\";
    private static readonly object s_fileLock = new object();

    static XmlTools()
    {
        if (!Directory.Exists(s_xmlDir))
            Directory.CreateDirectory(s_xmlDir);
    }

    #region SaveLoadWithXMLSerializer
    public static void SaveListToXMLSerializer<T>(List<T> list, string xmlFileName) where T : class
    {
        string xmlFilePath = s_xmlDir + xmlFileName;
        
        lock (s_fileLock)  // Add lock
        {
            try
            {
                using FileStream file = new(xmlFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                new XmlSerializer(typeof(List<T>)).Serialize(file, list);
            }
            catch (Exception ex)
            {
                throw new DalXMLFileLoadCreateException($"fail to create xml file: {s_xmlDir + xmlFileName}, {ex.Message}");
            }
        }
    }

    public static List<T> LoadListFromXMLSerializer<T>(string xmlFileName) where T : class
    {
        string xmlFilePath = s_xmlDir + xmlFileName;

        lock (s_fileLock)  // Add lock
        {
            try
            {
                if (!File.Exists(xmlFilePath)) return new();
                using FileStream file = new(xmlFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                XmlSerializer x = new(typeof(List<T>));
                return x.Deserialize(file) as List<T> ?? new();
            }
            catch (Exception ex)
            {
                throw new DalXMLFileLoadCreateException($"fail to load xml file: {xmlFilePath}, {ex.Message}");
            }
        }
    }
    #endregion

    #region SaveLoadWithXElement
    public static void SaveListToXMLElement(XElement rootElem, string xmlFileName)
    {
        string xmlFilePath = s_xmlDir + xmlFileName;

        lock (s_fileLock)
        {
            try
            {
                rootElem.Save(xmlFilePath);
            }
            catch (Exception ex)
            {
                throw new DalXMLFileLoadCreateException($"fail to create xml file: {s_xmlDir + xmlFileName}, {ex.Message}");
            }
        }
    }
    public static XElement LoadListFromXMLElement(string xmlFileName)
    {
        string xmlFilePath = s_xmlDir + xmlFileName;

        lock (s_fileLock)
        {
            try
            {
                if (File.Exists(xmlFilePath))
                    return XElement.Load(xmlFilePath);
                XElement rootElem = new(xmlFileName);
                rootElem.Save(xmlFilePath);
                return rootElem;
            }
            catch (Exception ex)
            {
                throw new DalXMLFileLoadCreateException($"fail to load xml file: {s_xmlDir + xmlFileName}, {ex.Message}");
            }
        }
    }
    #endregion

    #region XmlConfig
    public static int GetAndIncreaseConfigIntVal(string xmlFileName, string elemName)
    {
        XElement root = XmlTools.LoadListFromXMLElement(xmlFileName);
        int nextId = root.ToIntNullable(elemName) ?? throw new DalFormatException($"can't convert:  {xmlFileName}, {elemName}");
        root.Element(elemName)?.SetValue((nextId + 1).ToString());
        XmlTools.SaveListToXMLElement(root, xmlFileName);
        return nextId;
    }
    public static int GetConfigIntVal(string xmlFileName, string elemName)
    {
        XElement root = XmlTools.LoadListFromXMLElement(xmlFileName);
        int num = root.ToIntNullable(elemName) ?? throw new DalFormatException($"can't convert:  {xmlFileName}, {elemName}");
        return num;
    }
    public static DateTime GetConfigDateVal(string xmlFileName, string elemName)
    {
        XElement root = XmlTools.LoadListFromXMLElement(xmlFileName);
        DateTime dt = root.ToDateTimeNullable(elemName) ?? throw new DalFormatException($"can't convert:  {xmlFileName}, {elemName}");
        return dt;
    }
    public static void SetConfigIntVal(string xmlFileName, string elemName, int elemVal)
    {
        XElement root = XmlTools.LoadListFromXMLElement(xmlFileName);
        root.Element(elemName)?.SetValue((elemVal).ToString());
        XmlTools.SaveListToXMLElement(root, xmlFileName);
    }
    public static void SetConfigDateVal(string xmlFileName, string elemName, DateTime elemVal)
    {
        XElement root = XmlTools.LoadListFromXMLElement(xmlFileName);
        root.Element(elemName)?.SetValue((elemVal).ToString());
        XmlTools.SaveListToXMLElement(root, xmlFileName);
    }
    public static double? GetConfigDoubleVal(string xmlFileName, string elemName)
    {
        XElement root = XmlTools.LoadListFromXMLElement(xmlFileName);
        double? num = root.ToDoubleNullable(elemName) ?? throw new DalFormatException($"can't convert:  {xmlFileName}, {elemName}");
        return num;
    }
    public static void SetConfigDoubleVal(string xmlFileName, string elemName, double elemVal)
    {
        XElement root = XmlTools.LoadListFromXMLElement(xmlFileName);
        root.Element(elemName)?.SetValue((elemVal).ToString());
        XmlTools.SaveListToXMLElement(root, xmlFileName);
    }
    public static string? GetConfigStringVal(string xmlFileName, string elemName)
    {
        XElement root = XmlTools.LoadListFromXMLElement(xmlFileName);
        string? str = (string?)root.Element(elemName) ?? throw new DalFormatException($"can't convert:  {xmlFileName}, {elemName}");
        return str;
    }
    public static void SetConfigStringVal(string xmlFileName, string elemName, string elemVal)
    {
        XElement root = XmlTools.LoadListFromXMLElement(xmlFileName);
        root.Element(elemName)?.SetValue(elemVal);
        XmlTools.SaveListToXMLElement(root, xmlFileName);
    }
    #endregion


    #region ExtensionFuctions
    public static T? ToEnumNullable<T>(this XElement element, string name) where T : struct, Enum =>
        Enum.TryParse<T>((string?)element.Element(name), out var result) ? (T?)result : null;
    public static DateTime? ToDateTimeNullable(this XElement element, string name) =>
        DateTime.TryParse((string?)element.Element(name), out var result) ? (DateTime?)result : null;
    public static double? ToDoubleNullable(this XElement element, string name) =>
        double.TryParse((string?)element.Element(name), out var result) ? (double?)result : null;
    public static int? ToIntNullable(this XElement element, string name) =>
        int.TryParse((string?)element.Element(name), out var result) ? (int?)result : null;
    #endregion
}