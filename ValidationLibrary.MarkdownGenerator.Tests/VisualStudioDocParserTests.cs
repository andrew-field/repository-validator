using System.Xml.Linq;
using NUnit.Framework;

namespace ValidationLibrary.MarkdownGenerator.Tests
{
    public class VisualStudioDocParserTests
    {
        [Test]
        public void ParseXmlComment_ReturnsEmptyForNoComments()
        {
            Assert.IsEmpty(VisualStudioDocParser.ParseXmlComment(XDocument.Parse("<?xml version =\"1.0\"?><doc/>")));

            var xml = "<?xml version=\"1.0\"?>" +
                    "<doc>" +
                    "<members>" +
                    "</members>" +
                    "</doc>";
            Assert.IsEmpty(VisualStudioDocParser.ParseXmlComment(XDocument.Parse(xml)));
        }

        [Test]
        public void ParseXmlComment_ReturnsSummary()
        {
            Assert.IsEmpty(VisualStudioDocParser.ParseXmlComment(XDocument.Parse("<?xml version =\"1.0\"?><doc/>")));

            var xml = "<?xml version=\"1.0\"?>" +
                    "<doc>" +
                    "<members>" +
                    "<member name=\"T:ValidationLibrary.Rules.HasLicenseRule\">" +
                    "<summary>" +
                    "Comment here" +
                    "</summary>" +
                    "</member>" +
                    "</members>" +
                    "</doc>";
            var result = VisualStudioDocParser.ParseXmlComment(XDocument.Parse(xml));
            Assert.AreEqual(1, result.Length);

            Assert.AreEqual("HasLicenseRule", result[0].MemberName);
            Assert.AreEqual("Comment here", result[0].Summary);
        }

        [Test]
        public void ParseXmlComment_MissingSummaryDoesntBreak()
        {
            Assert.IsEmpty(VisualStudioDocParser.ParseXmlComment(XDocument.Parse("<?xml version =\"1.0\"?><doc/>")));

            var xml = "<?xml version=\"1.0\"?>" +
                    "<doc>" +
                    "<members>" +
                    "<member name=\"T:ValidationLibrary.Rules.HasLicenseRule\">" +
                    "</member>" +
                    "</members>" +
                    "</doc>";
            var result = VisualStudioDocParser.ParseXmlComment(XDocument.Parse(xml));
            Assert.AreEqual(1, result.Length);

            Assert.AreEqual("HasLicenseRule", result[0].MemberName);
            Assert.AreEqual("", result[0].Summary);
        }
    }
}
