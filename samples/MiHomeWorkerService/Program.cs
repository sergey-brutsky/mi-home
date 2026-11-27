using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiHomeWorkerService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<MiHomeOptions>(builder.Configuration.GetSection("MiHome"));
builder.Services.AddSingleton<GatewayRegistry>();
builder.Services.AddHostedService<GatewayHostedService>();
builder.Services.AddHostedService<LeakNotifier>();
builder.Build().Run();
