using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace Mdf2IsoUWP.CustomControls
{
    public class LogBox : TextBox
    {
        public StreamWriter LogWriter => new StreamWriter(
            new LogBoxStream()
            {
                LogBox = this
            });
    }

    class LogBoxStream : Stream
    {
        MemoryStream ms = new MemoryStream();

        public LogBox LogBox { get; set; }
        
        public override void Flush()
        {
            FlushAsync().Wait();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (LogBox == null)
                throw new ArgumentException("LogStream uninitialized");

            string message = Encoding.UTF8.GetString(ms.ToArray());
            await LogBox.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () => LogBox.Text += message);
            ms.Position = 0;
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
        public override long Length { get; } = 0;
        public override long Position { get; set; } = 0;
    }
}
