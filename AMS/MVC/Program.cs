using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.AspNetCore.DataProtection;
using Repositories.Implementations;
using Repositories.Interfaces;
using Repositories.Services;

var builder = WebApplication.CreateBuilder(args);
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ElasticSearchService>();
builder.Services.AddScoped<IUserInterface, UserRepository>();
builder.Services.AddScoped<IEmployeeInterface, EmployeeRepository>();
builder.Services.AddScoped<IAttendenceInterface, AttendenceRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("AMS-MVC");
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddControllers();
builder.Services.AddSingleton(provider =>
{
    var configuration = builder.Configuration;
    var settings = new ElasticsearchClientSettings(new
    Uri(configuration["Elasticsearch:Uri"]))
    .ServerCertificateValidationCallback(CertificateValidations.AllowAll)
    .DefaultIndex(configuration["Elasticsearch:DefaultIndex"])
    .Authentication(new
    BasicAuthentication(configuration["Elasticsearch:Username"],
    configuration["Elasticsearch:Password"]))
    .DisableDirectStreaming();
    return new ElasticsearchClient(settings);
});
builder.Services.AddScoped<Npgsql.NpgsqlConnection>(_ =>
    new Npgsql.NpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.MapControllers();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=User}/{action=Index}/{id?}");

async Task IndexDataOnStartup()
{
    using var scope = app.Services.CreateScope();
    var AttendRepo = scope.ServiceProvider.GetRequiredService<IAttendenceInterface>();
    var esService = scope.ServiceProvider.GetRequiredService<ElasticSearchService>();
    try
    {
        await esService.CreateIndexAsync();
        var attendance = await AttendRepo.GetAllAttendance();
        if (attendance.Count > 0)
        {
            foreach (var Attend in attendance)
            {
                await esService.IndexAttendanceAsync(Attend);
            }
            Console.WriteLine($" {attendance.Count} attendance indexed successfully in ElasticSearch.");
        }
        else
        {
            Console.WriteLine(" No attendance found in Database");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($" Error indexing attendance: {ex.Message}");
    }
}
// Run indexing on startup
await IndexDataOnStartup();

app.Run();
