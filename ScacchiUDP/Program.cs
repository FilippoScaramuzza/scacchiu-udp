using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using System.Net;
using System.Net.Sockets;

namespace ScacchiUDP
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string intUdpLocalPort = Interaction.InputBox("Inserisci la porta locale:", "Porta Locale", "5000");
            if (intUdpLocalPort == "") return;
            string intUdpRemotePort = Interaction.InputBox("Inserisci la porta remota:", "Porta Remota", "5000");
            if (intUdpRemotePort == "") return;
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            IPAddress localIp = null;
            foreach (IPAddress addr in localIPs)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIp = addr;
                }
            }
            string strIpRemote = Interaction.InputBox("Inserisci l'indirizzo IP remoto\n( il tuo indirizzo IPV4 è " + localIp.ToString() + " )", "IP Remoto", "192.168.1.107");
            if (strIpRemote == "") return;
            Application.Run(new FormScacchi(Convert.ToInt16(intUdpLocalPort),Convert.ToInt16(intUdpRemotePort), strIpRemote));
        }
    }
}
