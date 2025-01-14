// <Copyright file="Program.cs" company="MentorSystems">
//
//     Copyright (c) 2025 MentorSystems. All rights reserved.
//
// This code is licensed under the MIT License (MIT).
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// </Copyright>

namespace HPKISigner
{
    using System.Xml.Linq;

    internal sealed class Program
    {
        // Declaration of namespace
        private static readonly XNamespace ds = XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");
        private static readonly XNamespace xades = XNamespace.Get("http://uri.etsi.org/01903/v1.3.2#");

        // Unique ID
        private static readonly string UniqueID = Guid.NewGuid().ToString().Replace("-", string.Empty);

        // Certification
        private static readonly byte[] CertBytes = CertClass.GetCert();

        static void Main(string[] args)
        {
            string prescriptionCSVFilePath = args[0];
            string signedXMLFilePath = args[1];
            string userPin = args[2];

            // Make Frame
            XDocument xDocumentFrame = MakeFrameXml(prescriptionCSVFilePath);

            // Add reference to PrescriptionDocumtns, SignedProperties and KeyInfo
            XDocument? xDocumentAddRef = AddReference(xDocumentFrame);

            // Finally, add signature
            XDocument? xDocumentSigned = AddSignature(xDocumentAddRef, userPin);

            // Save the XML to signedXMLFilePath in one line
            if (xDocumentSigned != null)
            {
                xDocumentSigned.Save(signedXMLFilePath, SaveOptions.DisableFormatting);
            }
        }

        private static XDocument MakeFrameXml(string prescriptionCSVFilePath)
        {
            // Create Frame Xml

            // Base64 encoded PrescriptionDocument
            string base64EncodedPrescriptionDocument = Convert.ToBase64String(File.ReadAllBytes(prescriptionCSVFilePath));

            XDocument xDocument = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(
                    "Document",
                    new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                    new XAttribute("id", "Document"),
                    new XAttribute(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "noNamespaceSchemaLocation", "EP.xsd"),
                    new XElement(
                        "Prescription",
                        new XElement(
                            "PrescriptionManagement",
                            new XAttribute("id", "PrescriptionManagement"),
                            new XElement("Version", new XAttribute("Value", "EPS1.0"))),
                        new XElement(
                            "PrescriptionDocument",
                            new XAttribute("id", "PrescriptionDocument"),
                            base64EncodedPrescriptionDocument),
                        new XElement(
                            "PrescriptionSign",
                            new XElement(
                                ds + "Signature",
                                new XAttribute(XNamespace.Xmlns + "ds", ds),
                                new XAttribute("Id", "PrescriptionSign"),
                                    new XElement(
                                        ds + "SignedInfo",
                                        new XElement(
                                            ds + "CanonicalizationMethod",
                                            new XAttribute("Algorithm", "http://www.w3.org/2001/10/xml-exc-c14n#")),
                                        new XElement(
                                            ds + "SignatureMethod",
                                            new XAttribute("Algorithm", "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256"))
                                    // Reference to PrescriptionDcoument
                                    // Reference to KeyInfo
                                    // Reference to SignedProperties
                                    ),
                                    // SignatureValue
                                    GenerateKeyInfo(),
                                    GenerateObject()
                            )
                        )
                    )
                )
            );
            return xDocument;
        }

        private static XDocument? AddReference(XDocument xDocument)
        {
            // From given XDocument, Generate Reference to PrescriptionDcoument, SignedProperties and KeyInfo.
            var elementSignedInfo = xDocument?.Descendants(ds + "SignedInfo").FirstOrDefault();

            // 1.Insert "Reference to PrescriptionDcoument" into SignedInfo.
            var elementPrescriptionDocument = xDocument?.Descendants("PrescriptionDocument").FirstOrDefault();
            if (elementPrescriptionDocument != null)
            {
                var referenceXmlToPrescriptionDocument =
                    GenerateReference(
                        elementPrescriptionDocument,
                        "#PrescriptionDocument",
                        $"id-ref-PrescriptionDocument");

                elementSignedInfo?.Add(referenceXmlToPrescriptionDocument);
            }

            // 2.Insert "Reference to SignedProperties" into SignedInfo.
            var elementSignedProperties = xDocument?.Descendants(xades + "SignedProperties").FirstOrDefault();
            if (elementSignedProperties != null)
            {
                var referenceXmlToSignedProperties =
                    GenerateReference(
                        elementSignedProperties,
                        $"#xades-id-{UniqueID}",
                        null,
                        "http://uri.etsi.org/01903#SignedProperties"
                    );
                elementSignedInfo?.Add(referenceXmlToSignedProperties);
            }

            // 3.Insert "Reference to KeyInfo" into SignedInfo.
            var elementKeyInfo = xDocument?.Descendants(ds + "KeyInfo").FirstOrDefault();
            if (elementKeyInfo != null)
            {
                var referenceXmlToKeyInfo =
                   GenerateReference(
                           elementKeyInfo,
                           $"#keyInfo-id-{UniqueID}"
                   );
                elementSignedInfo?.Add(referenceXmlToKeyInfo);
            }

            return xDocument;
        }

        private static XDocument? AddSignature(XDocument? xDocument, string userPin)
        {
            // 1.After inserting references into elementSignedInfo, get elementSignedInfo again, then calculate its DigestValue and sign it with a private key.
            string signatureValue = string.Empty;
            var elementSignedInfo = xDocument?.Descendants(ds + "SignedInfo").FirstOrDefault();
            if (elementSignedInfo != null)
            {
                var digestValue = HelperClass.GetDigestValue(elementSignedInfo);
                var signatureValyeByte = CertClass.Sign(digestValue, userPin);
                signatureValue = Convert.ToBase64String(signatureValyeByte);

                Console.WriteLine(CertClass.Verify(digestValue, signatureValyeByte) ? "Veiry Succeeded" : "Verify Failed");
            }

            // 2.Insert signatureValue into Signature element.
            var elementSignature = xDocument?.Descendants(ds + "Signature").FirstOrDefault();
            if (elementSignature != null)
            {
                // Find the KeyInfo element
                var elementKeyInfo = elementSignature.Element(ds + "KeyInfo");
                if (elementKeyInfo != null)
                {
                    // Insert SignatureValue before KeyInfo
                    elementKeyInfo.AddBeforeSelf(
                    new XElement(
                        ds + "SignatureValue",
                        new XAttribute("Id", $"value-id-{UniqueID}"),
                        signatureValue));
                }
            }

            return xDocument;
        }

        /// <summary>
        /// <see cref="GenerateKeyInfo"/> Generate KeyInfo Element.
        /// </summary>
        /// <returns>KeyInfo Element.</returns>
        private static XElement GenerateKeyInfo()
        {
            return new XElement(
                ds + "KeyInfo",
                new XAttribute("Id", $"keyInfo-id-{UniqueID}"),
                new XElement(
                    ds + "X509Data",
                    new XElement(
                        ds + "X509Certificate",
                        Convert.ToBase64String(CertBytes))));
        }

        /// <summary>
        /// <see cref="GenerateObject"/> Generate Object Element.
        /// </summary>
        /// <returns>Object Element.</returns>
        private static XElement GenerateObject()
        {
            // Compute the SHA-256 hash of the certBytes
            byte[] certHash = HelperClass.ComputeSha256Hash(CertBytes);

            // Convert the hash to a Base64-encoded string
            string certDigestValue = Convert.ToBase64String(certHash);

            return new XElement(
                ds + "Object",
                new XElement(
                    xades + "QualifyingProperties",
                    new XAttribute(XNamespace.Xmlns + "xades", xades),
                    new XAttribute("Target", "#PrescriptionSign"),
                    new XElement(
                        xades + "SignedProperties",
                        new XAttribute("Id", $"xades-id-{UniqueID}"),
                        new XElement(
                            xades + "SignedSignatureProperties",
                            new XElement(
                                xades + "SigningCertificateV2",
                                new XElement(
                                    xades + "Cert",
                                    new XElement(
                                        xades + "CertDigest",
                                        new XElement(
                                            ds + "DigestMethod",
                                            new XAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#sha256")
                                        ),
                                        new XElement(ds + "DigestValue", certDigestValue)
                                    ),
                                    new XElement(xades + "IssuerSerialV2", HelperClass.GetIssuerSerialV2(CertBytes))
                                )
                            )
                        )
                    )
                )
            );
        }

        /// <summary>
        /// <see cref="GenerateReference"/> Generate Reference Element with its target's digestvalue.
        /// </summary>
        /// <param name="targetElement">targetElement to be exc-14n and digested.</param>
        /// <param name="uri">uri. This represents target Element and MUST.</param>
        /// <param name="id">id.</param>
        /// <param name="type">type.</param>
        /// <returns>ReferenceElement.</returns>
        private static XElement GenerateReference(XElement targetElement, string uri, string? id = null, string? type = null)
        {
            var digestValue = HelperClass.GetDigestValue(targetElement);

            return new XElement(
                ds + "Reference",
                type != null ? new XAttribute("Type", type) : null,
                id != null ? new XAttribute("Id", id) : null,
                new XAttribute("URI", uri),
                new XElement(
                    ds + "Transforms",
                    new XElement(
                        ds + "Transform",
                        new XAttribute("Algorithm", "http://www.w3.org/2001/10/xml-exc-c14n#"))),
                new XElement(
                    ds + "DigestMethod",
                    new XAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#sha256")),
                new XElement(ds + "DigestValue", Convert.ToBase64String(digestValue)));
        }
    }
}
