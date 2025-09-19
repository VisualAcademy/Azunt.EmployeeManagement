using Azunt.Models.Common;

namespace Azunt.EmployeeManagement;

public interface IEmployeeRepository
{
    Task<Employee> AddAsync(Employee model, string? connectionString = null);
    Task<List<Employee>> GetAllAsync(string? connectionString = null);
    Task<Employee> GetByIdAsync(long id, string? connectionString = null);
    Task<bool> UpdateAsync(Employee model, string? connectionString = null);
    Task<bool> DeleteAsync(long id, string? connectionString = null);
    Task<ArticleSet<Employee, int>> GetAllAsync<TParentIdentifier>(int pageIndex, int pageSize, string searchField, string searchQuery, string sortOrder, TParentIdentifier parentIdentifier, string? connectionString = null);
}
