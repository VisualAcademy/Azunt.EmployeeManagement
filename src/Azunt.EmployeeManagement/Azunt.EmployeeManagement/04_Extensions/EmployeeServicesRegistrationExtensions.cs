using Azunt.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Azunt.EmployeeManagement;

/// <summary>
/// EmployeeApp 의존성 주입(Dependency Injection) 확장 메서드 모음
/// - EF Core / Dapper / ADO.NET 모드를 선택적으로 등록
/// - 멀티테넌트 시나리오에서 각 리포지토리 생성자에 연결 문자열을 주입
/// </summary>
public static class EmployeeServicesRegistrationExtensions
{
    /// <summary>
    /// EmployeeApp 모듈의 서비스를 등록합니다.
    /// </summary>
    /// <param name="services">서비스 컨테이너</param>
    /// <param name="connectionString">연결 문자열</param>
    /// <param name="mode">레포지토리 사용 모드 (기본: EF Core)</param>
    /// <param name="dbContextLifetime">DbContext 수명 주기 (기본: Scoped)</param>
    public static void AddDependencyInjectionContainerForEmployeeApp(
        this IServiceCollection services,
        string connectionString,
        RepositoryMode mode = RepositoryMode.EfCore,
        ServiceLifetime dbContextLifetime = ServiceLifetime.Scoped)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));

        switch (mode)
        {
            case RepositoryMode.EfCore:
                // EF Core 방식 등록
                services.AddDbContext<EmployeeDbContext>(
                    options => options.UseSqlServer(connectionString),
                    dbContextLifetime);

                // DbContextFactory는 EmployeeRepository에서 사용
                services.AddTransient<EmployeeDbContextFactory>();

                // Repository 등록
                services.AddTransient<IEmployeeRepository, EmployeeRepository>();
                break;

            case RepositoryMode.Dapper:
                // Dapper 방식 등록 (연결 문자열 + LoggerFactory 주입)
                services.AddTransient<IEmployeeRepository>(provider =>
                    new EmployeeRepositoryDapper(
                        connectionString,
                        provider.GetRequiredService<ILoggerFactory>()));
                break;

            case RepositoryMode.AdoNet:
                // ADO.NET 방식 등록 (연결 문자열 + LoggerFactory 주입)
                services.AddTransient<IEmployeeRepository>(provider =>
                    new EmployeeRepositoryAdoNet(
                        connectionString,
                        provider.GetRequiredService<ILoggerFactory>()));
                break;

            default:
                throw new InvalidOperationException(
                    $"Invalid repository mode '{mode}'. Supported modes: EfCore, Dapper, AdoNet.");
        }
    }

    /// <summary>
    /// IConfiguration에서 "DefaultConnection"을 읽어 EmployeeApp 서비스를 등록합니다.
    /// </summary>
    /// <param name="services">서비스 컨테이너</param>
    /// <param name="configuration">구성(예: appsettings.json)</param>
    /// <param name="mode">레포지토리 사용 모드 (기본: EF Core)</param>
    /// <param name="dbContextLifetime">DbContext 수명 주기 (기본: Scoped)</param>
    public static void AddDependencyInjectionContainerForEmployeeApp(
        this IServiceCollection services,
        IConfiguration configuration,
        RepositoryMode mode = RepositoryMode.EfCore,
        ServiceLifetime dbContextLifetime = ServiceLifetime.Scoped)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DefaultConnection is not configured properly.");

        services.AddDependencyInjectionContainerForEmployeeApp(
            connectionString,
            mode,
            dbContextLifetime);
    }
}
