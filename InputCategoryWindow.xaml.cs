using MoneyFlowWPF.Models;
using System.Windows;

namespace MoneyFlowWPF
{
    public partial class InputCategoryWindow : Window
    {
        public Category Category { get; private set; } = new Category();

        public InputCategoryWindow()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Category.Name = NameTextBox.Text.Trim();
            Category.IsIncome = IncomeRadio.IsChecked == true;
            Category.Color = Category.IsIncome ? "#4CAF50" : "#F44336"; // зелёный / красный по умолчанию

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}