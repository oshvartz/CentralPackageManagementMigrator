using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace CentralPackageManagementMigrator.Runner
{
    public static class XElementExtensions
    {
        public static void SaveWithoutXmlDeclaration(this XElement element, string fileName)
        {
            using var writer = XmlWriter.Create(fileName, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true });
            element.Save(writer);
        }
    }
}
