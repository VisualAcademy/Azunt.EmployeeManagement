using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Azunt.EmployeeManagement
{
    /// <summary>
    /// Represents the Employee table mapped to the database.
    /// Each property corresponds to a column in the Employees table.
    /// </summary>
    [Table("Employees")]
    public class Employee
    {
        /// <summary>
        /// Employee unique identifier (auto-increment).
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// Indicates whether the employee is active. Default is true.
        /// </summary>
        public bool? Active { get; set; }

        /// <summary>
        /// Record creation timestamp.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Name of the record creator.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Full name of the employee.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Employee's first name.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Employee's last name.
        /// </summary>
        public string? LastName { get; set; }
    }
}
