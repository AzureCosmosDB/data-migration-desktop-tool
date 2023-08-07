using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace Cosmos.DataTransfer.App.Windows
{
    /// <summary>
    /// Interaction logic for MessageLog.xaml
    /// </summary>
    public partial class MessageLog : UserControl
    {
        public MessageLog()
        {
            InitializeComponent();

            var logs = App.Current.Services.GetService<LogViewModel>();
            LayoutRoot.DataContext = logs;

            if (logs != null)
            {
                logs.Messages.CollectionChanged += (s, e) =>
                {
                    if (e.NewItems != null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            FindScrollViewer(DocumentScroll)?.ScrollToBottom();
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                };
            }
        }

        public static ScrollViewer? FindScrollViewer(DependencyObject parent)
        {
            if (VisualTreeHelper.GetChildrenCount(parent) == 0)
            {
                return null;
            }

            var firstChild = VisualTreeHelper.GetChild(parent, 0);
            if (firstChild == null)
            {
                return null;
            }

            return firstChild as ScrollViewer ?? FindScrollViewer(firstChild);
        }
    }
}
