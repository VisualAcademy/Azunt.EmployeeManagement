using Azunt.EmployeeManagement;
using Azunt.Web.Components;
using Azunt.Web.Components.Account;
using Azunt.Web.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Azunt.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityUserAccessor>();
            builder.Services.AddScoped<IdentityRedirectManager>();
            builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
                })
                .AddIdentityCookies();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // Add additional endpoints required by the Identity /Account Razor components.
            app.MapAdditionalIdentityEndpoints();



            #region Employees 테이블 초기화/보강 및 시드 (DbInitItem 클래스 없이)
            try
            {
                var cfg = app.Services.GetRequiredService<IConfiguration>();
                var employeesSection = cfg.GetSection("Database:Initializers")
                                          .GetChildren()
                                          .FirstOrDefault(x =>
                                              string.Equals(x["Name"], "Employees", StringComparison.OrdinalIgnoreCase));

                if (employeesSection != null)
                {
                    bool forMaster = bool.TryParse(employeesSection["ForMaster"], out var fm) ? fm : false;
                    bool enableSeeding = bool.TryParse(employeesSection["EnableSeeding"], out var es) ? es : false; // 기본값 false

                    EmployeesTableBuilder.Run(app.Services, forMaster: forMaster, enableSeeding: enableSeeding);

                    Console.WriteLine(
                        $"Employees table initialization finished. Target={(forMaster ? "Master" : "Tenants")}, Seed={enableSeeding}"
                    );
                }
                else
                {
                    Console.WriteLine("Employees initializer not configured in Database:Initializers. Skipped.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Employees table initialization failed: {ex.Message}");
            }
            #endregion



            app.Run();
        }
    }
}
