using DS4WinWPF.DS4Control.DTOXml;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace DS4WindowsTests
{
    /// <summary>
    /// Serialises an <see cref="AppSettingsDTO"/> the way <c>BackingStore</c>
    /// does and reads it back.
    ///
    /// <para>Shared because more than one fixture needs to prove that a setting
    /// survives a save. Two private copies of this plumbing would be two places
    /// for the serializer configuration - notably <c>SerializeAppAttrs</c>, which
    /// changes the shape of the document - to drift apart, and a settings test
    /// that round-trips differently from the product is a test that proves
    /// nothing.</para>
    /// </summary>
    internal static class AppSettingsRoundTrip
    {
        internal static AppSettingsDTO Write(AppSettingsDTO source)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(AppSettingsDTO));
            using StringWriter writer = new StringWriter();
            source.SerializeAppAttrs = false;
            serializer.Serialize(writer, source,
                new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty }));
            return Read(writer.ToString());
        }

        internal static AppSettingsDTO Read(string xml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(AppSettingsDTO));
            using StringReader reader = new StringReader(xml);
            return (AppSettingsDTO)serializer.Deserialize(reader);
        }
    }
}
