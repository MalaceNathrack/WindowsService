using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using PlexMediaOrganizer;
using PlexMediaOrganizer.Services;
using PlexMediaOrganizer.Configuration;
using PlexMediaOrganizer.Data;
using PlexMediaOrganizer.Data.Repositories;
using PlexMediaOrganizer.WebInterface;
using PlexMediaOrganizer.WebInterface.Models;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PlexMediaOrganizer";
});

// Add configuration
builder.Services.Configure<PlexMediaOrganizerSettings>(
    builder.Configuration.GetSection(nameof(PlexMediaOrganizerSettings)));

// Add services
builder.Services.AddHttpClient();

// Get database settings
var serviceProvider = builder.Services.BuildServiceProvider();
var settings = serviceProvider.GetRequiredService<IOptions<PlexMediaOrganizerSettings>>().Value;

// Add database services if enabled
if (settings.Database.Enabled)
{
    builder.Services.AddDbContext<MediaOrganizerDbContext>(options =>
        options.UseSqlite(settings.Database.ConnectionString));

    builder.Services.AddScoped<IProcessedFileRepository, ProcessedFileRepository>();
    builder.Services.AddSingleton<IFileHashService, FileHashService>();
}

builder.Services.AddSingleton<IFileSystemWatcherFactory, FileSystemWatcherFactory>();
builder.Services.AddSingleton<IMediaProcessor, MediaProcessor>();
builder.Services.AddSingleton<TmdbMetadataService>();
builder.Services.AddSingleton<TvdbMetadataService>();
builder.Services.AddSingleton<IMetadataProviderFactory, MetadataProviderFactory>();
builder.Services.AddSingleton<IMetadataService, MetadataProviderFactory>();
builder.Services.AddSingleton<IImageOptimizer, ImageOptimizer>();
builder.Services.AddSingleton<IProcessingStatusTracker, ProcessingStatusTracker>();
builder.Services.AddSingleton<ISchedulerService, SchedulerService>();
builder.Services.AddSingleton<IEmailService, EmailService>();

// Add hosted services
builder.Services.AddHostedService<Worker>();

// Create a web application builder for the web interface
var webAppBuilder = WebApplication.CreateBuilder(args);

// Share configuration between the host and web app
webAppBuilder.Configuration.AddConfiguration(builder.Configuration);

// Register services for the web API and MVC
webAppBuilder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();
webAppBuilder.Services.AddEndpointsApiExplorer();

// Configure the views location
webAppBuilder.Services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Clear();
    options.ViewLocationFormats.Add("/WebInterface/Views/{1}/{0}" + RazorViewEngine.ViewExtension);
    options.ViewLocationFormats.Add("/WebInterface/Views/Shared/{0}" + RazorViewEngine.ViewExtension);
});
webAppBuilder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo {
        Title = "Plex Media Organizer API",
        Version = "v1",
        Description = "API for monitoring and configuring the Plex Media Organizer service"
    });
});

// Configure Kestrel web server
webAppBuilder.WebHost.UseKestrel(options =>
{
    options.ListenLocalhost(5000);
});

// Share services between the host and web app
webAppBuilder.Services.Configure<PlexMediaOrganizerSettings>(
    webAppBuilder.Configuration.GetSection(nameof(PlexMediaOrganizerSettings)));

// Share database services if enabled
if (settings.Database.Enabled)
{
    webAppBuilder.Services.AddDbContext<MediaOrganizerDbContext>(options =>
        options.UseSqlite(settings.Database.ConnectionString));

    webAppBuilder.Services.AddScoped<IProcessedFileRepository, ProcessedFileRepository>();
    webAppBuilder.Services.AddSingleton<IFileHashService, FileHashService>();
}

webAppBuilder.Services.AddSingleton<IProcessingStatusTracker>(sp =>
    builder.Services.BuildServiceProvider().GetRequiredService<IProcessingStatusTracker>());
webAppBuilder.Services.AddSingleton<IMediaProcessor>(sp =>
    builder.Services.BuildServiceProvider().GetRequiredService<IMediaProcessor>());
webAppBuilder.Services.AddSingleton<ISchedulerService>(sp =>
    builder.Services.BuildServiceProvider().GetRequiredService<ISchedulerService>());

// Build the web application
var webApp = webAppBuilder.Build();

// Configure the web application
webApp.UseSwagger();
webApp.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Plex Media Organizer API v1"));
webApp.UseStaticFiles();
webApp.UseRouting();
webApp.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
webApp.MapControllers();

// Add web host service with the pre-built web application
builder.Services.AddSingleton(webApp);
builder.Services.AddHostedService<WebHostService>();

var host = builder.Build();
host.Run();
