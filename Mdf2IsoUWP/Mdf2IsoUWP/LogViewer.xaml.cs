using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Mdf2IsoUWP
{
    public sealed partial class LogViewer : UserControl
    {
        public StreamWriter LogWriter => new StreamWriter(
            new LogStream()
            {
                LogBlock = LogTextBlock
            })
        {
            AutoFlush = true
        };

        public LogViewer()
        {
            this.InitializeComponent();
        }

        class LogStream : Stream
        {
            MemoryStream ms = new MemoryStream();

            public TextBlock LogBlock { get; set; }

            public override void Flush()
            {
                FlushAsync().Wait();
            }

            public override async Task FlushAsync(CancellationToken cancellationToken)
            {
                if (LogBlock == null)
                    throw new ArgumentException("LogStream uninitialized");

                string message = Encoding.UTF8.GetString(ms.ToArray());
                ms.SetLength(0);
                await LogBlock.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () => LogBlock.Text += message);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                ms.Write(buffer, offset, count);
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await ms.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override bool CanRead { get; } = false;
            public override bool CanSeek { get; } = false;
            public override bool CanWrite { get; } = true;
            public override long Length => ms.Length;
            public override long Position { get => ms.Position; set => ms.Position = value; }
        }
    }
}
