// See https://aka.ms/new-console-template for more information

using Bench.wcf;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.WriteLine("Starting...!");
var app = SetupWcf();
await app.RunAsync();

WebApplication SetupWcf() {
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddServiceModelServices().AddServiceModelMetadata();
    builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

    var host = builder.Build();

    // Configure an explicit none credential type for WSHttpBinding as it defaults to Windows which requires extra configuration in ASP.NET
    var myWsHttpBinding = new WSHttpBinding(SecurityMode.Transport);
    myWsHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;

    ((IHost)host).UseServiceModel(b => {
        b.AddService<INorthwindService>((serviceOptions) => { })
            .AddServiceEndpoint<NorthwindWcfService, INorthwindService>(new BasicHttpBinding(), "/NorthwindService/basichttp")
            .AddServiceEndpoint<NorthwindWcfService, INorthwindService>(myWsHttpBinding, "/NorthwindService/WSHttps");
    });

    var serviceMetadataBehavior = host.Services.GetRequiredService<CoreWCF.Description.ServiceMetadataBehavior>();
    serviceMetadataBehavior.HttpGetEnabled = true;
    return host;
}