using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Azunt.EmployeeManagement;

public class EmployeesTableBuilder
{
    private readonly string _masterConnectionString;
    private readonly ILogger<EmployeesTableBuilder> _logger;
    private readonly bool _enableSeeding;

    public EmployeesTableBuilder(string masterConnectionString, ILogger<EmployeesTableBuilder> logger, bool enableSeeding = true)
    {
        _masterConnectionString = masterConnectionString;
        _logger = logger;
        _enableSeeding = enableSeeding;
    }

    public void BuildTenantDatabases()
    {
        var tenantConnectionStrings = GetTenantConnectionStrings();

        foreach (var connStr in tenantConnectionStrings)
        {
            try
            {
                EnsureEmployeesTable(connStr);
                _logger.LogInformation("Employees table processed for tenant database.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tenant database.");
            }
        }
    }

    public void BuildMasterDatabase()
    {
        try
        {
            EnsureEmployeesTable(_masterConnectionString);
            _logger.LogInformation("Employees table processed for master database.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing master database.");
        }
    }

    private List<string> GetTenantConnectionStrings()
    {
        var result = new List<string>();

        using var connection = new SqlConnection(_masterConnectionString);
        connection.Open();

        using var cmd = new SqlCommand("SELECT ConnectionString FROM dbo.Tenants", connection);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var connectionString = reader["ConnectionString"]?.ToString();
            if (!string.IsNullOrEmpty(connectionString))
            {
                result.Add(connectionString);
            }
        }

        return result;
    }

    private void EnsureEmployeesTable(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        // 1) 테이블 존재 여부 확인
        using (var cmdCheck = new SqlCommand(@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Employees'", connection))
        {
            int tableCount = (int)cmdCheck.ExecuteScalar();

            if (tableCount == 0)
            {
                // 2) 신규 생성 (Created, Email 포함)
                using var cmdCreate = new SqlCommand(@"
                    CREATE TABLE [dbo].[Employees] (
                        [Id]         BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [Active]     BIT NULL CONSTRAINT DF_Employees_Active DEFAULT ((1)),
                        [CreatedAt]  DATETIMEOFFSET NULL CONSTRAINT DF_Employees_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
                        [CreatedBy]  NVARCHAR(255) NULL,
                        [Name]       NVARCHAR(MAX) NULL,
                        [FirstName]  NVARCHAR(255) NULL,
                        [LastName]   NVARCHAR(255) NULL,
                        [Created]    DATETIMEOFFSET NULL CONSTRAINT DF_Employees_Created DEFAULT (GETDATE()),
                        [Email]      NVARCHAR(254) NULL
                    );", connection);

                cmdCreate.ExecuteNonQuery();
                _logger.LogInformation("Employees table created.");
            }
            else
            {
                // 3) 누락된 컬럼만 추가 (Created, Email 포함)
                var expectedColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Active"] = "BIT NULL CONSTRAINT DF_Employees_Active DEFAULT ((1))",
                    ["CreatedAt"] = "DATETIMEOFFSET NULL CONSTRAINT DF_Employees_CreatedAt DEFAULT SYSDATETIMEOFFSET()",
                    ["CreatedBy"] = "NVARCHAR(255) NULL",
                    ["Name"] = "NVARCHAR(MAX) NULL",
                    ["FirstName"] = "NVARCHAR(255) NULL",
                    ["LastName"] = "NVARCHAR(255) NULL",
                    ["Created"] = "DATETIMEOFFSET NULL CONSTRAINT DF_Employees_Created DEFAULT (GETDATE())",
                    ["Email"] = "NVARCHAR(254) NULL"
                };

                foreach (var (columnName, typeClause) in expectedColumns)
                {
                    if (!ColumnExists(connection, "dbo", "Employees", columnName))
                    {
                        using var alterCmd = new SqlCommand(
                            $"ALTER TABLE [dbo].[Employees] ADD [{columnName}] {typeClause};", connection);
                        alterCmd.ExecuteNonQuery();
                        _logger.LogInformation("Column added: {Col} ({Type})", columnName, typeClause);
                    }
                }
            }
        }

        // 4) 초기 데이터 삽입 (토글 켜져 있을 때만) - Created, Email 포함
        if (_enableSeeding)
        {
            using var cmdCountRows = new SqlCommand("SELECT COUNT(*) FROM [dbo].[Employees];", connection);
            int rowCount = (int)cmdCountRows.ExecuteScalar();

            if (rowCount == 0)
            {
                using var cmdInsertDefaults = new SqlCommand(@"
                    INSERT INTO [dbo].[Employees] (Active, CreatedAt, CreatedBy, Name, FirstName, LastName, Created, Email)
                    VALUES
                        (1, SYSDATETIMEOFFSET(), N'System', N'Initial Employee 1', N'Initial', N'Employee1', GETDATE(), N'initial1@example.com'),
                        (1, SYSDATETIMEOFFSET(), N'System', N'Initial Employee 2', N'Initial', N'Employee2', GETDATE(), N'initial2@example.com');", connection);

                int inserted = cmdInsertDefaults.ExecuteNonQuery();
                _logger.LogInformation("Employees seed inserted: {Count}", inserted);
            }
        }
    }

    private static bool ColumnExists(SqlConnection connection, string schema, string table, string column)
    {
        using var cmd = new SqlCommand(@"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table AND COLUMN_NAME = @Column;", connection);

        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);
        cmd.Parameters.AddWithValue("@Column", column);

        return (int)cmd.ExecuteScalar() > 0;
    }

    public static void Run(IServiceProvider services, bool forMaster, bool enableSeeding = true)
    {
        try
        {
            var logger = services.GetRequiredService<ILogger<EmployeesTableBuilder>>();
            var config = services.GetRequiredService<IConfiguration>();
            var masterConnectionString = config.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(masterConnectionString))
                throw new InvalidOperationException("DefaultConnection is not configured in appsettings.json.");

            var builder = new EmployeesTableBuilder(masterConnectionString, logger, enableSeeding);

            if (forMaster)
                builder.BuildMasterDatabase();
            else
                builder.BuildTenantDatabases();
        }
        catch (Exception ex)
        {
            var fallbackLogger = services.GetService<ILogger<EmployeesTableBuilder>>();
            fallbackLogger?.LogError(ex, "Error while processing Employees table.");
        }
    }
}
