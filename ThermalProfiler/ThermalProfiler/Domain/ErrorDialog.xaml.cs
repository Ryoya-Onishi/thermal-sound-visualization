using System.ComponentModel;
using System.Windows.Controls;
using Reactive.Bindings;

namespace ThermalProfiler.Domain
{
    public sealed partial class ErrorDialog : UserControl
    {
        public ErrorDialog()
        {
            this.InitializeComponent();
        }
    }

    public class ErrorDialogViewModel : INotifyPropertyChanged
    {
#pragma warning disable 414
        public event PropertyChangedEventHandler PropertyChanged = null!;
#pragma warning restore 414

        public ReactiveProperty<string> Message { get; set; }

        public ErrorDialogViewModel()
        {
            Message = new ReactiveProperty<string>();
        }
    }
}
