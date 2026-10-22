using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Xml;
using Bench.Client.Wcf;
using Bench.wcf.DataContracts;

namespace Bench.Client.wcf;

public class WcfPerf(WcfTransport transport, Args args) : IDisposable {
    private readonly NorthwindServiceClient _client = Get(transport, args);
    private readonly BenchOperation _operation = args.Operation;
    private readonly int _expectedOrders = args.EffectiveOrderCount;
    private readonly OrdersRequest _ordersRequest = new() { Count = args.Count };
    public int? LastOrders { get; private set; }

    private static NorthwindServiceClient Get(WcfTransport transport, Args args) {
        NorthwindServiceClient client = transport switch {
            WcfTransport.BasicHttp => new NorthwindServiceClient(NorthwindServiceClient.EndpointConfiguration.BasicHttpBinding_INorthwindService),
            WcfTransport.WsHttp => new NorthwindServiceClient(NorthwindServiceClient.EndpointConfiguration.WSHttpBinding_INorthwindService),
            WcfTransport.NetTcp => new NorthwindServiceClient(CreateNetTcpBinding(), new EndpointAddress(args.Address)),
            _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
        };

        //disable cert validation for wshttp
        client.ClientCredentials.ServiceCertificate.SslCertificateAuthentication =
            new X509ServiceCertificateAuthentication() {
                CertificateValidationMode = X509CertificateValidationMode.None,
                RevocationMode = X509RevocationMode.NoCheck
            };

        return client;
    }

    public async Task<bool> Call(int clientId, CancellationToken ct = default) {
        switch (_operation) {
            case BenchOperation.GetOrderById:
                return await _client.GetOrderByIdAsync(new OrderRequest { OrderId = 1 }) != null;

            default: {
                var response = await _client.GetOrdersAsync(_ordersRequest);
                LastOrders = response.Orders.Length;
                // A length mismatch means the server clamped or something truncated the response.
                // Reporting success there would put a wrong throughput in the results table.
                return LastOrders.Value == _expectedOrders;
            }
        }
    }
 
    static NetTcpBinding CreateNetTcpBinding() {
        // MaxBufferSize caps the per-session buffer used on BOTH the send and the receive path, so it
        // has to fit the largest response or GetOrders fails with "the server did not provide a
        // meaningful reply". The default 65,536 is fine for GetOrderById and too small for the batch
        // operation. 16 MB covers a 10,000 order net.tcp response (~5.9 MB) with headroom, while
        // staying bounded - int.MaxValue asks for a 2 GB per-session buffer and is never needed.
        return new NetTcpBinding(SecurityMode.None) {
            MaxBufferSize         = 16 * 1024 * 1024,
            MaxReceivedMessageSize = int.MaxValue,
            ReaderQuotas          = XmlDictionaryReaderQuotas.Max,
            OpenTimeout           = TimeSpan.FromSeconds(10),
            CloseTimeout          = TimeSpan.FromSeconds(10),
            SendTimeout           = TimeSpan.FromMinutes(1),
            ReceiveTimeout        = TimeSpan.FromMinutes(1),
        };
    }

    public void Dispose() {
        // Closing a faulted channel throws CommunicationObjectFaultedException. That is a normal
        // outcome for a benchmark run - the harness counts the failed calls - so letting it escape
        // Dispose() tears the exception out through RunWcf's finally block and destroys a result set
        // that was otherwise complete. Abort a faulted channel and never throw from cleanup.
        try {
            if (_client.State == CommunicationState.Faulted) _client.Abort();
            else _client.Close();
        } catch (Exception) {
            _client.Abort();
        }
    }
}
