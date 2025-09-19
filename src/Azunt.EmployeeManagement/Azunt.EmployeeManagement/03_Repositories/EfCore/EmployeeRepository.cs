using Azunt.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Azunt.EmployeeManagement;

/// <summary>
/// EF Core 기반의 Employee 리포지토리 구현체입니다.
/// CRUD, 검색, 페이징, 정렬을 지원하며 멀티 테넌트 연결 문자열을 처리할 수 있습니다.
/// </summary>
public class EmployeeRepository : IEmployeeRepository
{
    private readonly EmployeeDbContextFactory _factory;
    private readonly ILogger<EmployeeRepository> _logger;

    public EmployeeRepository(
        EmployeeDbContextFactory factory,
        ILoggerFactory loggerFactory)
    {
        _factory = factory;
        _logger = loggerFactory.CreateLogger<EmployeeRepository>();
    }

    private EmployeeDbContext CreateContext(string? connectionString) =>
        string.IsNullOrEmpty(connectionString)
            ? _factory.CreateDbContext()
            : _factory.CreateDbContext(connectionString);

    /// <summary>
    /// 신규 직원 추가
    /// </summary>
    public async Task<Employee> AddAsync(Employee model, string? connectionString = null)
    {
        await using var context = CreateContext(connectionString);

        // DB 기본값 대신 코드 일관성을 원하면 아래 라인 유지
        model.CreatedAt = DateTimeOffset.UtcNow;

        context.Employees.Add(model);
        await context.SaveChangesAsync();
        return model;
    }

    /// <summary>
    /// 전체 목록 조회 (기본: Id DESC)
    /// </summary>
    public async Task<List<Employee>> GetAllAsync(string? connectionString = null)
    {
        await using var context = CreateContext(connectionString);
        return await context.Employees
            .OrderByDescending(m => m.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Id로 단건 조회 (없으면 빈 객체 반환)
    /// </summary>
    public async Task<Employee> GetByIdAsync(long id, string? connectionString = null)
    {
        await using var context = CreateContext(connectionString);
        return await context.Employees.SingleOrDefaultAsync(m => m.Id == id)
               ?? new Employee();
    }

    /// <summary>
    /// 직원 수정 (필요 필드만 업데이트)
    /// </summary>
    public async Task<bool> UpdateAsync(Employee model, string? connectionString = null)
    {
        await using var context = CreateContext(connectionString);

        var entity = await context.Employees.FirstOrDefaultAsync(e => e.Id == model.Id);
        if (entity is null) return false;

        entity.Active = model.Active;
        entity.Name = model.Name;
        entity.FirstName = model.FirstName;
        entity.LastName = model.LastName;
        entity.CreatedBy = model.CreatedBy;

        return await context.SaveChangesAsync() > 0;
    }

    /// <summary>
    /// 직원 삭제
    /// </summary>
    public async Task<bool> DeleteAsync(long id, string? connectionString = null)
    {
        await using var context = CreateContext(connectionString);
        var entity = await context.Employees.FindAsync(id);
        if (entity == null) return false;

        context.Employees.Remove(entity);
        return await context.SaveChangesAsync() > 0;
    }

    /// <summary>
    /// 페이징 + 검색 + 정렬 지원 목록 조회
    /// </summary>
    public async Task<ArticleSet<Employee, int>> GetAllAsync<TParentIdentifier>(
        int pageIndex,
        int pageSize,
        string searchField,    // 현재는 미사용(호환용). 필요 시 필드별 검색으로 확장 가능.
        string searchQuery,
        string sortOrder,
        TParentIdentifier parentIdentifier,
        string? connectionString = null)
    {
        await using var context = CreateContext(connectionString);

        var query = context.Employees.AsQueryable();

        // 검색: Name / FirstName / LastName 대상 LIKE
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var like = $"%{searchQuery}%";
            query = query.Where(m =>
                (m.Name != null && EF.Functions.Like(m.Name, like)) ||
                (m.FirstName != null && EF.Functions.Like(m.FirstName, like)) ||
                (m.LastName != null && EF.Functions.Like(m.LastName, like)));
        }

        // 정렬 적용
        query = ApplySorting(query, sortOrder);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ArticleSet<Employee, int>(items, totalCount);
    }

    /// <summary>
    /// sortOrder 문자열을 해석하여 안전한 정렬을 적용합니다.
    /// 지원: Id, IdDesc, Name, NameDesc, FirstName, FirstNameDesc, LastName, LastNameDesc, CreatedAt, CreatedAtDesc, Active, ActiveDesc
    /// 기본: IdDesc
    /// </summary>
    private static IQueryable<Employee> ApplySorting(IQueryable<Employee> query, string sortOrder)
    {
        var key = (sortOrder ?? string.Empty).Trim();

        return key switch
        {
            "Id" => query.OrderBy(e => e.Id),
            "IdDesc" => query.OrderByDescending(e => e.Id),

            "Name" => query.OrderBy(e => e.Name ?? string.Empty)
                                      .ThenByDescending(e => e.Id),
            "NameDesc" => query.OrderByDescending(e => e.Name ?? string.Empty)
                                      .ThenByDescending(e => e.Id),

            "FirstName" => query.OrderBy(e => e.FirstName ?? string.Empty)
                                      .ThenBy(e => e.LastName ?? string.Empty)
                                      .ThenByDescending(e => e.Id),
            "FirstNameDesc" => query.OrderByDescending(e => e.FirstName ?? string.Empty)
                                      .ThenByDescending(e => e.LastName ?? string.Empty)
                                      .ThenByDescending(e => e.Id),

            "LastName" => query.OrderBy(e => e.LastName ?? string.Empty)
                                      .ThenBy(e => e.FirstName ?? string.Empty)
                                      .ThenByDescending(e => e.Id),
            "LastNameDesc" => query.OrderByDescending(e => e.LastName ?? string.Empty)
                                      .ThenByDescending(e => e.FirstName ?? string.Empty)
                                      .ThenByDescending(e => e.Id),

            "CreatedAt" => query.OrderBy(e => e.CreatedAt)
                                      .ThenByDescending(e => e.Id),
            "CreatedAtDesc" => query.OrderByDescending(e => e.CreatedAt)
                                      .ThenByDescending(e => e.Id),

            "Active" => query.OrderBy(e => e.Active ?? false)
                                      .ThenByDescending(e => e.Id),
            "ActiveDesc" => query.OrderByDescending(e => e.Active ?? false)
                                      .ThenByDescending(e => e.Id),

            _ => query.OrderByDescending(e => e.Id) // 기본값
        };
    }
}
