using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MesServices.Desktop
{
    /// <summary>
    /// Order.xaml 的交互逻辑
    /// </summary>
    public partial class ErpSynchronous : UserControl
    {
        ErpSynchronousViewModel vm = new ErpSynchronousViewModel();
        public ErpSynchronous()
        {
            InitializeComponent();
          
            this.DataContext = vm;
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            this.kkk.ItemsSource = vm.OrderSyn.OrderList;
        }
    }
}
