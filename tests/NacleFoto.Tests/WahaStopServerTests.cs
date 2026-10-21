using System.Net;
using System.Net.Sockets;
using NacleFoto.Core.Linking;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// The "Stop server" button needs to find the process listening
    /// on the port (WAHA runs detached, outside the app).
    /// </summary>
    public class WahaStopServerTests
    {
        [Fact]
        public void Finds_the_process_listening_on_the_port()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int porta = ((IPEndPoint)listener.LocalEndpoint).Port;
                int pid = WahaManager.ProcessOnPort(porta);
                Assert.Equal(System.Diagnostics.Process.GetCurrentProcess().Id, pid);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public void Returns_zero_when_nothing_listens_on_the_port()
        {
            // high port, likely free
            Assert.Equal(0, WahaManager.ProcessOnPort(45999));
        }
    }
}
