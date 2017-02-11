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
using MesServices.Desktop.ViewModel;

namespace MesServices.Desktop
{
    /// <summary>
    /// OnLine.xaml 的交互逻辑
    /// </summary>
    public partial class OnLine : UserControl
    {
        private AttendanceProcesserViewModel vm = null;
        public OnLine()
        {
            InitializeComponent();
            vm = new ViewModel.AttendanceProcesserViewModel();
            this.DataContext = vm;
        }

     
    }
}
