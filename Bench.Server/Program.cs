// See https://aka.ms/new-console-template for more information

using Bench.wcf;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Starting...!");
await StartWcf();


async Task StartWcf() {
    var builder = WebApplication.CreateBuilder(args);

// Add WSDL support
    builder.Services.AddServiceModelServices().AddServiceModelMetadata();
    builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

    var app = builder.Build();


// Specify both HTTP and HTTPS ports explicitly
    app.Urls.Add("http://localhost:5000");
    app.Urls.Add("https://localhost:5001");

// Configure an explicit none credential type for WSHttpBinding as it defaults to Windows which requires extra configuration in ASP.NET
    var myWsHttpBinding = new WSHttpBinding(SecurityMode.Transport);
    myWsHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;

    ((IApplicationBuilder)app).UseServiceModel(option => {
        option
            .AddService<NorthwindWcfService>(serviceOptions => { })
            .AddServiceEndpoint<NorthwindWcfService, INorthwindService>(new BasicHttpBinding(), "/NorthwindWcfService/basic")
            .AddServiceEndpoint<NorthwindWcfService, INorthwindService>(myWsHttpBinding, "/NorthwindWcfService/ws");
    });

    var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    serviceMetadataBehavior.HttpGetEnabled = true;
    await app.RunAsync();
}