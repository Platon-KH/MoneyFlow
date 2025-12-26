using MoneyFlowWPF.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;

namespace MoneyFlowWPF.Services
{
    public class BankStatementParser
    {
        public List<Transaction> ParsePdf(string filePath, bool isIncomeDefault = false)
        {
            var transactions = new List<Transaction>();
            try
            {
                using (var pdf = PdfDocument.Open(filePath))
                {
                    string fullText = GetFullPdfText(pdf);
                    int tableStart = fullText.IndexOf("Дата и времяоперацииДатасписания");
                    if (tableStart == -1)
                        tableStart = fullText.IndexOf("30.11.202511:37");
                    if (tableStart == -1)
                        throw new Exception("Не удалось найти таблицу операций в PDF");

                    int tableEnd = fullText.IndexOf("Пополнения:", tableStart);
                    if (tableEnd == -1)
                        tableEnd = fullText.IndexOf("АО «ТБанк»", tableStart);
                    if (tableEnd == -1)
                        tableEnd = fullText.Length;

                    string tableText = fullText.Substring(tableStart, tableEnd - tableStart);
                    transactions = ParseTableText(tableText);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка чтения PDF: {ex.Message}");
            }
            return transactions;
        }

        private string GetFullPdfText(PdfDocument pdf)
        {
            var sb = new StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                sb.Append(page.Text);
            }
            return sb.ToString();
        }

        private List<Transaction> ParseTableText(string tableText)
        {
            var transactions = new List<Transaction>();
            var regex = new System.Text.RegularExpressions.Regex(
                @"(\d{2}\.\d{2}\.\d{4})(\d{2}:\d{2})(\d{2}\.\d{2}\.\d{4})(\d{2}:\d{2})([+-]?\s*\d[\d\s]*\.?\d*)\s*₽([+-]?\s*\d[\d\s]*\.?\d*)\s*₽(.+?)(?=\d{2}\.\d{2}\.\d{4}\d{2}:\d{2}|\Z)");
            var matches = regex.Matches(tableText);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count >= 8)
                {
                    var transaction = ParseMatch(match);
                    if (transaction != null)
                    {
                        transactions.Add(transaction);
                    }
                }
            }
            return transactions;
        }

        private Transaction? ParseMatch(System.Text.RegularExpressions.Match match)
        {
            try
            {
                string dateStr = match.Groups[1].Value;
                string amountStr = match.Groups[5].Value;
                string description = match.Groups[7].Value;
                description = System.Text.RegularExpressions.Regex.Replace(description, @"\d{4}$", "").Trim();

                if (!DateTime.TryParseExact(dateStr, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    return null;

                amountStr = amountStr.Replace(" ", "");
                if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount))
                    return null;

                bool isIncome = amount > 0;
                amount = Math.Abs(amount);
                description = CleanDescription(description);
                string merchantPattern = ExtractMerchantPattern(description);

                return new Transaction
                {
                    Date = date,
                    Amount = amount,
                    Description = description,
                    Category = "Другое", // для обратной совместимости
                    CategoryId = 0,      // будет определено позже
                    IsIncome = isIncome,
                    MerchantPattern = merchantPattern
                };
            }
            catch
            {
                return null;
            }
        }

        private string CleanDescription(string description)
        {
            description = description
                .Replace("KhabarovskRUS", "Khabarovsk RUS")
                .Replace("MoskvaRUS", "Moskva RUS")
                .Replace("HabarovskRUS", "Habarovsk RUS")
                .Replace("KurganRUS", "Kurgan RUS");
            description = System.Text.RegularExpressions.Regex.Replace(description, @"([a-zA-Z]{2,})(\d{2,})", "$1 $2");
            description = System.Text.RegularExpressions.Regex.Replace(description, @"(\w)(RUS|Rus)", "$1 $2");
            return description.Trim();
        }

        public List<Transaction> ParseCsv(string filePath, bool isIncomeDefault = false)
        {
            var transactions = new List<Transaction>();
            try
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                int startIndex = lines.Length > 0 && (lines[0].ToLower().Contains("дата") || lines[0].ToLower().Contains("сумма")) ? 1 : 0;

                for (int i = startIndex; i < lines.Length; i++)
                {
                    var transaction = ParseCsvLine(lines[i], isIncomeDefault);
                    if (transaction != null)
                    {
                        transactions.Add(transaction);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка чтения CSV: {ex.Message}");
            }
            return transactions;
        }

        private Transaction? ParseCsvLine(string line, bool isIncomeDefault)
        {
            try
            {
                char separator = line.Contains(';') ? ';' : ',';
                var parts = line.Split(separator);
                if (parts.Length >= 3)
                {
                    if (DateTime.TryParse(parts[0].Trim(), out DateTime date) ||
                        DateTime.TryParseExact(parts[0].Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    {
                        string amountStr = parts[1].Trim()
                            .Replace(" ", "")
                            .Replace(",", ".")
                            .Replace("₽", "");
                        if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount))
                        {
                            bool isIncome = amount > 0;
                            amount = Math.Abs(amount);
                            string description = parts.Length > 2 ? parts[2].Trim() : "";
                            return new Transaction
                            {
                                Date = date,
                                Amount = amount,
                                Description = description,
                                Category = "Другое",
                                CategoryId = 0,
                                IsIncome = isIncome,
                                MerchantPattern = ExtractMerchantPattern(description)
                            };
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // УДАЛЕНО: DetermineCategory()

        public static string ExtractMerchantPattern(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return description;

            var patterns = new[] { "в ", "оплата ", "перевод ", "платеж " };
            string descLower = description.ToLower();
            foreach (var pattern in patterns)
            {
                int index = descLower.IndexOf(pattern);
                if (index >= 0)
                {
                    string merchant = description.Substring(index + pattern.Length);
                    var words = merchant.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0)
                        return words[0];
                }
            }
            return description.Length > 30 ? description.Substring(0, 30) : description;
        }
    }
}