using System.Reflection;
using System.Xml.XPath;

namespace EI.API.Service.Rest.Helpers.Documentation;

public static class DocumentationLoader
{
    public static XPathDocument? GetXmlDocs(Assembly assembly, string documentationResourceName)
    {
        var resourceNames = assembly.GetManifestResourceNames();
        var resourceName = resourceNames.Single(n => n.EndsWith(documentationResourceName));

        using var stream = assembly.GetManifestResourceStream(resourceName);
        return stream == null ? null : new XPathDocument(stream);
    }
}
