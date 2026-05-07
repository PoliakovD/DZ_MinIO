using DZ_MinIO.Services;

var builder = WebApplication.CreateBuilder(args);

// логирование
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Сервис
builder.Services.AddScoped<IFileService, FileService>();

// MVC
builder.Services.AddControllersWithViews();


var app = builder.Build();

// автоматическое создание бакета
using (var scope = app.Services.CreateScope())
{
    var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
    await fileService.EnsureBucketExistsAsync();
}

app.UseStaticFiles();
app.UseRouting();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Files}/{action=Index}");

app.Run();