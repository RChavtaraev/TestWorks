using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Cashbox
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        DataSet m_DS = new DataSet();
        MySqlDataAdapter m_tranItemsAdapter;
        MySqlDataAdapter m_transactionsAdapter;
        MySqlDataAdapter m_itemsAdapter;

        public MainWindow()
        {
            InitializeComponent();

            MySqlConnection _conn = new MySqlConnection(ConfigurationSettings.AppSettings["ConnStr"]);
            m_itemsAdapter = new MySqlDataAdapter("SELECT id, DName, Quantity, Cost FROM items ORDER BY DName", _conn);
            m_itemsAdapter.UpdateCommand = new MySqlCommand("UPDATE items SET Quantity=@Quantity where id=@id", _conn);
            m_itemsAdapter.UpdateCommand.Parameters.Add("@id", MySqlDbType.Int32).SourceColumn = "id";
            m_itemsAdapter.UpdateCommand.Parameters.Add("@Quantity", MySqlDbType.Int32).SourceColumn = "Quantity";
            m_itemsAdapter.TableMappings.Add("Table", "items");
            m_itemsAdapter.Fill(m_DS);

            m_tranItemsAdapter = new MySqlDataAdapter("SELECT items.DName, items.Cost, itemid, tranid, Amount, (items.Cost * Amount) as Sm FROM tranitems INNER JOIN items ON items.id=tranitems.itemid WHERE (tranid = @tranid)", _conn);
            m_tranItemsAdapter.SelectCommand.Parameters.Add("@tranid", MySqlDbType.Int32);

            m_tranItemsAdapter.InsertCommand = new MySqlCommand("INSERT INTO tranitems (itemid, tranid, Amount) VALUES (@itemid, @tranid, @Amount)", m_tranItemsAdapter.SelectCommand.Connection);
            m_tranItemsAdapter.InsertCommand.Parameters.Add("@itemid", MySqlDbType.Int32).SourceColumn = "itemid";
            m_tranItemsAdapter.InsertCommand.Parameters.Add("@tranid", MySqlDbType.Int32).SourceColumn = "tranid";
            m_tranItemsAdapter.InsertCommand.Parameters.Add("@Amount", MySqlDbType.Int32).SourceColumn = "Amount";
            m_tranItemsAdapter.TableMappings.Add("Table", "tranitems");

            m_transactionsAdapter = new MySqlDataAdapter("SELECT id, Status, Number FROM transactions ORDER BY Number", _conn);
            m_transactionsAdapter.InsertCommand = new MySqlCommand("INSERT INTO transactions (Status, Number) VALUES (@Status, @Number)", m_transactionsAdapter.SelectCommand.Connection);
            m_transactionsAdapter.InsertCommand.Parameters.Add("@Status", MySqlDbType.Int32).SourceColumn = "Status"; ;
            m_transactionsAdapter.InsertCommand.Parameters.Add("@Number", MySqlDbType.String).SourceColumn = "Number";

            m_transactionsAdapter.TableMappings.Add("Table", "transactions");
            m_transactionsAdapter.Fill(m_DS);

            

            Transactions.ItemsSource = m_DS.Tables["transactions"].DefaultView;
        }

        private void NewTransactionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TransactionFrm.Show(m_DS, m_itemsAdapter, m_transactionsAdapter, m_tranItemsAdapter)) {
                m_DS.Tables["transactions"].Clear();
                m_transactionsAdapter.Fill(m_DS);
            }
        }

        private void Transactions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Transactions.SelectedItem is DataRowView)
                TransactionFrm.Show(m_DS, m_itemsAdapter, m_transactionsAdapter, m_tranItemsAdapter, (Transactions.SelectedItem as DataRowView).Row);
        }
    }

    [ValueConversion(typeof(int), typeof(String))]
    public class StatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int _status = (int)value;
            if (_status == 0)
                return "Открыта";
            return "Закрыта";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            
            return DependencyProperty.UnsetValue;
        }
    }
}
