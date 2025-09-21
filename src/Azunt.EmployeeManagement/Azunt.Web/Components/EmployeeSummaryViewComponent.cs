using Microsoft.AspNetCore.Mvc;
using Azunt.EmployeeManagement;

namespace Azunt.Web.Components
{
    public class EmployeeSummaryViewComponent : ViewComponent
    {
        private readonly IEmployeeRepository _repository;

        public EmployeeSummaryViewComponent(IEmployeeRepository repository)
        {
            _repository = repository;
        }

        public class EmployeeSummaryArgs
        {
            public string? Title { get; set; } = "Employees";
            public int Top { get; set; } = 5;
            public bool ActiveOnly { get; set; } = false;
            public bool ShowViewAllLink { get; set; } = true;
            public string? ViewAllUrl { get; set; } = "/Employees";
        }

        public class EmployeeSummaryItem
        {
            public long Id { get; set; }
            public string FirstName { get; set; } = "";
            public string LastName { get; set; } = "";
            public string FullName => BuildFullName(FirstName, LastName);
            public DateTimeOffset? CreatedAt { get; set; } = DateTimeOffset.MinValue;
            public bool? Active { get; set; } = true;
        }

        public class EmployeeSummaryViewModel
        {
            public string Title { get; set; } = "Employees";
            public bool ShowViewAllLink { get; set; } = true;
            public string ViewAllUrl { get; set; } = "/Employees";
            public IReadOnlyList<EmployeeSummaryItem> Items { get; set; } = Array.Empty<EmployeeSummaryItem>();
        }

        public async Task<IViewComponentResult> InvokeAsync(
            string? title = null,
            int? top = null,
            bool? activeOnly = null,
            bool? showViewAllLink = null,
            string? viewAllUrl = null)
        {
            var args = new EmployeeSummaryArgs
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Employees" : title,
                Top = Math.Max(1, top ?? 5),
                ActiveOnly = activeOnly ?? false,
                ShowViewAllLink = showViewAllLink ?? true,
                ViewAllUrl = string.IsNullOrWhiteSpace(viewAllUrl) ? "/Employees" : viewAllUrl
            };

            var all = await _repository.GetAllAsync();

            var items = all.Select(e => new EmployeeSummaryItem
                {
                    Id = e.Id,
                    FirstName = e.FirstName ?? "",
                    LastName = e.LastName ?? "",
                    CreatedAt = e.CreatedAt,
                    Active = e.Active
                })
                .Where(i => !args.ActiveOnly || i.Active.GetValueOrDefault())
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.Id)
                .Take(args.Top)
                .ToList();

            var vm = new EmployeeSummaryViewModel
            {
                Title = args.Title!,
                ShowViewAllLink = args.ShowViewAllLink,
                ViewAllUrl = args.ViewAllUrl!,
                Items = items
            };

            return View(vm);
        }

        private static string BuildFullName(string? first, string? last)
        {
            first ??= "";
            last ??= "";
            if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last))
                return "(No name)";
            var full = $"{first} {last}".Trim();
            return string.IsNullOrWhiteSpace(full) ? "(No name)" : full;
        }
    }
}
