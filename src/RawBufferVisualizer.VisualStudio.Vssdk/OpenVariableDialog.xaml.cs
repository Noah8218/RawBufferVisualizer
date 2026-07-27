using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    public partial class OpenVariableDialog : Window
    {
        private readonly Func<string, string> _evaluate;

        public OpenVariableDialog(IReadOnlyList<string> recentExpressions, Func<string, string> evaluate)
        {
            InitializeComponent();
            _evaluate = evaluate ?? throw new ArgumentNullException("evaluate");
            if (recentExpressions != null && recentExpressions.Count > 0)
            {
                RecentList.ItemsSource = recentExpressions;
                RecentHeading.Visibility = Visibility.Visible;
                RecentList.Visibility = Visibility.Visible;
            }
        }

        public string ExpressionText
        {
            get { return ExpressionBox.Text ?? string.Empty; }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            TryOpen();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ExpressionBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryOpen();
                e.Handled = true;
            }
        }

        private void RecentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = RecentList.SelectedItem as string;
            if (!string.IsNullOrEmpty(selected))
            {
                var box = ExpressionBox;
                if (box != null)
                {
                    box.Text = selected;
                    box.CaretIndex = selected!.Length;
                    box.Focus();
                }
            }
        }

        private void TryOpen()
        {
            var result = _evaluate(ExpressionText);
            if (string.IsNullOrEmpty(result))
            {
                DialogResult = true;
                return;
            }

            StatusText.Text = result;
        }
    }
}
