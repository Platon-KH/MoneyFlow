using System.Windows;

namespace MoneyFlowWPF
{
    public partial class PdfDebugWindow : Window
    {
        public PdfDebugWindow(string pdfText)
        {
            InitializeComponent();
            PdfTextTextBox.Text = pdfText;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(PdfTextTextBox.Text);
            MessageBox.Show("Текст скопирован в буфер обмена", "Успех");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}