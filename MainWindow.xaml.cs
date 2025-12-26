using MoneyFlowWPF.Models;
using MoneyFlowWPF.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MoneyFlowWPF
{
    public partial class MainWindow : Window
    {
        private DatabaseService _dbService;

        public MainWindow()
        {
            InitializeComponent();
            _dbService = new DatabaseService();

            // Привязываем обработчики событий
            AddButton.Click += AddButton_Click;
            RefreshButton.Click += RefreshButton_Click;

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var transactions = _dbService.GetAllTransactions()
                    .Where(t => !t.IsInternalTransfer)
                    .ToList();
                var categories = _dbService.GetAllCategories().ToDictionary(c => c.Id, c => c.Name);

                var enriched = transactions.Select(t => new
                {
                    t.Id,
                    t.Date,
                    t.Amount,
                    t.Description,
                    Category = categories.TryGetValue(t.CategoryId, out var name) ? name : t.Category,
                    t.IsIncome,
                    t.CategoryId
                }).ToList();

                TransactionsGrid.ItemsSource = enriched;
                UpdateStatistics();
                StatusText.Text = $"Загружено {transactions.Count} операций";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
        }

        private void UpdateStatistics()
        {
            var transactions = _dbService.GetAllTransactions()
                .Where(t => !t.IsInternalTransfer)
                .ToList();

            decimal income = 0;
            decimal expense = 0;

            foreach (var t in transactions)
            {
                if (t.IsIncome)
                    income += t.Amount;
                else
                    expense += t.Amount;
            }

            IncomeText.Text = $"{income:C}";
            ExpenseText.Text = $"{expense:C}";
            BalanceText.Text = $"{income - expense:C}";
            CountText.Text = $"{transactions.Count}";
        }
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно добавления операции
            var addWindow = new AddTransactionWindow();

            if (addWindow.ShowDialog() == true)
            {
                // Если операция успешно добавлена
                LoadData();
                MessageBox.Show("Операция успешно добавлена!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int id)
            {
                var transaction = _dbService.GetAllTransactions().FirstOrDefault(t => t.Id == id);
                if (transaction == null)
                {
                    MessageBox.Show("Операция не найдена", "Ошибка");
                    return;
                }

                var editWindow = new EditTransactionWindow(transaction)
                {
                    Owner = this
                };

                if (editWindow.ShowDialog() == true)
                {
                    LoadData();
                    MessageBox.Show("Операция успешно обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int id)
            {
                var result = MessageBox.Show($"Удалить операцию {id}?",
                    "Подтверждение", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    _dbService.DeleteTransaction(id);
                    LoadData();
                    MessageBox.Show("Удалено!");
                }
            }
        }
        private void CategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var categoriesWindow = new CategoriesWindow();
                categoriesWindow.Owner = this; // Делаем главное окно владельцем
                categoriesWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии категорий: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var importWindow = new ImportWindow();
            importWindow.Owner = this;

            if (importWindow.ShowDialog() == true)
            {
                LoadData();
                MessageBox.Show("Выписка успешно импортирована!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void ChartsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var chartsWindow = new ChartsWindow();
                chartsWindow.Owner = this;
                chartsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть графики: {ex.Message}", "Ошибка");
            }
        }
        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var transactions = _dbService.GetAllTransactions();
                if (transactions.Count == 0)
                {
                    MessageBox.Show("Нет данных для экспорта", "Информация");
                    return;
                }

                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Операции");

                // Заголовки
                worksheet.Cell(1, 1).Value = "Дата";
                worksheet.Cell(1, 2).Value = "Описание";
                worksheet.Cell(1, 3).Value = "Категория";
                worksheet.Cell(1, 4).Value = "Сумма";
                worksheet.Cell(1, 5).Value = "Тип";

                // Данные
                for (int i = 0; i < transactions.Count; i++)
                {
                    var t = transactions[i];
                    worksheet.Cell(i + 2, 1).Value = t.Date;
                    worksheet.Cell(i + 2, 2).Value = t.Description;
                    worksheet.Cell(i + 2, 3).Value = t.Category;
                    worksheet.Cell(i + 2, 4).Value = t.Amount;
                    worksheet.Cell(i + 2, 5).Value = t.IsIncome ? "Доход" : "Расход";
                }

                // Форматирование
                worksheet.Columns().AdjustToContents();

                // Сохранение
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Файлы Excel|*.xlsx",
                    FileName = $"MoneyFlow_Экспорт_{DateTime.Now:yyyy-MM-dd_HHmm}.xlsx"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    workbook.SaveAs(saveDialog.FileName);
                    MessageBox.Show("Данные успешно экспортированы в Excel!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow { Owner = this };
            helpWindow.ShowDialog();
        }
    }

}