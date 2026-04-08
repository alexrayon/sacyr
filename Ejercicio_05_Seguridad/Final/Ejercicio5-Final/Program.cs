using Microsoft.AspNetCore.Authentication;
using Ejercicio5_Final.Security;
using Ejercicio5_Final.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IMachineService, MachineService>();

builder.Services
    .AddAuthentication("DevAuth")
    .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("DevAuth", _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("FleetViewer", policy => policy.RequireRole("FleetViewer", "CriticalAssetAdmin", "CentralAdminOnly"));
    options.AddPolicy("CriticalAssetAdmin", policy => policy.RequireRole("CriticalAssetAdmin", "CentralAdminOnly"));
    options.AddPolicy("CentralAdminOnly", policy => policy.RequireRole("CentralAdminOnly"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

