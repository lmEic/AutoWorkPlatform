using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
namespace MesServices.Desktop.ViewModel
{
    public  class AttendanceUpDataServerViewModel:ViewModelBase
    {

        private string _msg;
        public string  Msg
        {
            get { return _msg; }
            set
            {
                _msg = value;
                this.RaisePropertyChanged("_msg");
            }
        }
       
    }
}
