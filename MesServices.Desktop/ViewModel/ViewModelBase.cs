using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace MesServices.Desktop.ViewModel
{
    /// <summary>
    /// 视图模型接口
    /// </summary>
    public interface IViewModel : INotifyPropertyChanged
    {
        string Title { get; set; }
        void Initialize(object parameters);
    }
    /// <summary>
    /// 视图模型基类
    /// </summary>
    public abstract class ViewModelBase : IViewModel
    {
        #region implement infterface
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual void Initialize(object parameters)
        { }
        #endregion

        #region Property
        string _Title = "";
        /// <summary>
        /// 标题
        /// </summary>
        public string Title
        {
            get
            {
                return _Title;
            }
            set
            {
                if (_Title != value)
                {
                    _Title = value;
                    OnPropertyChanged("Title");
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// 中继命令
    /// </summary>
    public class RelayCommand : ICommand
    {
        #region Fields
        readonly Action<object> _execute;
        readonly Predicate<object> _canExecute;

        #endregion // Fields

        #region Constructors

        public RelayCommand(Action<object> execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");

            _execute = execute;
            _canExecute = canExecute;
        }
        #endregion

        #region ICommand Member
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }
        #endregion
    }
}
