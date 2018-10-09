using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;


namespace Cashbox
{
    /// <summary>
    /// Interaction logic for TransactionFrm.xaml
    /// </summary>
    public partial class TransactionFrm : Window
    {
        DataSet m_DS;
        DataRelation TranItemsToItems;

        public TransactionFrm()
        {
            InitializeComponent();
            
        }

        private void DefaultView_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
        {
            double _sum = 0;
            foreach(DataRowView _drv in m_DS.Tables["tranitems"].DefaultView)
                _sum += (double)_drv["Sm"];
            SumLBL.Content = _sum.ToString("0.00");
            DoneBtn.IsEnabled = (_sum > 0);
            CachTB_TextChanged(null, null);
        }

        public static bool Show(DataSet DS, MySqlDataAdapter ItemsAdapter, MySqlDataAdapter TransactionAdapter, MySqlDataAdapter TranItemAdapter, DataRow TransactionRow = null)
        {
            TransactionFrm _form = new TransactionFrm();
            _form.m_DS = DS;
            _form.NewItemTB.ItemsSource = DS.Tables["items"].DefaultView;
            DataRow _transactionRow;
            if (TransactionRow == null)
            {
                _transactionRow = DS.Tables["transactions"].NewRow();
                _transactionRow["Number"] = (DS.Tables["transactions"].Rows.Count + 1).ToString("000000");
                _transactionRow["Status"] = 0;
            }
            else
            {
                _transactionRow = TransactionRow;
            }

            _form.AddNewItemGR.IsEnabled = (TransactionRow == null);
            _form.DelBtn.Visibility = TransactionRow == null ? Visibility.Visible : Visibility.Hidden;
            _form.SummaryGR.IsEnabled = (TransactionRow == null);

            _form.DataContext = _transactionRow;
            TranItemAdapter.SelectCommand.Parameters["@tranid"].Value = _transactionRow["id"];
            TranItemAdapter.Fill(DS);

            if (!DS.Relations.Contains("TranItemsToItems")) {
                _form.TranItemsToItems = new DataRelation("TranItemsToItems", _form.m_DS.Tables["items"].Columns["id"], _form.m_DS.Tables["tranitems"].Columns["itemid"]);
                DS.Relations.Add(_form.TranItemsToItems);
            }

            _form.TranItemsDG.ItemsSource = DS.Tables["tranitems"].DefaultView;
            _form.m_DS.Tables["tranitems"].DefaultView.ListChanged += _form.DefaultView_ListChanged;
            _form.DefaultView_ListChanged(null, null);
            _form.Title = "Операция № " + _transactionRow["Number"] + ((int)(_transactionRow)["Status"] == 0 ? " (открыта)" : " (закрыта)");
            bool _retval = (bool)_form.ShowDialog();
            if (_retval) 
            {
                TransactionAdapter.SelectCommand.Connection.Open();
                try
                {
                    MySqlTransaction _tran = TransactionAdapter.SelectCommand.Connection.BeginTransaction();
                    try
                    {
                        TransactionAdapter.InsertCommand.Transaction = _tran;
                        TranItemAdapter.InsertCommand.Transaction = _tran;
                        ItemsAdapter.UpdateCommand.Transaction = _tran;

                        DS.Tables["transactions"].Rows.Add(_transactionRow);
                        TransactionAdapter.Update(DS.Tables["transactions"]);
                        long _newTranId = TransactionAdapter.InsertCommand.LastInsertedId;
                        foreach (DataRow _tranItemRow in DS.Tables["tranitems"].Rows)
                            _tranItemRow["tranid"] = _newTranId;
                        TranItemAdapter.Update(DS.Tables["tranitems"]);
                        ItemsAdapter.Update((DS.Tables["items"]));
                        _tran.Commit();
                    }
                    catch (Exception _e)
                    {
                        _tran.Rollback();
                        MessageBox.Show("Что-то пошло не так: " + _e.Message, "Ошибка");
                    }
                }
                finally
                {
                    TransactionAdapter.SelectCommand.Connection.Close();
                }
                
            }
            DS.Tables["tranitems"].Clear();
            return _retval;
        }

        private void CountTB_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            
            Regex _regex = new Regex("[^0-9]+");
            e.Handled = _regex.IsMatch(e.Text);
        }

        private void AddItemBtn_Click(object sender, RoutedEventArgs e)
        {
            DataRow _row = m_DS.Tables["tranitems"].NewRow();
            _row["DName"] = (NewItemTB.SelectedItem as DataRowView)["DName"];
            _row["Amount"] = int.Parse(CountTB.Text);
            int _qantity = (int)(NewItemTB.SelectedItem as DataRowView)["Quantity"];
            if (_qantity < (int)_row["Amount"])
            {
                MessageBox.Show("Kоличество товара не должно превышать " + _qantity, "Недостаточное количество товара", MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }
            (NewItemTB.SelectedItem as DataRowView)["Quantity"] = _qantity - (int)_row["Amount"];

            _row["Cost"] = (float)(NewItemTB.SelectedItem as DataRowView)["Cost"];
            _row["Sm"] = (int)_row["Amount"] * (float)_row["Cost"];
            _row["itemid"] = (NewItemTB.SelectedItem as DataRowView)["id"];
            _row.SetParentRow((NewItemTB.SelectedItem as DataRowView).Row, TranItemsToItems);
            m_DS.Tables["tranitems"].Rows.Add(_row);
        }

        private void CachTB_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            //Regex _regex = new Regex(@"^[0-9]*\.?[0-9]*");
            Regex _regex = new Regex("[^0-9]+");
            e.Handled = _regex.IsMatch(e.Text);
        }

        private void CachTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                float _cash = float.Parse(CachTB.Text) - float.Parse(SumLBL.Content as string);
                changeLBL.Content = _cash >= 0 ? _cash.ToString("0.00") : "Недостаточно средсв";
            }
            catch
            {
                changeLBL.Content = "";
            }
        }

        private void DelBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TranItemsDG.SelectedItem is DataRowView)
            {
                DataRow _itemRow = (TranItemsDG.SelectedItem as DataRowView).Row.GetParentRow(TranItemsToItems);
                _itemRow["Quantity"] = (int)_itemRow["Quantity"] + (int)(TranItemsDG.SelectedItem as DataRowView).Row["Amount"];
                (TranItemsDG.SelectedItem as DataRowView).Row.Delete();

            }
        }

        private void DoneBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (float.Parse(CachTB.Text) - float.Parse(SumLBL.Content as string) >= 0)
                {
                    (DataContext as DataRow)["Status"] = 1;
                    DialogResult = true;
                    Close();
                }
                else
                    MessageBox.Show("Недостаточно средсв для закрытия операции ");
            }
            catch { }

        }
    }

    [ValueConversion(typeof(DataRowView), typeof(string))]
    public class GetCellConverter : IValueConverter
    {
        public object Convert(object value, Type t, object parameter, CultureInfo culture)
        {
            if (value is DataRowView)
                return (value as DataRowView)[parameter as string];
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type t, object parameter, CultureInfo culture)
        {
            return value.Equals(false) ? DependencyProperty.UnsetValue : parameter;
        }
    }

    [ValueConversion(typeof(DataRowView), typeof(bool))]
    public class RowNotNullConverter : IValueConverter
    {
        public object Convert(object value, Type t, object parameter, CultureInfo culture)
        {
            return (value is DataRowView);
        }

        public object ConvertBack(object value, Type t, object parameter, CultureInfo culture)
        {
            return value.Equals(false) ? DependencyProperty.UnsetValue : parameter;
        }
    }
}
