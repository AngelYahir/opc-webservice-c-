using System;
using System.Collections.Generic;
using System.Net;
using System.Web.Http;
using System.Xml;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;
using System.IO;
using System.Net.Http;
using System.Text;



namespace EBOopc.Controllers
{
    [RoutePrefix("api/data")]
    public class DataController : ApiController
    {
        public class OpcData
        {
            public string ItemId { get; set; }
            public object Value { get; set; }
        }

        [HttpGet]
        [Route("")]
        public HttpResponseMessage Get()
        {
            try
            {
                var opcDataList = ReadOpcValues();

                var xmlDocument = new XmlDocument();
                var rootElement = xmlDocument.CreateElement("ArrayOfOpcDataResponse");

                foreach (var opcData in opcDataList)
                {
                    int firstDotIndex = opcData.ItemId.IndexOf('.');
                    string itemName = firstDotIndex != -1 ? opcData.ItemId.Substring(firstDotIndex + 1) : opcData.ItemId;

                    var opcElementName = SanitizeXmlTagName(itemName);
                    var opcElement = xmlDocument.CreateElement(opcElementName);
                    opcElement.InnerText = opcData.Value.ToString();
                    rootElement.AppendChild(opcElement);
                }

                xmlDocument.AppendChild(rootElement);


                var xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
                xmlDocument.InsertBefore(xmlDeclaration, xmlDocument.DocumentElement);

                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "    ",
                    NewLineChars = "\r\n"
                };

                using (var stringWriter = new StringWriter())
                {
                    using (var xmlTextWriter = XmlWriter.Create(stringWriter, settings))
                    {
                        xmlDocument.WriteTo(xmlTextWriter);
                    }

                    var formattedXml = stringWriter.ToString();

                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(formattedXml, Encoding.UTF8, "application/xml")
                    };

                    return response;
                }
            }
            catch (System.Exception ex)
            {
                var errorResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                errorResponse.Content = new StringContent(ex.Message, Encoding.UTF8, "text/plain");
                return errorResponse;
            }
        }


        private string SanitizeXmlTagName(string input)
        {
            return input.Replace(" ", "_");
        }


        private List<OpcData> ReadOpcValues()
        {
            List<OpcData> opcDataList = new List<OpcData>();
            System.Uri url = TitaniumAS.Opc.Client.Common.UrlBuilder.Build("Bosch.FPA5000OpcServer.1");

            using (var server = new OpcDaServer(url))
            {
                server.Connect();
                var browser = new OpcDaBrowserAuto(server);
                List<OpcDaItemDefinition> definitions = new List<OpcDaItemDefinition>();

                BrowseChildren(browser, definitions);
                OpcDaGroup group = server.AddGroup("mygroup");
                group.IsActive = true;
                OpcDaItemResult[] results = group.AddItems(definitions);

                OpcDaItemValue[] values = group.Read(group.Items);

                foreach (OpcDaItemValue value in values)
                {
                    OpcData opcData = new OpcData
                    {
                        ItemId = value.Item.ItemId,
                        Value = value.Value
                    };

                    opcDataList.Add(opcData);
                }
            }

            return opcDataList;
        }

        private void BrowseChildren(IOpcDaBrowser browser, List<OpcDaItemDefinition> definitions, string itemId = null, int indent = 0)
        {
            OpcDaBrowseElement[] elements = browser.GetElements(itemId);

            try
            {
                foreach (OpcDaBrowseElement element in elements)
                {
                    var definition = new OpcDaItemDefinition
                    {
                        ItemId = element.ItemId,
                        IsActive = true
                    };

                    definitions.Add(definition);

                    if (!element.HasChildren)
                        continue;

                    BrowseChildren(browser, definitions, element.ItemId, indent + 2);
                }
            }
            catch (System.Exception e)
            {
                // Handle the exception if necessary.
            }
        }
    }
}
