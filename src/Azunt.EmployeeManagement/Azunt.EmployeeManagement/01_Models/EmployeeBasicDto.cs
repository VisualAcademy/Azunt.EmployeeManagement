namespace Azunt.EmployeeManagement
{
    /// <summary>
    /// Represents the basic identifying information of an employee,
    /// such as ID and full name. 
    /// Suitable for modules that require lightweight employee references.
    /// </summary>
    public class EmployeeBasicDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the employee.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the first name of the employee.
        /// Defaults to an empty string to avoid null values.
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last name of the employee.
        /// Defaults to an empty string to avoid null values.
        /// </summary>
        public string LastName { get; set; } = string.Empty;
    }
}
