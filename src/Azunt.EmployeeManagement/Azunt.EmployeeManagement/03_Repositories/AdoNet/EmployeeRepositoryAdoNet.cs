using Azunt.Models.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Azunt.EmployeeManagement;

/// <summary>
/// ADO.NET 기반의 Employee 리포지토리 구현체입니다.
/// CRUD, 검색, 페이징, 정렬을 지원하며 멀티 테넌트 연결 문자열을 처리할 수 있습니다.
/// - SQL 인젝션 방지를 위해 WHERE 조건은 파라미터 바인딩, ORDER BY는 화이트리스트 방식으로 처리합니다.
/// - CreatedAt은 DB의 SYSDATETIMEOFFSET()을 사용해 일관된 서버 시간으로 기록합니다.
/// </summary>
public class EmployeeRepositoryAdoNet : IEmployeeRepository
{
    private readonly string _defaultConnectionString;
    private readonly ILogger<EmployeeRepositoryAdoNet> _logger;

    public EmployeeRepositoryAdoNet(string defaultConnectionString, ILoggerFactory loggerFactory)
    {
        _defaultConnectionString = defaultConnectionString;
        _logger = loggerFactory.CreateLogger<EmployeeRepositoryAdoNet>();
    }

    private SqlConnection GetConnection(string? connectionString) =>
        new SqlConnection(connectionString ?? _defaultConnectionString);

    /// <summary>
    /// 신규 직원 추가 (CreatedAt은 DB의 SYSDATETIMEOFFSET() 사용)
    /// </summary>
    public async Task<Employee> AddAsync(Employee model, string? connectionString = null)
    {
        await using var conn = GetConnection(connectionString);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO [dbo].[Employees] (Active, CreatedAt, CreatedBy, [Name], FirstName, LastName)
            OUTPUT INSERTED.Id
            VALUES (@Active, SYSDATETIMEOFFSET(), @CreatedBy, @Name, @FirstName, @LastName);";

        cmd.Parameters.AddWithValue("@Active", (object?)model.Active ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedBy", (object?)model.CreatedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Name", (object?)model.Name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FirstName", (object?)model.FirstName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastName", (object?)model.LastName ?? DBNull.Value);

        await conn.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        if (result is null or DBNull)
            throw new InvalidOperationException("Failed to insert Employee. No ID was returned.");

        model.Id = Convert.ToInt64(result);
        // CreatedAt은 DB에서 설정되므로 필요 시 재조회하여 값 보강 가능
        return model;
    }

    /// <summary>
    /// 전체 목록 조회 (기본: Id DESC)
    /// </summary>
    public async Task<List<Employee>> GetAllAsync(string? connectionString = null)
    {
        var list = new List<Employee>();
        await using var conn = GetConnection(connectionString);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Active, CreatedAt, CreatedBy, [Name], FirstName, LastName
            FROM [dbo].[Employees]
            ORDER BY Id DESC;";

        await conn.OpenAsync();
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        while (await reader.ReadAsync())
        {
            list.Add(MapEmployee(reader));
        }

        return list;
    }

    /// <summary>
    /// Id로 단건 조회 (없으면 빈 개체 반환)
    /// </summary>
    public async Task<Employee> GetByIdAsync(long id, string? connectionString = null)
    {
        await using var conn = GetConnection(connectionString);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Active, CreatedAt, CreatedBy, [Name], FirstName, LastName
            FROM [dbo].[Employees]
            WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);

        await conn.OpenAsync();
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (await reader.ReadAsync())
            return MapEmployee(reader);

        return new Employee(); // 인터페이스 시그니처와 일치: null 대신 빈 개체 반환
    }

    /// <summary>
    /// 직원 수정 (필요 필드만 업데이트)
    /// </summary>
    public async Task<bool> UpdateAsync(Employee model, string? connectionString = null)
    {
        await using var conn = GetConnection(connectionString);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE [dbo].[Employees]
            SET
                Active    = @Active,
                [Name]    = @Name,
                FirstName = @FirstName,
                LastName  = @LastName,
                CreatedBy = @CreatedBy
            WHERE Id = @Id;";

        cmd.Parameters.AddWithValue("@Active", (object?)model.Active ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Name", (object?)model.Name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FirstName", (object?)model.FirstName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastName", (object?)model.LastName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedBy", (object?)model.CreatedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Id", model.Id);

        await conn.OpenAsync();
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>
    /// 직원 삭제
    /// </summary>
    public async Task<bool> DeleteAsync(long id, string? connectionString = null)
    {
        await using var conn = GetConnection(connectionString);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM [dbo].[Employees] WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);

        await conn.OpenAsync();
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>
    /// 페이징 + 검색 + 정렬 지원 목록 조회
    /// </summary>
    public async Task<ArticleSet<Employee, int>> GetAllAsync<TParentIdentifier>(
        int pageIndex,
        int pageSize,
        string searchField,     // 현재는 미사용(호환용). 필요 시 단일 필드 검색으로 확장 가능.
        string searchQuery,
        string sortOrder,
        TParentIdentifier parentIdentifier,
        string? connectionString = null)
    {
        var items = new List<Employee>();
        int totalCount = 0;

        await using var conn = GetConnection(connectionString);
        await conn.OpenAsync();

        // 1) WHERE 절 구성 (파라미터 바인딩)
        using var cmd = conn.CreateCommand();

        var whereClauses = new List<string> { "1=1" }; // 기본 true
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            // Name/FirstName/LastName LIKE 검색
            whereClauses.Add("( [Name] LIKE @q OR FirstName LIKE @q OR LastName LIKE @q )");
            cmd.Parameters.AddWithValue("@q", $"%{searchQuery}%");
        }

        string where = string.Join(" AND ", whereClauses);

        // 2) ORDER BY (화이트리스트)
        string orderBy = BuildOrderBy(sortOrder);

        // 3) 총 개수 + 페이지 아이템 (멀티쿼리)
        cmd.CommandText = $@"
            SELECT COUNT(*)
            FROM [dbo].[Employees]
            WHERE {where};

            SELECT Id, Active, CreatedAt, CreatedBy, [Name], FirstName, LastName
            FROM [dbo].[Employees]
            WHERE {where}
            {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        cmd.Parameters.AddWithValue("@Offset", pageIndex * pageSize);
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        await using var reader = await cmd.ExecuteReaderAsync();

        // totalCount
        if (await reader.ReadAsync())
            totalCount = reader.GetInt32(0);

        // items
        await reader.NextResultAsync();
        while (await reader.ReadAsync())
            items.Add(MapEmployee(reader));

        return new ArticleSet<Employee, int>(items, totalCount);
    }

    /// <summary>
    /// 데이터 리더에서 Employee 인스턴스로 매핑
    /// </summary>
    private static Employee MapEmployee(SqlDataReader reader)
    {
        return new Employee
        {
            Id = reader.GetInt64(0),
            Active = reader.IsDBNull(1) ? (bool?)null : reader.GetBoolean(1),
            CreatedAt = reader.GetDateTimeOffset(2),
            CreatedBy = reader.IsDBNull(3) ? null : reader.GetString(3),
            Name = reader.IsDBNull(4) ? null : reader.GetString(4),
            FirstName = reader.IsDBNull(5) ? null : reader.GetString(5),
            LastName = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
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
