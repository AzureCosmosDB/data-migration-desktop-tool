using CommunityToolkit.Mvvm.Messaging;
using Cosmos.DataTransfer.App.Windows.Framework;
using Cosmos.DataTransfer.Ui.Common;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Cosmos.DataTransfer.App.Windows;

public class LogViewModel : ViewModelBase
{
    public LogViewModel()
    {
        Document.FontFamily = new("Consolas");
        Document.FontSize = 12;
        Document.LineHeight = 18;
        Document.Background = Brushes.Black;
        Document.Blocks.Add(new Paragraph 
        {
            TextAlignment = TextAlignment.Left,
        });
        Document.PagePadding = new(5);

        BindingOperations.EnableCollectionSynchronization(Messages, new object());
        Messenger.Register(this, (MessageHandler<object, LogMessage>)((s, m) =>
        {
            Messages.Add(m);

            var output = new Run($"{m.Text}\n");

            switch (m.Type)
            {
                case MessageType.Error:
                    output.Foreground = Brushes.Red;
                    break;
                case MessageType.AppLogError:
                    output.Foreground = new SolidColorBrush(Colors.Red) { Opacity = 0.75 };
                    break;
                case MessageType.Warning:
                    output.Foreground = Brushes.Orange;
                    break;
                case MessageType.AppLogWarning:
                    output.Foreground = new SolidColorBrush(Colors.Orange) { Opacity = 0.75 };
                    break;
                case MessageType.AppLogInfo:
                case MessageType.AppLog:
                    output.Foreground = new SolidColorBrush(Colors.White) { Opacity = 0.5 };
                    break;
                case MessageType.Data:
                default:
                    output.Foreground = Brushes.White;
                    break;
            }

            (Document.Blocks.LastBlock as Paragraph)?.Inlines.Add(output);
        }));
    }

    public ObservableCollection<LogMessage> Messages { get; } = new();

    public FlowDocument Document { get; } = new();
}
