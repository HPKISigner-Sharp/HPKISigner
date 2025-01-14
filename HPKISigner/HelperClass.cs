// <Copyright file="HelperClass.cs" company="MentorSystems">
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
    using System.Formats.Asn1;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Cryptography.Xml;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;

    internal static class HelperClass
    {
        /// <summary>
        /// <see cref="CanonicalizeElement"/> Exc-14n Canonicalize element.
        /// </summary>
        /// <param name="element">Element to be Canonicalized.</param>
        /// <returns>Canonicalized Element.</returns>
        internal static string CanonicalizeElement(XElement element)
        {
            // Load XML document via MemoryStream
            XmlDocument xmlDocument = new XmlDocument();
            using (var memoryStream = new MemoryStream())
            {
                using (var xmlWriter = XmlWriter.Create(memoryStream, new XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Encoding = Encoding.UTF8,
                }))
                {
                    element.Save(xmlWriter);
                }

                memoryStream.Position = 0;
                xmlDocument.Load(memoryStream);
            }

            // Create the canonicalization transform (exc-14n)
            XmlDsigExcC14NTransform canonicalizationTransform = new XmlDsigExcC14NTransform();
            canonicalizationTransform.LoadInput(xmlDocument);

            // Get canonicalized XML
            using (Stream canonicalStream = (Stream)canonicalizationTransform.GetOutput(typeof(Stream)))
            {
                using (StreamReader reader = new StreamReader(canonicalStream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// <see cref="ComputeSha256Hash"/> Compute Sha256Hash from byte[].
        /// </summary>
        /// <param name="input">byte[] to be hashed.</param>
        /// <returns>Computed Sha256Hash.</returns>
        internal static byte[] ComputeSha256Hash(byte[] input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(input);
            }
        }

        /// <summary>
        /// <see cref="ComputeSha256Hash"/> Compute Sha256Hash from string.
        /// </summary>
        /// <param name="input">string to be hashed.</param>
        /// <returns>Computed Sha256Hash.</returns>
        internal static byte[] ComputeSha256Hash(string input)
        {
            return ComputeSha256Hash(Encoding.UTF8.GetBytes(input));
        }

        /// <summary>
        /// <see cref="GetDigestValue"/> CanonicalizeElement -> ComputeSha256Hash.
        /// </summary>
        /// <param name="xElement">Element to be Canonicalized and SHA-256 Hashed.</param>
        /// <returns>DigestValue.</returns>
        internal static byte[] GetDigestValue(XElement xElement)
        {
            // 1. Canonicalize the element (Exc-C14N)
            string canonicalizedXml = CanonicalizeElement(xElement);

            // 2. Compute the SHA-256 hash of the canonicalized XML
            byte[] sha256Digest = ComputeSha256Hash(canonicalizedXml);

            return sha256Digest;
        }

        /// <summary>
        /// <see cref="GetIssuerSerialV2"/> GetIssuerSerialV2.
        /// </summary>
        /// <param name="certBytes">certBytes.</param>
        /// <returns>ssuerSerialV2.</returns>
        internal static string GetIssuerSerialV2(byte[] certBytes)
        {
            // Make X509Certificate2 structured cert
            X509Certificate2 cert = new X509Certificate2(certBytes);

            // Extract issuer name (distinguished name)
            var issuerName2 = cert.IssuerName.RawData;

            // Extract serial number and normalize it (convert to byte array)
            var serialNumber2 = NormalizeSerialNumber(cert.SerialNumber);

            // Encode IssuerSerial structure in DER format
            var asnEncoded = EncodeIssuerSerial(issuerName2, serialNumber2);

            // Base64 encode the DER-encoded structure
            var issuerSerialV2 = Convert.ToBase64String(asnEncoded);

            return issuerSerialV2;
        }

        private static byte[] NormalizeSerialNumber(string serialNumber)
        {
            // SerialNumber from X509Certificate2 is usually a hexadecimal string
            // Normalize by converting it to a byte array (big-endian)
            var number = serialNumber.ToUpperInvariant(); // Ensure uniform casing
            int length = number.Length;
            byte[] result = new byte[length / 2];
            for (int i = 0; i < length; i += 2)
            {
                result[i / 2] = Convert.ToByte(number.Substring(i, 2), 16);
            }

            return result;
        }

        private static byte[] EncodeIssuerSerial(byte[] issuerName, byte[] serialNumber)
        {
            var writer = new AsnWriter(AsnEncodingRules.DER);
            writer.PushSequence(); // Start IssuerSerial SEQUENCE

            // Add issuerName as a DirectoryString
            writer.PushSequence(); // Start GeneralName (issuer name)
            writer.WriteOctetString(issuerName); // Encode as an octet string
            writer.PopSequence(); // End GeneralName

            // Add serialNumber as an INTEGER
            writer.WriteInteger(serialNumber);

            writer.PopSequence(); // End IssuerSerial

            return writer.Encode(); // Encode the complete structure
        }
    }
}
