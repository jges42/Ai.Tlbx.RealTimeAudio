using Ai.Tlbx.RealTimeAudio.Demo.Web.Components;
using Ai.Tlbx.RealTimeAudio.Hardware.Web;
using Ai.Tlbx.RealTimeAudio.OpenAi;

namespace Ai.Tlbx.RealTimeAudio.Demo.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();


        builder.Services.AddScoped<IAudioHardwareAccess,WebAudioAccess>();
        builder.Services.AddScoped<OpenAiRealTimeApiAccess>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}
