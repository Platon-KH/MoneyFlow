using MoneyFlowWPF.Models;
using MoneyFlowWPF.Services;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace MoneyFlowWPF
{
    public partial class AddTransactionWindow : Window
    {
        readonly private DatabaseService _dbService;
        private List<Category> _categories = [];

        public AddTransactionWindow()
        {
            InitializeComponent();
            _dbService = new DatabaseService();
            LoadCategories();
            DatePicker.SelectedDate = DateTime.Now;
        }

        private void LoadCategories()
        {
            _categories = _dbService.GetAllCategories();
            CategoryComboBox.ItemsSource = _categories;

            var defaultCat = _categories.FirstOrDefault(c => c.Name == "Другое");
            if (defaultCat != null)
                CategoryComboBox.SelectedItem = defaultCat;
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

            var category = (Category)CategoryComboBox.SelectedItem;
            DateTime date = DatePicker.SelectedDate ?? DateTime.Now;
            string description = DescriptionTextBox.Text.Trim();
            bool isIncome = IncomeRadio.IsChecked == true;

            var transaction = new Transaction
            {
                Date = date,
                Amount = amount,
                Description = string.IsNullOrEmpty(description) ? "Без описания" : description,
                Category = category.Name,
                CategoryId = category.Id,
                IsIncome = isIncome,
                MerchantPattern = BankStatementParser.ExtractMerchantPattern(description)
            };

            _dbService.AddTransaction(transaction);
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