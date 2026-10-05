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
        return new NetTcpBinding(SecurityMode.None) {
            MaxBufferSize         = int.MaxValue,
            MaxReceivedMessageSize = int.MaxValue,
            ReaderQuotas          = XmlDictionaryReaderQuotas.Max,
            OpenTimeout           = TimeSpan.FromSeconds(10),
            CloseTimeout          = TimeSpan.FromSeconds(10),
            SendTimeout           = TimeSpan.FromMinutes(1),
            ReceiveTimeout        = TimeSpan.FromMinutes(1),
        };
    }

    public void Dispose() {
        _client.Close();
    }
}
