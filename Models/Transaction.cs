using SQLite;

namespace MoneyFlowWPF.Models
{
    public class Transaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;

        // Строка оставлена для обратной совместимости (можно удалить позже)
        public string Category { get; set; } = string.Empty;

        public bool IsIncome { get; set; }
        public string MerchantPattern { get; set; } = string.Empty;

        // Новое поле — основное
        public int CategoryId { get; set; } = 0; // 0 = не назначена

        // Внутренний перевод (между своими счетами)
        public bool IsInternalTransfer { get; set; } = false;
    }
}