/*
 * File: ConfirmDialog.xaml.cs
 * Project: Domain
 * Created Date: 18/04/2021
 * Author: Shun Suzuki
 * -----
 * Last Modified: 18/04/2021
 * Modified By: Shun Suzuki (suzuki@hapis.k.u-tokyo.ac.jp)
 * -----
 * Copyright (c) 2021 Hapis Lab. All rights reserved.
 * 
 */

using System.ComponentModel;
using System.Windows.Controls;
using Reactive.Bindings;

namespace ThermalProfiler.Domain
{
    public partial class ConfirmDialog : UserControl
    {
        public ConfirmDialog()
        {
            InitializeComponent();
        }
    }

    public class ConfirmDialogViewModel : INotifyPropertyChanged
    {
#pragma warning disable 414
        public event PropertyChangedEventHandler PropertyChanged = null!;
#pragma warning restore 414

        public ReactiveProperty<string> Message { get; set; }

        public ConfirmDialogViewModel()
        {
            Message = new ReactiveProperty<string>();
        }
    }
}
