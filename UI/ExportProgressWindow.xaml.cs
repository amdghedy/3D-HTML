using System;
using System.Windows;
using System.Windows.Threading;
using ClearView.Export;

namespace ClearView.UI
{
    public partial class ExportProgressWindow : Window
    {
        private readonly RevitExportContext _context;
        private readonly DispatcherTimer _timer;

        public ExportProgressWindow(RevitExportContext context)
        {
            InitializeComponent();
            _context = context;
            _context.ProgressChanged += OnProgress;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += (s, e) => Refresh();
            _timer.Start();
            Closed += (s, e) =>
            {
                _timer.Stop();
                _context.ProgressChanged -= OnProgress;
            };
        }

        public void SetStatus(string text)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusText.Text = text;
            }));
        }

        void OnProgress()
        {
            Dispatcher.BeginInvoke(new Action(Refresh));
        }

        void Refresh()
        {
            StatusText.Text = _context.StatusMessage +
                              "  ·  meshes " + _context.Meshes.Count +
                              "  ·  tris " + _context.TriangleCount;
        }

        void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _context.CancelRequested = true;
            StatusText.Text = "Cancel requested… finishing current element";
            Bar.IsIndeterminate = false;
            Bar.Value = 0;
        }
    }
}
