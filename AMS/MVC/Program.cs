using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.AspNetCore.DataProtection;
using Repositories;
using Repositories.Implementations;
using Repositories.Interfaces;
using Repositories.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ElasticSearchService>();

// for mail
builder.Services.Configure<Repositories.Models.EmailSettings>(
    builder.Configuration.GetSection(Repositories.Models.EmailSettings.SectionName));
builder.Services.AddTransient<Repositories.Interfaces.IGmailSmtpSenderInterface, Repositories.Services.GmailSmtpSender>();


builder.Services.AddScoped<IUserInterface, UserRepository>();
builder.Services.AddScoped<IEmployeeInterface, EmployeeRepository>();
builder.Services.AddScoped<IAttendenceInterface, AttendenceRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IRedisUserService, RedisUserService>();
builder.Services.AddScoped<IAttedanceCacheService,AttedanceCacheService>();
builder.Services.AddScoped<IRabbitRegistration,RabbitRegistration>();
builder.Services.AddScoped<OTPEmailService>();
builder.Services.AddScoped<ReportEmailService>();

builder.Services.AddSingleton<IDatabase>(provider =>
{
    var multiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
    return multiplexer.GetDatabase();
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var options = new ConfigurationOptions
    {
         EndPoints= { {"redis-13720.crce286.ap-south-1-1.ec2.cloud.redislabs.com", 13720} },
                User="default",
                Password="3onIHgjIU4yOKGIN4oz9BOZHCusNhjgU",

        Ssl = false, 
        AbortOnConnectFail = false
    };

    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379"; // Redis Server Address
    //options.InstanceName = "Session_"; // Prefix for session keys in Redis
});
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
    Uri(configuration["Elasticsearch:Uri"] ?? throw new InvalidOperationException("Elasticsearch:Uri not configured")))
    .DefaultIndex(configuration["Elasticsearch:DefaultIndex"] ?? "attendance")
    .Authentication(new
    BasicAuthentication(configuration["Elasticsearch:Username"] ?? "elastic",
    configuration["Elasticsearch:Password"] ?? ""))
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
    var employeeRepo = scope.ServiceProvider.GetRequiredService<IEmployeeInterface>();
    var esService = scope.ServiceProvider.GetRequiredService<ElasticSearchService>();
    try
    {
        await esService.CreateIndexAsync();
        var attendance = await AttendRepo.GetAllAttendance();
        if (attendance.Count > 0)
        {
            int indexedCount = 0;
            foreach (var Attend in attendance)
            {
                // Get existing document from ES to check if already indexed
                var existingDoc = await esService.GetAttendanceByIdAsync(Attend.AttendId);
                if (existingDoc == null)
                {
                    // Fetch employee data to populate email and status
                    var empData = await employeeRepo.GetUserById(Attend.EmpId);
                    await esService.IndexAttendanceAsync(
                        Attend,
                        empData?.Name,
                        empData?.Email,
                        empData?.Status);
                    indexedCount++;
                }
            }
            Console.WriteLine($" {indexedCount} new attendance records indexed in ElasticSearch (Total in DB: {attendance.Count}).");
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
