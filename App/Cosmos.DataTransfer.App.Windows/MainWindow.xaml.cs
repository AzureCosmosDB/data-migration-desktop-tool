using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Cosmos.DataTransfer.Ui.Common;

namespace Cosmos.DataTransfer.App.Windows;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (Properties.Settings.Default.WindowSize.Width > 0)
        {
            Width = Properties.Settings.Default.WindowSize.Width;
            Height = Properties.Settings.Default.WindowSize.Height;
            Left = Properties.Settings.Default.WindowPosition.X;
            Top = Properties.Settings.Default.WindowPosition.Y;
        }

        if (Properties.Settings.Default.WindowMaximized)
            WindowState = WindowState.Maximized;

        Closing += (s, e) =>
        {
            if (WindowState == WindowState.Maximized)
            {
                Properties.Settings.Default.WindowPosition = new System.Drawing.Point((int)RestoreBounds.Location.X, (int)RestoreBounds.Location.Y);
                Properties.Settings.Default.WindowSize = new System.Drawing.Size((int)RestoreBounds.Size.Width, (int)RestoreBounds.Size.Height);
                Properties.Settings.Default.WindowMaximized = true;
            }
            else
            {
                Properties.Settings.Default.WindowPosition = new System.Drawing.Point((int)Left, (int)Top);
                Properties.Settings.Default.WindowSize = new System.Drawing.Size((int)Width, (int)Height);
                Properties.Settings.Default.WindowMaximized = false;
            }
            Properties.Settings.Default.Save();
        };
    }

    private void AllowOnlyNumbers(object sender, TextCompositionEventArgs e)
    {
        var regex = new Regex("[^0-9]+", RegexOptions.Compiled);
        e.Handled = regex.IsMatch(e.Text);
    }

    private void AllowOnlyFloatNumbers(object sender, TextCompositionEventArgs e)
    {
        var regex = new Regex("[^0-9.]+", RegexOptions.Compiled);
        e.Handled = regex.IsMatch(e.Text);
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        const string url = "https://github.com/AzureCosmosDB/data-migration-desktop-tool";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }
}

public class SettingEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Default { get; set; }
    public DataTemplate? Boolean { get; set; }
    public DataTemplate? Number { get; set; }
    public DataTemplate? Float { get; set; }
    public DataTemplate? SelectList { get; set; }
    public DataTemplate? MultiLine { get; set; }
    public DataTemplate? Date { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not ExtensionSetting setting)
            return Default;
        switch (setting.Definition.Type)
        {
            case PropertyType.Boolean:
                return Boolean;
            case PropertyType.Int:
                return Number;
            case PropertyType.Float:
                return Float;
            case PropertyType.DateTime:
                return Date;
            case PropertyType.Enum:
                return SelectList;
            case PropertyType.Array:
                return MultiLine;
            case PropertyType.String:
            case PropertyType.Undeclared:
            default:
                return Default;
        }
        return base.SelectTemplate(item, container);
    }
}
