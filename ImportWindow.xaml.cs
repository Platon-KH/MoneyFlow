using MoneyFlowWPF.Models;
using MoneyFlowWPF.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using UglyToad.PdfPig;

namespace MoneyFlowWPF
{
    public partial class ImportWindow : Window
    {
        private BankStatementParser _parser;
        private DatabaseService _dbService;
        private List<Transaction> _parsedTransactions;

        public ImportWindow()
        {
            InitializeComponent();
            _parser = new BankStatementParser();
            _dbService = new DatabaseService();
            _parsedTransactions = new List<Transaction>();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Банковские выписки|*.pdf;*.csv|PDF файлы|*.pdf|CSV файлы|*.csv|Все файлы|*.*",
                Title = "Выберите файл выписки"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                FilePathTextBox.Text = openFileDialog.FileName;
                LoadFilePreview(openFileDialog.FileName);
            }
        }

        private void LoadFilePreview(string filePath)
        {
            try
            {
                _parsedTransactions.Clear();
                string extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".pdf")
                    _parsedTransactions = _parser.ParsePdf(filePath);
                else if (extension == ".csv")
                    _parsedTransactions = _parser.ParseCsv(filePath);
                else
                {
                    MessageBox.Show("Неподдерживаемый формат файла", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // УБИРАЕМ DetermineCategory — теперь все Category = "Другое" по умолчанию
                // Но CategoryId остаётся 0 — это ключевой маркер "не назначена"

                PreviewGrid.ItemsSource = _parsedTransactions;
                ImportButton.IsEnabled = _parsedTransactions.Count > 0;
                MessageBox.Show($"Найдено {_parsedTransactions.Count} операций", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при чтении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parsedTransactions.Count == 0)
            {
                MessageBox.Show("Нет операций для импорта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var allCategories = _dbService.GetAllCategories();
                var unclassified = new List<Transaction>();
                int importedCount = 0;

                // Шаг 1: Импортируем всё, что уже имеет категорию или известно по MerchantPattern
                foreach (var tx in _parsedTransactions.ToList())
                {
                    // Проверка дубликата по дате, сумме и описанию
                    bool isDuplicate = _dbService.GetTransactionsByDate(tx.Date.AddMinutes(-2), tx.Date.AddMinutes(2))
                        .Any(existing =>
                            Math.Abs(existing.Amount - tx.Amount) < 0.01m &&
                            existing.Description == tx.Description);

                    if (isDuplicate) continue;

                    // Пробуем найти правило
                    var rule = _dbService.GetCategoryIdByMerchantPattern(tx.MerchantPattern);
                    if (rule.HasValue && rule.Value > 0)
                    {
                        tx.CategoryId = rule.Value;
                        tx.Category = allCategories.FirstOrDefault(c => c.Id == rule.Value)?.Name ?? "Другое";
                        _dbService.AddTransaction(tx);
                        importedCount++;
                    }
                    else
                    {
                        unclassified.Add(tx);
                    }
                }

                // Сообщение о первичном импорте
                int total = _parsedTransactions.Count;
                MessageBox.Show($"Импортировано {importedCount} операций из {total}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // Шаг 2: Обработка нераспознанных — по одной
                if (unclassified.Count > 0)
                {
                    MessageBox.Show($"Внимание! У {unclassified.Count} операций не присвоена категория.", "Информация", MessageBoxButton.OK, MessageBoxImage.Warning);

                    foreach (var tx in unclassified.ToList())
                    {
                        // Проверка: может, правило уже добавлено предыдущей операцией?
                        var rule = _dbService.GetCategoryIdByMerchantPattern(tx.MerchantPattern);
                        if (rule.HasValue && rule.Value > 0)
                        {
                            tx.CategoryId = rule.Value;
                            tx.Category = allCategories.FirstOrDefault(c => c.Id == rule.Value)?.Name ?? "Другое";
                            _dbService.AddTransaction(tx);
                            importedCount++;
                            continue;
                        }

                        // Показываем диалог
                        string prompt = $"{tx.Date:dd.MM.yyyy} — {tx.Description} — {tx.Amount} ₽";
                        var dialog = new SingleCategorySelectionWindow(prompt, allCategories, tx.IsIncome)
                        {
                            Owner = this // 🔑 КЛЮЧЕВОЙ МОМЕНТ
                        };

                        if (dialog.ShowDialog() == true)
                        {
                            tx.CategoryId = dialog.SelectedCategory.Id;
                            tx.Category = dialog.SelectedCategory.Name;
                            _dbService.AddTransaction(tx);
                            importedCount++;

                        }
                        else
                        {
                            // Пропущено — назначаем "Другое"
                            var other = allCategories.FirstOrDefault(c => !c.IsIncome && c.Name == "Другое")
                                      ?? allCategories.FirstOrDefault(c => !c.IsIncome)
                                      ?? allCategories.FirstOrDefault();
                            if (other != null)
                            {
                                tx.CategoryId = other.Id;
                                tx.Category = other.Name;
                                _dbService.AddTransaction(tx);
                                importedCount++;
                            }
                        }
                    }
                }

                MessageBox.Show($"Импорт завершён. Всего добавлено: {importedCount}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void DebugButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FilePathTextBox.Text))
            {
                MessageBox.Show("Сначала выберите файл", "Информация");
                return;
            }
            try
            {
                string extension = Path.GetExtension(FilePathTextBox.Text).ToLower();
                if (extension == ".pdf")
                {
                    using (var pdf = PdfDocument.Open(FilePathTextBox.Text))
                    {
                        var sb = new StringBuilder();
                        foreach (var page in pdf.GetPages())
                        {
                            sb.AppendLine($"=== Страница {page.Number} ===");
                            sb.AppendLine(page.Text);
                            sb.AppendLine(new string('=', 50));
                            sb.AppendLine();
                        }
                        var debugWindow = new PdfDebugWindow(sb.ToString());
                        debugWindow.Owner = this;
                        debugWindow.ShowDialog();
                    }
                }
                else
                {
                    MessageBox.Show("Отладка доступна только для PDF файлов", "Информация");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
        }
    }
}