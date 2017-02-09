using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.Practices.Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace MesServices.Desktop
{
    class ErpSynchronousViewModel:ViewModelBase
    {

        public ErpSynchronousViewModel()
        {
            OrderSyn.Interval = 15;
        }


        private Business.OrderSynchronous orderSyn =new Business.OrderSynchronous();

        public Business.OrderSynchronous OrderSyn
        {
            get { return orderSyn; }
            set
            {
                orderSyn = value;
                this.RaisePropertyChanged("OrderSyn");
            }
        }

 


    }
}
