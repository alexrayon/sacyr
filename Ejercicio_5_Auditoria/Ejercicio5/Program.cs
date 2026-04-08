using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.sacyr.local";
        options.Audience = "sacyr-fleet";
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("FleetViewer", policy => policy.RequireAssertion(context =>
        context.User.HasClaim(c => c.Type == "permission" && c.Value == "fleet.view")
        || context.User.IsInRole("FleetAnalyst")));

    options.AddPolicy("CriticalAssetAdmin", policy => policy.RequireAssertion(context =>
        context.User.HasClaim(c => c.Type == "permission" && c.Value == "fleet.critical.manage")
        || context.User.IsInRole("CriticalAdmin")));
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
