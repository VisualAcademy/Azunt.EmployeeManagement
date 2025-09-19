using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Azunt.EmployeeManagement;

/// <summary>
/// EmployeeDbContext 인스턴스를 생성하는 Factory 클래스
/// </summary>
public class EmployeeDbContextFactory
{
    private readonly IConfiguration? _configuration;

    /// <summary>
    /// 기본 생성자 (Configuration 없이 사용 가능)
    /// </summary>
    public EmployeeDbContextFactory()
    {
    }

    /// <summary>
    /// IConfiguration을 주입받는 생성자
    /// </summary>
    public EmployeeDbContextFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// 연결 문자열을 사용하여 DbContext 인스턴스를 생성합니다.
    /// </summary>
    public EmployeeDbContext CreateDbContext(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));
        }

        var options = new DbContextOptionsBuilder<EmployeeDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new EmployeeDbContext(options);
    }

    /// <summary>
    /// DbContextOptions를 사용하여 DbContext 인스턴스를 생성합니다.
    /// </summary>
    public EmployeeDbContext CreateDbContext(DbContextOptions<EmployeeDbContext> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new EmployeeDbContext(options);
    }

    /// <summary>
    /// appsettings.json의 "DefaultConnection"을 사용하여 DbContext 인스턴스를 생성합니다.
    /// </summary>
    public EmployeeDbContext CreateDbContext()
    {
        if (_configuration == null)
        {
            throw new InvalidOperationException("Configuration is not provided.");
        }

        var defaultConnection = _configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(defaultConnection))
        {
            throw new InvalidOperationException("DefaultConnection is not configured properly.");
        }

        return CreateDbContext(defaultConnection);
    }
}
