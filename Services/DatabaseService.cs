using MoneyFlowWPF.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MoneyFlowWPF.Services
{
    public class DatabaseService
    {
        private SQLiteConnection _db;

        public DatabaseService()
        {
            string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoneyFlowWPF", "finance.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            _db = new SQLiteConnection(dbPath);
            _db.CreateTable<Transaction>();
            _db.CreateTable<Category>();
            InitializeDefaultCategories();
        }

        public void AddTransaction(Transaction transaction) => _db.Insert(transaction);
        public void UpdateTransaction(Transaction transaction) => _db.Update(transaction);
        public void DeleteTransaction(int id) => _db.Delete<Transaction>(id);

        public List<Transaction> GetAllTransactions() => _db.Table<Transaction>().OrderByDescending(t => t.Date).ToList();
        public List<Transaction> GetTransactionsByDate(DateTime start, DateTime end) =>
            _db.Table<Transaction>().Where(t => t.Date >= start && t.Date <= end).ToList();

        public (decimal TotalIncome, decimal TotalExpense, decimal Balance) GetStatistics()
        {
            var transactions = GetAllTransactions()
                .Where(t => !t.IsInternalTransfer) // ← игнорируем переводы
                .ToList();

            var income = transactions.Where(t => t.IsIncome).Sum(t => t.Amount);
            var expense = transactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
            return (income, expense, income - expense);
        }

        // === Категории ===
        public void AddCategory(Category category) => _db.Insert(category);
        public void UpdateCategory(Category category) => _db.Update(category);
        public void DeleteCategory(int id) => _db.Delete<Category>(id);

        public List<Category> GetAllCategories() => _db.Table<Category>().OrderBy(c => c.Name).ToList();
        public List<Category> GetIncomeCategories() => _db.Table<Category>().Where(c => c.IsIncome).OrderBy(c => c.Name).ToList();
        public List<Category> GetExpenseCategories() => _db.Table<Category>().Where(c => !c.IsIncome).OrderBy(c => c.Name).ToList();

        public int GetTransactionCountByCategoryId(int categoryId) =>
            _db.Table<Transaction>().Count(t => t.CategoryId == categoryId);

        // === НОВОЕ: автоподстановка ===
        public int? GetCategoryIdByMerchantPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return null;
            return _db.Table<Transaction>()
                .Where(t => t.MerchantPattern == pattern && t.CategoryId > 0)
                .OrderByDescending(t => t.Date)
                .Select(t => t.CategoryId)
                .FirstOrDefault();
        }

        // === Инициализация категорий ===
        public void InitializeDefaultCategories()
        {
            if (_db.Table<Category>().Count() > 0) return;

            var defaultCategories = new[]
            {
                new Category { Name = "Продукты", IsIncome = false, Color = "#4CAF50" },
                new Category { Name = "Транспорт", IsIncome = false, Color = "#2196F3" },
                new Category { Name = "Жилье", IsIncome = false, Color = "#9C27B0" },
                new Category { Name = "Коммуналка", IsIncome = false, Color = "#FF9800" },
                new Category { Name = "Развлечения", IsIncome = false, Color = "#E91E63" },
                new Category { Name = "Здоровье", IsIncome = false, Color = "#00BCD4" },
                new Category { Name = "Одежда", IsIncome = false, Color = "#8BC34A" },
                new Category { Name = "Техника", IsIncome = false, Color = "#607D8B" },
                new Category { Name = "Кафе", IsIncome = false, Color = "#795548" },
                new Category { Name = "Другое", IsIncome = false, Color = "#9E9E9E" },

                new Category { Name = "Зарплата", IsIncome = true, Color = "#2E7D32" },
                new Category { Name = "Подработка", IsIncome = true, Color = "#388E3C" },
                new Category { Name = "Инвестиции", IsIncome = true, Color = "#43A047" },
                new Category { Name = "Подарок", IsIncome = true, Color = "#4CAF50" },
                new Category { Name = "Возврат долга", IsIncome = true, Color = "#66BB6A" }
            };

            foreach (var cat in defaultCategories)
                _db.Insert(cat);
        }
        public void SaveCategoryRule(string pattern, int categoryId)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return;

            // Ищем существующее правило (маркер: Amount == 0 и Description начинается с [RULE])
            var existing = _db.Table<Transaction>()
                .Where(t => t.MerchantPattern == pattern && t.Amount == 0m && t.Description.StartsWith("[RULE]"))
                .FirstOrDefault();

            if (existing != null)
            {
                existing.CategoryId = categoryId;
                _db.Update(existing);
            }
            else
            {
                _db.Insert(new Transaction
                {
                    MerchantPattern = pattern,
                    CategoryId = categoryId,
                    Date = DateTime.Now,
                    Amount = 0,
                    Description = $"[RULE] {pattern}",
                    IsIncome = false
                });
            }
        }
        public List<Transaction> GetTransactionsByMerchantPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return new List<Transaction>();
            return _db.Table<Transaction>()
                .Where(t => t.MerchantPattern == pattern)
                .ToList();
        }

        // Обнаруживает и помечает внутренние переводы
        public void DetectAndMarkInternalTransfers()
        {
            var allTransactions = _db.Table<Transaction>().ToList();
            var transferKeywords = new[] { "перевод", "перечисл", "transfer", "internal", "между счет", "на карту" };

            // Находим кандидаты на перевод (доходы и расходы с ключевыми словами)
            var candidates = allTransactions
                .Where(t => transferKeywords.Any(kw => t.Description.ToLower().Contains(kw)))
                .ToList();

            var updated = new HashSet<int>();

            for (int i = 0; i < candidates.Count; i++)
            {
                var t1 = candidates[i];
                if (t1.IsInternalTransfer || updated.Contains(t1.Id)) continue;

                for (int j = i + 1; j < candidates.Count; j++)
                {
                    var t2 = candidates[j];
                    if (t2.IsInternalTransfer || updated.Contains(t2.Id)) continue;

                    // Условия пары:
                    if (t1.IsIncome != t2.IsIncome &&                    // противоположные типы
                        Math.Abs(t1.Amount - t2.Amount) < 0.01m &&       // одинаковая сумма
                        Math.Abs((t1.Date - t2.Date).TotalMinutes) <= 10) // ≤10 мин
                    {
                        // Помечаем обе операции
                        t1.IsInternalTransfer = true;
                        t2.IsInternalTransfer = true;
                        _db.Update(t1);
                        _db.Update(t2);
                        updated.Add(t1.Id);
                        updated.Add(t2.Id);
                        break;
                    }
                }
            }
        }

    }
}