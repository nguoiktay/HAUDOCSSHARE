using Microsoft.EntityFrameworkCore;
using Documentshare.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// ── HttpClient cho Python AI Chatbot Service ──────────────────────────────
builder.Services.AddHttpClient("PythonChatbot", client =>
{
    client.BaseAddress = new Uri("http://localhost:8001");
    client.Timeout     = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "DocumentShare_Session";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi khi tạo cơ sở dữ liệu.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();

// Restore session from "Remember Me" cookie if session is null
app.Use(async (context, next) =>
{
    if (context.Session.GetInt32("UserId") == null)
    {
        if (context.Request.Cookies.TryGetValue("DS_RememberUser", out var userIdStr) && int.TryParse(userIdStr, out var userId))
        {
            var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();
            var user = await dbContext.Users.FindAsync(userId);
            if (user != null && user.IsActive)
            {
                context.Session.SetInt32("UserId", user.Id);
                context.Session.SetString("UserName", user.Username);
                context.Session.SetString("UserDisplayName", user.DisplayName);
                context.Session.SetString("UserRole", user.Role);
            }
        }
    }
    await next();
});

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
