

using System.Diagnostics;
using System.ServiceModel;
using System.Text.Json;
using System.Xml;
using Bench.Client.Wcf;
using Bench.wcf.DataContracts;

namespace Bench.Client.wcf;

public class Perf(WcfTransport transport, Args args) : IDisposable {
    
    NorthwindServiceClient _client = transport switch {
        WcfTransport.BasicHttp => new NorthwindServiceClient(NorthwindServiceClient.EndpointConfiguration.BasicHttpBinding_INorthwindService),
        WcfTransport.WsHttp    => new NorthwindServiceClient(NorthwindServiceClient.EndpointConfiguration.WSHttpBinding_INorthwindService),
        WcfTransport.NetTcp    => new NorthwindServiceClient(CreateNetTcpBinding(), new EndpointAddress(args.Address)),
        _                      => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    public async Task<bool> Call(int clientId, CancellationToken ct = default) {
        var order = await _client.GetOrderByIdAsync(new OrderRequest { OrderId = 1 });
        return order != null;
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