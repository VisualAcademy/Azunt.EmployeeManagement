using System;
using System.Linq; // Count(), First() 확장 메서드
using System.Threading.Tasks;
using Azunt.EmployeeManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Azunt.EmployeeManagement.Tests;

[TestClass]
public class EmployeeRepositoryTests
{
    // InMemory DbContextOptions를 보관하고, 매 호출마다 새 컨텍스트를 반환
    private sealed class TestableEmployeeRepository : EmployeeRepository
    {
        private readonly DbContextOptions<EmployeeDbContext> _options;

        public TestableEmployeeRepository(
            DbContextOptions<EmployeeDbContext> options,
            ILoggerFactory loggerFactory)
            : base(new EmployeeDbContextFactory(), loggerFactory)
        {
            _options = options;
        }

        // 기반 시그니처와 정확히 일치(기반: protected virtual)
        protected override EmployeeDbContext CreateContext(string? connectionString)
            => new EmployeeDbContext(_options);
    }

    private static TestableEmployeeRepository CreateRepository(string dbName)
    {
        var options = new DbContextOptionsBuilder<EmployeeDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();

        return new TestableEmployeeRepository(options, loggerFactory);
    }

    [TestMethod]
    public async Task AddAndGetById_Works()
    {
        var repo = CreateRepository(Guid.NewGuid().ToString());

        var employee = new Employee
        {
            Name = "홍길동",
            FirstName = "길동",
            LastName = "홍",
            Active = true,
            CreatedBy = "test"
        };

        await repo.AddAsync(employee);
        var result = await repo.GetByIdAsync(employee.Id);

        Assert.AreEqual("홍길동", result.Name);
    }

    [TestMethod]
    public async Task Update_Works()
    {
        var repo = CreateRepository(Guid.NewGuid().ToString());
        var employee = new Employee { Name = "OldName" };
        await repo.AddAsync(employee);

        employee.Name = "NewName";
        var updated = await repo.UpdateAsync(employee);

        Assert.IsTrue(updated);

        var result = await repo.GetByIdAsync(employee.Id);
        Assert.AreEqual("NewName", result.Name);
    }

    [TestMethod]
    public async Task Delete_Works()
    {
        var repo = CreateRepository(Guid.NewGuid().ToString());
        var employee = new Employee { Name = "DeleteMe" };
        await repo.AddAsync(employee);

        var deleted = await repo.DeleteAsync(employee.Id);
        Assert.IsTrue(deleted);

        var result = await repo.GetByIdAsync(employee.Id);
        Assert.AreEqual(0, result.Id);
    }

    [TestMethod]
    public async Task PagingAndSorting_Works()
    {
        var repo = CreateRepository(Guid.NewGuid().ToString());
        await repo.AddAsync(new Employee { Name = "Alpha" });
        await repo.AddAsync(new Employee { Name = "Bravo" });
        await repo.AddAsync(new Employee { Name = "Charlie" });

        var result = await repo.GetAllAsync<int>(
            pageIndex: 0,
            pageSize: 2,
            searchField: "",
            searchQuery: "",
            sortOrder: "Name",
            parentIdentifier: 0);

        Assert.AreEqual(3, result.TotalCount);
        Assert.AreEqual(2, result.Items.Count());
        Assert.AreEqual("Alpha", result.Items.First().Name);
    }
}
