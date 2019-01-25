using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace NetCoreServer
{
    /// <summary>
    /// SSL context
    /// </summary>
    public class SslContext
    {
        /// <summary>
        /// Initialize SSL context with default protocols
        /// </summary>
        public SslContext() : this(SslProtocols.Default, null) {}
        /// <summary>
        /// Initialize SSL context with given protocols
        /// </summary>
        /// <param name="protocols">SSL protocols</param>
        public SslContext(SslProtocols protocols) : this(protocols, null) {}
        /// <summary>
        /// Initialize SSL context with given protocols and certificate
        /// </summary>
        /// <param name="protocols">SSL protocols</param>
        /// <param name="certificate">SSL certificate</param>
        public SslContext(SslProtocols protocols, X509Certificate certificate) : this(protocols, certificate, null) {}
        /// <summary>
        /// Initialize SSL context with given protocols, certificate and validation callback
        /// </summary>
        /// <param name="protocols">SSL protocols</param>
        /// <param name="certificate">SSL certificate</param>
        /// <param name="certificateValidationCallback">SSL certificate</param>
        public SslContext(SslProtocols protocols, X509Certificate certificate, RemoteCertificateValidationCallback certificateValidationCallback)
        {
            Protocols = protocols;
            Certificate = certificate;
            CertificateValidationCallback = certificateValidationCallback;
        }

        /// <summary>
        /// SSL protocols
        /// </summary>
        public SslProtocols Protocols { get; set; }
        /// <summary>
        /// SSL certificate
        /// </summary>
        public X509Certificate Certificate { get; set; }
        /// <summary>
        /// SSL certificate validation callback
        /// </summary>
        public RemoteCertificateValidationCallback CertificateValidationCallback { get; set; }

        /// <summary>
        /// Is the client is asked for a certificate for authentication.
        /// Note that this is only a request - if no certificate is provided, the server still accepts the connection request.
        /// </summary>
        public bool ClientCertificateRequired { get; set; }
    }
}
