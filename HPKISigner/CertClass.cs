// <Copyright file="CertClass.cs" company="MentorSystems">
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
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using Net.Pkcs11Interop.Common;
    using Net.Pkcs11Interop.HighLevelAPI;
    using Org.BouncyCastle.Asn1;
    using Org.BouncyCastle.Asn1.Nist;
    using Org.BouncyCastle.Asn1.X509;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Crypto.Encodings;
    using Org.BouncyCastle.Crypto.Engines;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.X509;
    using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

    internal static class CertClass
    {
        // private static readonly string HpkiModulePath = @"C:\Windows\System32\HpkiAuthP11_MPKCS11H.dll";   // If you want to use auth cert, uncomment this line.
        private static readonly string HpkiModulePath = @"C:\Windows\System32\HpkiSigP11_MPKCS11H.dll";       // We use ONLY sign cert. NOT auth cert.

        internal static byte[] GetCert()
        {
            try
            {
                // Open the Windows certificate store for smart card
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                // var selectedCert = store.Certificates.FirstOrDefault(x => x.Issuer.Contains("Authentication"));  // If you want to use auth cert, uncomment this line.
                var selectedCert = store.Certificates.FirstOrDefault(x => x.Issuer.Contains("NonRepudiation"));     // We use ONLY sign cert. NOT auth cert. Nonrepudiation is for signing.
                if (selectedCert == null)
                {
                    throw new Exception("No cert found");
                }

                // Convert the certificate to BouncyCastle format
                X509Certificate bouncyCert = new X509CertificateParser().ReadCertificate(selectedCert.RawData);

                // Close Store
                store.Close();

                // bouncyCert is changed to byte[] and return
                return bouncyCert.GetEncoded();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// <see cref="Sign"/> Sign with a private key stored in a smart card.
        /// </summary>
        /// <param name="dataString">data string to be signed.</param>
        /// <param name="userPin">User's PIN.</param>
        /// <returns>signatured string signed with a private key.</returns>
        internal static byte[] Sign(string dataString, string userPin)
        {
            byte[] hash = HelperClass.ComputeSha256Hash(Encoding.UTF8.GetBytes(dataString));
            return Sign(hash, userPin);
        }

        /// <summary>
        /// <see cref="Sign"/> Sign with a private key stored in my smart card.
        /// </summary>
        /// <param name="data">data byte[] to be signed.</param>
        /// <param name="userPin">User's PIN.</param>
        /// <returns>signatured byte[] signed with a private key.</returns>
        internal static byte[] Sign(byte[] data, string userPin)
        {
            var factories = new Pkcs11InteropFactories();
            try
            {
                using (var pkcs11Library = factories.Pkcs11LibraryFactory.LoadPkcs11Library(factories, HpkiModulePath, AppType.MultiThreaded))
                {
                    // Find first slot with token present
                    var slot = pkcs11Library.GetSlotList(SlotsType.WithOrWithoutTokenPresent).First();

                    // Open session
                    using (var session = slot.OpenSession(SessionType.ReadWrite))
                    {
                        // Login as normal user
                        session.Login(CKU.CKU_USER, userPin);

                        // Search handle which has a private key. if not, throw error.
                        var pkcs11UriBuilder = new Pkcs11UriBuilder
                        {
                            ModulePath = HpkiModulePath,
                            PinValue = userPin,
                            Type = CKO.CKO_PRIVATE_KEY,
                        };
                        var searchTemplate = Pkcs11UriUtils.GetObjectAttributes(new Pkcs11Uri(pkcs11UriBuilder.ToString()), session.Factories.ObjectAttributeFactory);
                        var allObjects = session.FindAllObjects(searchTemplate);
                        if (allObjects.Count == 0)
                        {
                            throw new Exception("Private key not found on token.");
                        }

                        // Create DigestInfo (ASN.1 Structure)
                        AlgorithmIdentifier algorithmIdentifier = new AlgorithmIdentifier(NistObjectIdentifiers.IdSha256, DerNull.Instance);
                        var digestinfo = new DigestInfo(algorithmIdentifier, data);
                        byte[] der = digestinfo.GetEncoded();
                        int len = der.Length;

                        // Specify signing mechanism
                        var mechanism = session.Factories.MechanismFactory.Create(CKM.CKM_RSA_PKCS);

                        // Sign data
                        byte[] signature = session.Sign(mechanism, allObjects[0], der);

                        // Logout
                        session.Logout();

                        // return byte[] of signature
                        return signature;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return Array.Empty<byte>();
            }
        }


        /// <summary>
        /// <see cref="Verify"/> Verify signature. Judge DataHash is same or not as SignaturedHash.
        /// </summary>
        /// <param name="dataHash">DataHash.</param>
        /// <param name="signature">Signatured Hash.</param>
        /// <returns>ReturnValue.</returns>
        internal static bool Verify(byte[] dataHash, byte[] signature)
        {
            // Get certBytes
            byte[] certBytes = GetCert();

            try
            {
                // Load certificate
                X509Certificate certificate = new X509CertificateParser().ReadCertificate(certBytes);

                // Get public key from certificate
                AsymmetricKeyParameter publicKey = certificate.GetPublicKey();

                if (!(publicKey is RsaKeyParameters rsaPublicKey))
                {
                    throw new Exception("Public key is not an RSA key");
                }

                // Verify the signature
                return Verify(dataHash, signature, rsaPublicKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

        private static bool Verify(byte[] dataHash, byte[] signature, RsaKeyParameters publicKey)
        {
            try
            {
                // IMORTANT!
                // The hash used for verification must not be raw hash bytes but part of a properly formatted DigestInfo structure for RSA - PKCS#1 v1.5 padding.
                // DigestInfo includes the hash algorithm identifier followed by the actual hash.If this is not properly encoded, the signature will fail.

                // Use PKCS1 Encoding to handle DigestInfo
                var engine = new Pkcs1Encoding(new RsaEngine());
                engine.Init(false, publicKey);

                // Decrypt the signature to retrieve the DigestInfo
                byte[] decryptedDigestInfo = engine.ProcessBlock(signature, 0, signature.Length);

                // Manually construct the expected DigestInfo
                var expectedDigestInfo = CreateDigestInfo("SHA-256", dataHash);

                // Compare decrypted DigestInfo with expected DigestInfo
                return CompareArrays(decryptedDigestInfo, expectedDigestInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during signature verification: {ex.Message}");
                return false;
            }
        }

        private static byte[] CreateDigestInfo(string hashAlgorithm, byte[] hashValue)
        {
            // OIDs for common hash algorithms
            var oids = new Dictionary<string, string>
            {
                { "SHA-256", "2.16.840.1.101.3.4.2.1" },
            };

            if (!oids.TryGetValue(hashAlgorithm, out var oid))
            {
                throw new ArgumentException($"Unsupported hash algorithm: {hashAlgorithm}");
            }

            var seq = new DerSequence(
                new AlgorithmIdentifier(new DerObjectIdentifier(oid), DerNull.Instance),
                new DerOctetString(hashValue));

            return seq.GetEncoded();
        }

        private static bool CompareArrays(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
