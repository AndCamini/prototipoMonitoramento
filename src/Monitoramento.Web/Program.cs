using Microsoft.Extensions.Hosting.WindowsServices;
using Monitoramento.Persistencia;
using Monitoramento.Servico;
using Monitoramento.Web.Components;

// Os quatro componentes (domínio, persistência, serviço de monitoramento e interface web) são
// publicados como um único executável, registrado como Windows Service ou unidade do systemd.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
});

// RNF03: integração com o gerenciador de serviços do sistema operacional (sem efeito fora dele).
builder.Services.AddWindowsService(o => o.ServiceName = "MonitoramentoDisponibilidade");
builder.Services.AddSystemd();

var conexao = builder.Configuration.GetConnectionString("Monitoramento")
              ?? throw new InvalidOperationException("Configure ConnectionStrings:Monitoramento no appsettings.json.");

builder.Services.AddPersistencia(conexao);
builder.Services.AddServicoMonitoramento(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// RNF07: o esquema é criado e evoluído automaticamente pelas migrações do EF Core.
await app.Services.AplicarMigracoesAsync(app.Logger);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
