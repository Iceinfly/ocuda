using System.Collections.Generic;

namespace Ocuda.HappyFoxHelper.Models
{
    public class Category
    {
        public string Description { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string PrepopulateCc { get; set; }
        public bool Public { get; set; }
        public bool TimeSpentMandatory { get; set; }
    }

    public class Priority
    {
        public bool Default { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
    }

    public class Status
    {
        public string Behavior { get; set; }
        public string Color { get; set; }
        public bool Default { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
    }

    public class Staff
    {
        public bool Active { get; set; }
        public IReadOnlyCollection<int> Categories { get; set; } = new List<int>();
        public string Email { get; set; }
        public string FirstName { get; set; }
        public int Id { get; set; }
        public bool IsAccountAdmin { get; set; }
        public string LastName { get; set; }
        public string Name { get; set; }
        public IReadOnlyCollection<string> Permissions { get; set; } = new List<string>();
    }

    public class CustomField
    {
        public IReadOnlyCollection<CustomFieldCategory> Categories { get; set; }
            = new List<CustomFieldCategory>();
        public IReadOnlyCollection<CustomFieldChoice> Choices { get; set; }
            = new List<CustomFieldChoice>();
        public bool CompulsoryOnCompleted { get; set; }
        public bool CompulsoryOnMove { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
        public bool Required { get; set; }
        public string Type { get; set; }
        public bool VisibleToStaffOnly { get; set; }
    }

    public class CustomFieldCategory
    {
        public int Category { get; set; }
        public int Order { get; set; }
    }

    public class CustomFieldChoice
    {
        public IReadOnlyCollection<int> DependantFields { get; set; } = new List<int>();
        public int Id { get; set; }
        public string Text { get; set; }
    }
}
