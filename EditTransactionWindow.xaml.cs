using MoneyFlowWPF.Models;
using MoneyFlowWPF.Services;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace MoneyFlowWPF
{
    public partial class EditTransactionWindow : Window
    {
        readonly private DatabaseService _dbService;
        private List<Category> _categories = [];
        public Transaction EditedTransaction { get; private set; }

        public EditTransactionWindow(Transaction transaction)
        {
            InitializeComponent();
            _dbService = new DatabaseService();
            EditedTransaction = transaction;
            LoadCategories();
            FillForm();
        }

        private void LoadCategories()
        {
            _categories = _dbService.GetAllCategories();
            CategoryComboBox.ItemsSource = _categories;
        }

        private void FillForm()
        {
            AmountTextBox.Text = EditedTransaction.Amount.ToString("0.##");
            DatePicker.SelectedDate = EditedTransaction.Date;
            DescriptionTextBox.Text = EditedTransaction.Description;

            if (EditedTransaction.IsIncome)
                IncomeRadio.IsChecked = true;
            else
                ExpenseRadio.IsChecked = true;

            var selectedCat = _categories.FirstOrDefault(c => c.Id == EditedTransaction.CategoryId);
            if (selectedCat != null)
                CategoryComboBox.SelectedItem = selectedCat;
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9,]*$");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(AmountTextBox.Text.Replace(",", "."), out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Введите корректную сумму больше 0", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CategoryComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите категорию", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            EditedTransaction.Amount = amount;
            EditedTransaction.Date = DatePicker.SelectedDate ?? DateTime.Now;
            EditedTransaction.Description = DescriptionTextBox.Text.Trim();
            EditedTransaction.IsIncome = IncomeRadio.IsChecked == true;
            EditedTransaction.CategoryId = ((Category)CategoryComboBox.SelectedItem).Id;
            EditedTransaction.Category = ((Category)CategoryComboBox.SelectedItem).Name;
            EditedTransaction.MerchantPattern = BankStatementParser.ExtractMerchantPattern(EditedTransaction.Description);

            _dbService.UpdateTransaction(EditedTransaction);
            // Обновляем правило
            _dbService.SaveCategoryRule(EditedTransaction.MerchantPattern, EditedTransaction.CategoryId);

            // НАХОДИМ ВСЕ ОПЕРАЦИИ С ТАКИМ ЖЕ MERCHANT PATTERN
            var relatedTransactions = _dbService.GetTransactionsByMerchantPattern(EditedTransaction.MerchantPattern);

            foreach (var tx in relatedTransactions)
            {
                var result = MessageBox.Show(
                 $"Найдено ещё {relatedTransactions.Count - 1} операций с описанием \"{EditedTransaction.MerchantPattern}\".\n" +
                 "Обновить категорию для всех этих операций?",
                 "Подтверждение",
                 MessageBoxButton.YesNo,
                 MessageBoxImage.Question);
                if (tx.Id != EditedTransaction.Id) // пропускаем текущую
                {
                    tx.CategoryId = EditedTransaction.CategoryId;
                    tx.Category = EditedTransaction.Category;
                    _dbService.UpdateTransaction(tx);
                }
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}