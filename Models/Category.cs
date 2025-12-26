using SQLite;

namespace MoneyFlowWPF.Models
{
    public class Category
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsIncome { get; set; } // true - категория доходов, false - категория расходов

        public string? ParentCategory { get; set; } // для иерархии (позже)

        public string Color { get; set; } = "#808080"; // цвет для отображения
    }
}