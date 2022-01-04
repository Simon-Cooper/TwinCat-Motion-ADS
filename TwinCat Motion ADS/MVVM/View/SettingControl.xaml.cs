﻿using System;
using System.Windows.Controls;


namespace TwinCat_Motion_ADS.MVVM.View
{
    /// <summary>
    /// Interaction logic for SettingControl.xaml
    /// </summary>
    public partial class SettingControl : UserControl
    {
        public SettingControl()
        {
            InitializeComponent();
            this.DataContext = this;
        }
        public string SetValue { get; set; }
        public string SetName { get; set; }
    }
}
