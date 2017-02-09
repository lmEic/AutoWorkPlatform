﻿using System;
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
using WPF.DazzleUI2.Controls;

namespace MesServices.Desktop
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : DazzleWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void DazzleButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void DazzleButton_Click_1(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void DazzleButton_Click_2(object sender, RoutedEventArgs e)
        {
            this.WindowState = System.Windows.WindowState.Minimized;
        }

      
    }
}
