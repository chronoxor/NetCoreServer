using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace NetCoreServer
{
    /// <summary>
    /// SSL context
    /// </summary>
    /// <remarks>
    /// By default <c>SslProtocols.None</c> allows the operating system to choose
    /// the best protocol to use, and to block protocols that are not secure.
    /// </remarks>
    public class SslContext
    {
        /// <summary>
        /// Initialize SSL context with given protocols
        /// </summary>
        /// <param name="protocols">SSL protocols (default is SslProtocols.None)</param>
        public SslContext(SslProtocols protocols = SslProtocols.None) { Protocols = protocols; }
        /// <summary>
        /// Initialize SSL context with given protocols and validation callback
        /// </summary>
        /// <param name="certificateValidationCallback">SSL certificate</param>
        /// <param name="protocols">SSL protocols (default is SslProtocols.None)</param>
        public SslContext(RemoteCertificateValidationCallback certificateValidationCallback, SslProtocols protocols = SslProtocols.None)
        {
            CertificateValidationCallback = certificateValidationCallback;
            Protocols = protocols;
        }
        /// <summary>
        /// Initialize SSL context with given protocols and certificate
        /// </summary>
        /// <param name="certificate">SSL certificate</param>
        /// <param name="protocols">SSL protocols (default is SslProtocols.None)</param>
        public SslContext(X509Certificate certificate, SslProtocols protocols = SslProtocols.None) : this(certificate, null, protocols) {}
        /// <summary>
        /// Initialize SSL context with given protocols, certificate and validation callback
        /// </summary>
        /// <param name="certificate">SSL certificate</param>
        /// <param name="certificateValidationCallback">SSL certificate</param>
        /// <param name="protocols">SSL protocols (default is SslProtocols.None)</param>
        public SslContext(X509Certificate certificate, RemoteCertificateValidationCallback certificateValidationCallback, SslProtocols protocols = SslProtocols.None)
        {
            Certificate = certificate;
            CertificateValidationCallback = certificateValidationCallback;
            Protocols = protocols;
        }
        /// <summary>
        /// Initialize SSL context with given protocols and certificates collection
        /// </summary>
        /// <param name="certificates">SSL certificates collection</param>
        /// <param name="protocols">SSL protocols (default is SslProtocols.None)</param>
        public SslContext(X509Certificate2Collection certificates, SslProtocols protocols = SslProtocols.None) : this(certificates, null, protocols) {}
        /// <summary>
        /// Initialize SSL context with given protocols, certificates collection and validation callback
        /// </summary>
        /// <param name="certificates">SSL certificates collection</param>
        /// <param name="certificateValidationCallback">SSL certificate</param>
        /// <param name="protocols">SSL protocols (default is SslProtocols.None)</param>
        public SslContext(X509Certificate2Collection certificates, RemoteCertificateValidationCallback certificateValidationCallback, SslProtocols protocols = SslProtocols.None)
        {
            Certificates = certificates;
            CertificateValidationCallback = certificateValidationCallback;
            Protocols = protocols;
        }

        /// <summary>
        /// SSL certificate
        /// </summary>
        public X509Certificate Certificate { get; set; }
        /// <summary>
        /// SSL certificates collection
        /// </summary>
        public X509Certificate2Collection Certificates { get; set; }
        /// <summary>
        /// SSL certificate validation callback
        /// </summary>
        public RemoteCertificateValidationCallback CertificateValidationCallback { get; set; }

        /// <summary>
        /// Is the client is asked for a certificate for authentication.
        /// Note that this is only a request - if no certificate is provided, the server still accepts the connection request.
        /// </summary>
        public bool ClientCertificateRequired { get; set; }

        /// <summary>
        /// SSL protocols
        /// </summary>
        public SslProtocols Protocols { get; set; }
    }
}
