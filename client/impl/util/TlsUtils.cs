using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using io.harness.ff_dotnet_client_sdk.client.impl.dto;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace io.harness.ff_dotnet_client_sdk.client.impl.util
{
    public class TlsUtils
    {
        internal static HttpClient CreateHttpClientWithTls(FfConfig config, ILoggerFactory loggerFactory)
        {
            if (config.TlsTrustedCAs.IsNullOrEmpty())
            {
                return new HttpClient();
            }

#if (NETSTANDARD || NET461)
            throw new NotSupportedException("Custom TLS certificates require .net5.0 target or greater");
#else
            var logger = loggerFactory.CreateLogger<TlsUtils>();
            var handler = new HttpClientHandler();

            handler.ServerCertificateCustomValidationCallback = delegate (HttpRequestMessage request, X509Certificate2 serverCertificate, X509Chain serverChain, SslPolicyErrors sslPolicyErrors)
            {
                logger.LogDebug("TLS: Validating server certificate {subject} for {url}, policyErrors={sslPolicyErrors}", serverCertificate.Subject, request.RequestUri, sslPolicyErrors);
                PrintCert(logger, serverCertificate);

                var requestHost = request.RequestUri?.Host;
                var certHost = serverCertificate.GetNameInfo(X509NameType.DnsFromAlternativeName, false);

                if (requestHost == null || certHost == null)
                {
                    logger.LogError("Missing hostname/certhost");
                    return false;
                }

                var match = IPAddress.TryParse(certHost, out _) ? requestHost.Equals(certHost) : requestHost.EndsWith(certHost);

                if (!match)
                {
                    logger.LogError("SDKCODE(init:1005): TLS Hostname validation failed (sdk requested={reqhost} server cert wants={svrhost}) for {url}",
                        requestHost,
                        certHost,
                        request.RequestUri);
                    return false;
                }

                using var chain = new X509Chain(false);
                chain.ChainPolicy.DisableCertificateDownloads = true;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Clear();

                foreach (var nextCa in config.TlsTrustedCAs)
                {
                    logger.LogDebug("TLS truststore: Adding cert: {subject}", nextCa.Subject);
                    chain.ChainPolicy.CustomTrustStore.Add(nextCa);
                    PrintCert(logger, nextCa);
                }

                foreach (var nextCa in serverChain.ChainElements)
                {
                    var builder = new StringBuilder();
                    foreach (var status in nextCa.ChainElementStatus)
                    {
                        builder.Append(status.Status).Append(' ').Append(status.StatusInformation).Append(' ');
                        PrintCert(logger, nextCa.Certificate);
                    }

                    logger.LogDebug("TLS truststore: Adding server cert: {subject} chainStatus=[{status}]", nextCa.Certificate.Subject, builder.ToString());
                    chain.ChainPolicy.CustomTrustStore.Add(nextCa.Certificate);
                }

                if (!chain.Build(serverCertificate))
                {
                    if (chain.ChainStatus.Any(s => s.Status != X509ChainStatusFlags.NoError))
                    {
                        logger.LogError("SDKCODE(init:1004): TLS Certificate did not validate against trust store (reason={reason}) for {url}",
                            chain.ChainStatus.First(c => c.Status != X509ChainStatusFlags.NoError).Status,
                            request.RequestUri);
                    }

                    return false;
                }

                logger.LogDebug("TLS: Endpoint {hostname}:{port} is trusted", request.RequestUri?.Host ?? "", request.RequestUri?.Port ?? -1);

                return true;
            };

            return new HttpClient(handler, true);

#endif // NETSTANDARD
        }

        internal static void PrintCert(ILogger logger, X509Certificate2 cert)
        {
            if (!logger.IsEnabled(LogLevel.Trace)) return;
            logger.LogTrace(cert.ToString());
            foreach (var ext in cert.Extensions)
            {
                AsnEncodedData asn = new AsnEncodedData(ext.Oid, ext.RawData);
                logger.LogTrace("Oid Name: {name} Oid: {value} Data: {data}", ext.Oid?.FriendlyName, asn.Oid?.Value, asn.Format(true));
            }
        }

    }
}