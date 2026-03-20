using Microsoft.AspNetCore.DataProtection;
using Repositories.Implementations;
using Repositories.Interfaces;
using Repositories.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IUserInterface, UserRepository>();
builder.Services.AddScoped<IEmployeeInterface, EmployeeRepository>();
builder.Services.AddScoped<IAttendenceInterface, AttendenceRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IRedisUserService, RedisUserService>();
builder.Services.AddScoped<IAttedanceCacheService,AttedanceCacheService>();
builder.Services.AddScoped<IRabbitRegistration,RabbitRegistration>();

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
    pattern: "{controller=User}/{action=Login}/{id?}");

app.Run();
