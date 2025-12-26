using MoneyFlowWPF.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MoneyFlowWPF
{
    public partial class SingleCategorySelectionWindow : Window
    {
        public Category? SelectedCategory { get; private set; }

        public SingleCategorySelectionWindow(string prompt, List<Category> allCategories, bool isIncome = false)
        {
            InitializeComponent();
            PromptText.Text = prompt;
            
            var filtered = allCategories
                .Where(c => c.IsIncome == isIncome)
                .OrderBy(c => c.Name)
                .ToList();

            CategoryComboBox.ItemsSource = filtered;
            CategoryComboBox.DisplayMemberPath = "Name";
            CategoryComboBox.SelectedValuePath = "Id";

            if (filtered.Count > 0)
                CategoryComboBox.SelectedIndex = 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryComboBox.SelectedItem is Category cat)
            {
                SelectedCategory = cat;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите категорию.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}