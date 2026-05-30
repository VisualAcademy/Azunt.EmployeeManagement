using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Azunt.EmployeeManagement;

/// <summary>
/// Adds the LicenseNumberSort column and IX_Employees_LicenseNumberSort index
/// to the Employees table in master or tenant databases.
/// </summary>
public class EmployeesLicenseNumberSortBuilder
{
    private readonly string _masterConnectionString;
    private readonly ILogger<EmployeesLicenseNumberSortBuilder> _logger;

    public EmployeesLicenseNumberSortBuilder(
        string masterConnectionString,
        ILogger<EmployeesLicenseNumberSortBuilder> logger)
    {
        _masterConnectionString = masterConnectionString;
        _logger = logger;
    }

    public void BuildTenantDatabases()
    {
        var tenantConnectionStrings = GetTenantConnectionStrings();

        foreach (var connStr in tenantConnectionStrings)
        {
            try
            {
                EnsureLicenseNumberSortColumnAndIndex(connStr);
                _logger.LogInformation("Employees LicenseNumberSort column/index processed for tenant database.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Employees LicenseNumberSort column/index for tenant database.");
            }
        }
    }

    public void BuildMasterDatabase()
    {
        try
        {
            EnsureLicenseNumberSortColumnAndIndex(_masterConnectionString);
            _logger.LogInformation("Employees LicenseNumberSort column/index processed for master database.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Employees LicenseNumberSort column/index for master database.");
        }
    }

    private List<string> GetTenantConnectionStrings()
    {
        var result = new List<string>();

        using var connection = new SqlConnection(_masterConnectionString);
        connection.Open();

        using var cmd = new SqlCommand(@"
            SELECT ConnectionString
            FROM dbo.Tenants
            WHERE ConnectionString IS NOT NULL
              AND LTRIM(RTRIM(ConnectionString)) <> '';", connection);

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var connectionString = reader["ConnectionString"]?.ToString();

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                result.Add(connectionString);
            }
        }

        return result;
    }

    private void EnsureLicenseNumberSortColumnAndIndex(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        if (!TableExists(connection, "dbo", "Employees"))
        {
            _logger.LogWarning("dbo.Employees table does not exist. Skipping LicenseNumberSort update.");
            return;
        }

        if (!ColumnExists(connection, "dbo", "Employees", "LicenseNumberSort"))
        {
            using var cmdAddColumn = new SqlCommand(@"
                ALTER TABLE [dbo].[Employees]
                ADD [LicenseNumberSort] BIGINT NULL;", connection);

            cmdAddColumn.ExecuteNonQuery();
            _logger.LogInformation("Column added: dbo.Employees.LicenseNumberSort BIGINT NULL.");
        }

        if (!IndexExists(connection, "dbo", "Employees", "IX_Employees_LicenseNumberSort"))
        {
            using var cmdCreateIndex = new SqlCommand(@"
                CREATE INDEX [IX_Employees_LicenseNumberSort]
                ON [dbo].[Employees] ([LicenseNumberSort], [ID]);", connection);

            cmdCreateIndex.ExecuteNonQuery();
            _logger.LogInformation("Index created: IX_Employees_LicenseNumberSort.");
        }
    }

    private static bool TableExists(SqlConnection connection, string schema, string table)
    {
        using var cmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @Schema
              AND TABLE_NAME = @Table;", connection);

        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);

        return (int)cmd.ExecuteScalar() > 0;
    }

    private static bool ColumnExists(SqlConnection connection, string schema, string table, string column)
    {
        using var cmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema
              AND TABLE_NAME = @Table
              AND COLUMN_NAME = @Column;", connection);

        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);
        cmd.Parameters.AddWithValue("@Column", column);

        return (int)cmd.ExecuteScalar() > 0;
    }

    private static bool IndexExists(SqlConnection connection, string schema, string table, string indexName)
    {
        using var cmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE name = @IndexName
              AND object_id = OBJECT_ID(QUOTENAME(@Schema) + '.' + QUOTENAME(@Table));", connection);

        cmd.Parameters.AddWithValue("@IndexName", indexName);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);

        return (int)cmd.ExecuteScalar() > 0;
    }

    public static void Run(IServiceProvider services, bool forMaster)
    {
        try
        {
            var logger = services.GetRequiredService<ILogger<EmployeesLicenseNumberSortBuilder>>();
            var config = services.GetRequiredService<IConfiguration>();
            var masterConnectionString = config.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(masterConnectionString))
            {
                throw new InvalidOperationException("DefaultConnection is not configured in appsettings.json.");
            }

            var builder = new EmployeesLicenseNumberSortBuilder(masterConnectionString, logger);

            if (forMaster)
            {
                builder.BuildMasterDatabase();
            }
            else
            {
                builder.BuildTenantDatabases();
            }
        }
        catch (Exception ex)
        {
            var fallbackLogger = services.GetService<ILogger<EmployeesLicenseNumberSortBuilder>>();
            fallbackLogger?.LogError(ex, "Error while processing Employees LicenseNumberSort column/index.");
        }
    }
}
