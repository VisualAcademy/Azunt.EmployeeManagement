using Microsoft.EntityFrameworkCore;

namespace Azunt.EmployeeManagement
{
    /// <summary>
    /// EmployeeApp에서 사용하는 EF Core DbContext.
    /// 애그리게이트 루트(Employee 등)에 대한 매핑과 공통 규칙을 구성합니다.
    /// </summary>
    public class EmployeeDbContext : DbContext
    {
        /// <summary>
        /// DbContextOptions를 받는 기본 생성자.
        /// 주로 Program.cs/Startup.cs 등록에서 사용합니다.
        /// </summary>
        public EmployeeDbContext(DbContextOptions<EmployeeDbContext> options)
            : base(options)
        {
            // 기본 조회는 변경 추적 없이 수행 (쓰기 시나리오에서는 AsTracking() 사용)
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        /// <summary>
        /// Employees 테이블에 매핑되는 엔터티 집합.
        /// </summary>
        public DbSet<Employee> Employees { get; set; } = null!;

        /// <summary>
        /// 모델 구성: 컬럼 타입/기본값 등 스키마 규칙 정의.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Employee>(entity =>
            {
                // CreatedAt: 테이블 스키마와 일치 (datetimeoffset + SYSDATETIMEOFFSET())
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("datetimeoffset")
                      .HasDefaultValueSql("SYSDATETIMEOFFSET()")
                      .IsRequired(false);
            });
        }
    }
}
