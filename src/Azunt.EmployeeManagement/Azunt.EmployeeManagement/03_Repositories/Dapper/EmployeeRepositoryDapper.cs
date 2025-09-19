using Azunt.Models.Common;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Azunt.EmployeeManagement;

/// <summary>
/// Dapper 기반의 Employee 리포지토리 구현체입니다.
/// CRUD, 검색, 페이징, 정렬을 지원하며 멀티 테넌트 연결 문자열을 처리할 수 있습니다.
/// - WHERE 조건은 파라미터 바인딩으로 SQL 인젝션을 방지합니다.
/// - ORDER BY는 화이트리스트 방식으로만 문자열을 조합합니다.
/// - CreatedAt은 DB의 SYSDATETIMEOFFSET()을 사용해 서버 시간 기준으로 기록합니다.
/// </summary>
public class EmployeeRepositoryDapper : IEmployeeRepository
{
    private readonly string _defaultConnectionString;
    private readonly ILogger<EmployeeRepositoryDapper> _logger;

    public EmployeeRepositoryDapper(string defaultConnectionString, ILoggerFactory loggerFactory)
    {
        _defaultConnectionString = defaultConnectionString;
        _logger = loggerFactory.CreateLogger<EmployeeRepositoryDapper>();
    }

    private SqlConnection GetConnection(string? connectionString) =>
        new SqlConnection(connectionString ?? _defaultConnectionString);

    /// <summary>
    /// 신규 직원 개체 추가 (CreatedAt은 DB의 SYSDATETIMEOFFSET() 사용)
    /// </summary>
    public async Task<Employee> AddAsync(Employee model, string? connectionString = null)
    {
        const string sql = @"
INSERT INTO [dbo].[Employees] (Active, CreatedAt, CreatedBy, [Name], FirstName, LastName)
OUTPUT INSERTED.Id
VALUES (@Active, SYSDATETIMEOFFSET(), @CreatedBy, @Name, @FirstName, @LastName);";

        using var conn = GetConnection(connectionString);
        // Dapper는 null을 자동으로 DBNull로 매핑하므로 그대로 전달해도 됩니다.
        model.Id = await conn.ExecuteScalarAsync<long>(sql, new
        {
            model.Active,
            model.CreatedBy,
            model.Name,
            model.FirstName,
            model.LastName
        });

        return model; // CreatedAt은 DB에서 설정되므로 필요 시 재조회해도 됩니다.
    }

    /// <summary>
    /// 전체 목록 조회 (기본: Id DESC)
    /// </summary>
    public async Task<List<Employee>> GetAllAsync(string? connectionString = null)
    {
        const string sql = @"
SELECT Id, Active, CreatedAt, CreatedBy, [Name], FirstName, LastName
FROM [dbo].[Employees]
ORDER BY Id DESC;";

        using var conn = GetConnection(connectionString);
        var rows = await conn.QueryAsync<Employee>(sql);
        return rows.AsList();
    }

    /// <summary>
    /// Id로 단건 조회 (없으면 빈 개체 반환)
    /// </summary>
    public async Task<Employee> GetByIdAsync(long id, string? connectionString = null)
    {
        const string sql = @"
SELECT Id, Active, CreatedAt, CreatedBy, [Name], FirstName, LastName
FROM [dbo].[Employees]
WHERE Id = @Id;";

        using var conn = GetConnection(connectionString);
        var model = await conn.QuerySingleOrDefaultAsync<Employee>(sql, new { Id = id });
        return model ?? new Employee();
    }

    /// <summary>
    /// 직원 개체 수정 (필요 필드만 업데이트)
    /// </summary>
    public async Task<bool> UpdateAsync(Employee model, string? connectionString = null)
    {
        const string sql = @"
UPDATE [dbo].[Employees]
SET
    Active    = @Active,
    [Name]    = @Name,
    FirstName = @FirstName,
    LastName  = @LastName,
    CreatedBy = @CreatedBy
WHERE Id = @Id;";

        using var conn = GetConnection(connectionString);
        var affected = await conn.ExecuteAsync(sql, new
        {
            model.Active,
            model.Name,
            model.FirstName,
            model.LastName,
            model.CreatedBy,
            model.Id
        });
        return affected > 0;
    }

    /// <summary>
    /// 직원 개체 삭제
    /// </summary>
    public async Task<bool> DeleteAsync(long id, string? connectionString = null)
    {
        const string sql = @"DELETE FROM [dbo].[Employees] WHERE Id = @Id;";

        using var conn = GetConnection(connectionString);
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    /// <summary>
    /// 페이징 + 검색 + 정렬 지원 목록 조회
    /// </summary>
    public async Task<ArticleSet<Employee, int>> GetAllAsync<TParentIdentifier>(
        int pageIndex,
        int pageSize,
        string searchField,    // 현재는 미사용(호환용). 필요 시 단일 필드 검색으로 확장 가능.
        string searchQuery,
        string sortOrder,
        TParentIdentifier parentIdentifier,
        string? connectionString = null)
    {
        // WHERE 구성
        var whereList = new List<string> { "1=1" };
        var param = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            whereList.Add("([Name] LIKE @q OR FirstName LIKE @q OR LastName LIKE @q)");
            param.Add("@q", $"%{searchQuery}%");
        }

        string where = string.Join(" AND ", whereList);
        string orderBy = BuildOrderBy(sortOrder);

        // COUNT + PAGE 데이터 쿼리
        string countSql = $@"
SELECT COUNT(*)
FROM [dbo].[Employees]
WHERE {where};";

        string dataSql = $@"
SELECT Id, Active, CreatedAt, CreatedBy, [Name], FirstName, LastName
FROM [dbo].[Employees]
WHERE {where}
{orderBy}
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        param.Add("@Offset", pageIndex * pageSize);
        param.Add("@PageSize", pageSize);

        using var conn = GetConnection(connectionString);

        // 멀티 쿼리 순차 실행
        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, param);
        var items = (await conn.QueryAsync<Employee>(dataSql, param)).AsList();

        return new ArticleSet<Employee, int>(items, totalCount);
    }

    /// <summary>
    /// ORDER BY 절을 화이트리스트로 빌드 (SQL 인젝션 방지)
    /// 지원: Id, IdDesc, Name, NameDesc, FirstName, FirstNameDesc, LastName, LastNameDesc, CreatedAt, CreatedAtDesc, Active, ActiveDesc
    /// 기본: Id DESC
    /// </summary>
    private static string BuildOrderBy(string sortOrder)
    {
        return (sortOrder ?? string.Empty).Trim() switch
        {
            "Id" => "ORDER BY Id ASC",
            "IdDesc" => "ORDER BY Id DESC",

            "Name" => "ORDER BY [Name] ASC, Id DESC",
            "NameDesc" => "ORDER BY [Name] DESC, Id DESC",

            "FirstName" => "ORDER BY FirstName ASC, LastName ASC, Id DESC",
            "FirstNameDesc" => "ORDER BY FirstName DESC, LastName DESC, Id DESC",

            "LastName" => "ORDER BY LastName ASC, FirstName ASC, Id DESC",
            "LastNameDesc" => "ORDER BY LastName DESC, FirstName DESC, Id DESC",

            "CreatedAt" => "ORDER BY CreatedAt ASC, Id DESC",
            "CreatedAtDesc" => "ORDER BY CreatedAt DESC, Id DESC",

            "Active" => "ORDER BY Active ASC, Id DESC",
            "ActiveDesc" => "ORDER BY Active DESC, Id DESC",

            _ => "ORDER BY Id DESC"
        };
    }
}
