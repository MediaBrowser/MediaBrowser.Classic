using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using MediaBrowser.Library.Logging;
using MediaBrowser.Attributes;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using System.Windows.Media;
using MediaBrowser.Library.Plugins.Configuration;
using MediaBrowser.Library.Persistance;

namespace MediaBrowser.Library.Plugins
{

    public class PluginConfigurationOptions 
    {
    }
    
    // the new base configuration class 
    public class PluginConfiguration<T> : IPluginConfiguration where T : PluginConfigurationOptions, new() 
    {
        private string initialPath;
        private string pluginID;
        private string configFile;
        private T _instance;
        public T Instance
        {
            get
            {
                if (_instance == null)
                {
                    Load();
                }
                return _instance;
            }
        }
        
        public PluginConfiguration(Kernel kernel, Assembly assembly)
        {

            this.initialPath = Path.Combine(kernel.ConfigData.InitialFolder, @"..\Plugins\Configurations");
            this.pluginID = assembly.GetName().Name;
            this.configFile = Path.Combine(initialPath, string.Format("{0}.xml", pluginID));
            
        }

        //testing constructor
        public PluginConfiguration(string savePath, string fileName)
        {
            this.initialPath = savePath;
            this.pluginID = fileName;
            this.configFile = Path.Combine(initialPath, string.Format("{0}.xml", pluginID));
        }

        private void Reset() {
            try {
                File.Delete(configFile);
            } catch (Exception e) {
                Logger.ReportException("Failed during config file reset!", e);
            }
            Load();
        } 
        
        public void Save()
        {
            if (string.IsNullOrEmpty(initialPath))
            {
                Logger.ReportError(string.Format("{0} plugin configuration save failed, initial path is empty.", pluginID));
                return;
            }

            try
            {
                settings.Write();
            }
            catch (Exception e)
            {
                Logger.ReportException(string.Format("{0} plugin configuration save failed.", pluginID), e);
            }
        }

        private XmlSettings<T> settings;

        public void Load()
        {
            if (!Directory.Exists(initialPath)) {
                Directory.CreateDirectory(initialPath);
            }

            _instance = new T();
            settings = XmlSettings<T>.Bind(Instance, configFile);
        }


        // hackyness to stop WPF from loading in ehexthost, cause that screws up fonts, needs a refactor. 
        object oControlBindings = null;

        Dictionary<Control, AbstractMember> _controlBindings {
            get {
                if (oControlBindings == null) {
                    oControlBindings = new Dictionary<Control, AbstractMember>();
                }
                return oControlBindings as Dictionary<Control, AbstractMember>; 
            }
        } 
                
        public bool? BuildUI()
        {            
            Type type = Instance.GetType();

            Grid grid = BuildGrid();

            Window window  = BuildWindow();
            window.Content = (grid);

            foreach (var member in type.GetMembers()) {
                AbstractMember abstractMember = null;
                if (member.MemberType == MemberTypes.Property) {
                    abstractMember = new PropertyMember((PropertyInfo)member);
                } else if (member.MemberType == MemberTypes.Field) {
                    abstractMember = new FieldMember((FieldInfo)member);
                }
                if (abstractMember != null) {
                    BuildControl(grid, abstractMember, Instance);
                }
            }

            PopulateControls(Instance);

            StackPanel panel = BuildButtonPanel(window);

            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40) });
            Grid.SetRow(panel, grid.RowDefinitions.Count - 1);
            Grid.SetColumn(panel, 2);

            grid.Children.Add(panel);

            grid.Height = grid.RowDefinitions.Sum(o => o.Height.Value);
            grid.Width = grid.ColumnDefinitions.Sum(o => o.Width.Value);
            window.Height = grid.Height + 60;
            window.Width = grid.Width + 40;

            window.Owner = System.Windows.Application.Current.MainWindow;
            return window.ShowDialog();
        }

        void PopulateControls(PluginConfigurationOptions pluginConfigurationOptions)
        {
            foreach (var item in _controlBindings.Keys)
            {
                if (item is CheckBox)
                    (item as CheckBox).IsChecked = (bool)_controlBindings[item].Read(pluginConfigurationOptions);
                else if (item is TextBox)
                    (item as TextBox).Text = (string)_controlBindings[item].Read(pluginConfigurationOptions);
                else if (item is ComboBox)
                    (item as ComboBox).Text = (string)_controlBindings[item].Read(pluginConfigurationOptions);
            }
        }

        Grid BuildGrid()
        {
            Grid grid = new Grid() { Margin = new Thickness(10) };

            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(200) });
            return grid;
        }

        Window BuildWindow()
        {
            Window window = new Window();
            window.WindowStyle = WindowStyle.ToolWindow;
            window.Title = "Plugin Options";
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.ResizeMode = ResizeMode.NoResize;
            window.Icon = null;

            LinearGradientBrush linear = new LinearGradientBrush();
            linear.StartPoint = new Point(0.5, 0);
            linear.EndPoint = new Point(0.5, 1);
            linear.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0.853));
            linear.GradientStops.Add(new GradientStop(Color.FromArgb(255, 202, 192, 192), 1));

            window.Background = linear;

            return window;

        }

        StackPanel BuildButtonPanel(Window window)
        {
            StackPanel panel = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Height = 40 };
            Button ok = new Button() { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(5, 10, 5, 0), Content = "OK", Height = 25, Width = 60, IsDefault = true };
            Button reset = new Button() { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(5, 10, 5, 0), Content = "Reset", Height = 25, Width = 60 };
            Button cancel = new Button() { IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(5, 10, 0, 0), Content = "Cancel", Height = 25, Width = 60 };
            
            // capture
            var w = window; 
            ok.Click += new RoutedEventHandler((x, y) => { w.DialogResult = true; });
            cancel.Click += new RoutedEventHandler((x, y) => { w.DialogResult = false; });
            reset.Click += new RoutedEventHandler(reset_Click);
            panel.Children.Add(ok);
            panel.Children.Add(reset);
            panel.Children.Add(cancel);
            return panel;
        }

        void BuildControl(Grid grid, AbstractMember member, PluginConfigurationOptions pluginConfigurationOptions)
        {
            Control control = null;
            object[] attributes = member.GetAttributes();

            if (attributes == null || attributes.Length == 0)
                return;

            LabelAttribute labelAttribute = attributes.Select(x => x as LabelAttribute).Where(i => i != null).First();

            bool isBool = member.Type == typeof(bool);
            bool isString = member.Type == typeof(string);
            bool isChoice = attributes.FirstOrDefault(x => x is ItemsAttribute) != null;

            if (isBool) {
                control = new CheckBox() { VerticalAlignment = VerticalAlignment.Center, IsChecked = (bool)member.Read(pluginConfigurationOptions), Name = member.Name };
                (control as CheckBox).Checked += new RoutedEventHandler(PluginConfigureView_Checked);
                (control as CheckBox).Unchecked += new RoutedEventHandler(PluginConfigureView_Checked);
            } else if (isChoice) {
                control = new ComboBox() { Margin = new Thickness(0, 2, 0, 2), Name = member.Name, Width = 200 };
                (control as ComboBox).SelectionChanged += new SelectionChangedEventHandler(PluginConfigureView_SelectionChanged);
                ItemsAttribute itemsAttribute = attributes.Select(x => x as ItemsAttribute).Where(i => i != null).First();
                foreach (var item in itemsAttribute.Items.Split(','))
                    (control as ComboBox).Items.Add(item);
                (control as ComboBox).Text = (string)member.Read(pluginConfigurationOptions);
            } 
            else if (isString) {
                control = new TextBox() { Margin = new Thickness(0, 2, 0, 2), Text = (string)member.Read(pluginConfigurationOptions), Name = member.Name, Width = 200 };
                (control as TextBox).TextChanged += new TextChangedEventHandler(PluginConfigureView_TextChanged);
            } else {
                return;
            }

            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(30) });
            Label label = new Label() { Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Right, Content = labelAttribute.Label };
            grid.Children.Add(label);
            Grid.SetColumn(label, 0);
            Grid.SetRow(label, grid.RowDefinitions.Count - 1);
            grid.Children.Add(control);
            Grid.SetColumn(control, 1);
            Grid.SetRow(control, grid.RowDefinitions.Count - 1);
            _controlBindings.Add(control, member);
        }


        #region events
        void PluginConfigureView_Checked(object sender, RoutedEventArgs e)
        {
            ValueChanged(sender);
        }

        void reset_Click(object sender, RoutedEventArgs e)
        {
            Reset();
            PopulateControls(Instance);
        }

        void PluginConfigureView_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValueChanged(sender);
        }

        void PluginConfigureView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValueChanged(sender);
        }

        void ValueChanged(object sender)
        {
            if (_controlBindings.Keys.Contains((Control)sender))
            {
                if (sender is CheckBox)
                {
                    _controlBindings[(Control)sender].Write(Instance, (sender as CheckBox).IsChecked);
                }
                else if (sender is TextBox)
                {
                    _controlBindings[(Control)sender].Write(Instance, (sender as TextBox).Text);
                }
                else if (sender is ComboBox)
                {
                    _controlBindings[(Control)sender].Write(Instance, (sender as ComboBox).SelectedValue);
                }
            }
        }
        #endregion
    }
}
