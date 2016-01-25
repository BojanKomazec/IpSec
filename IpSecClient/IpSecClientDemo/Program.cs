using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using BojanKom.IpSecClient;

namespace IpSecClientDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var menu =
                "Type number for option you wish: " + Environment.NewLine +
                "1) List all connections" + Environment.NewLine +
                "2) Create new IPSec Connection" + Environment.NewLine +
                "3) Connect/Disconnect demo" + Environment.NewLine +
                "4) Remove IPSec Connection" + Environment.NewLine +
                Environment.NewLine + "Your choice: ";
            Console.WriteLine(menu);
            var choiceStr = Console.ReadLine();
            int choice;

            if (Int32.TryParse(choiceStr, out choice))
            {
                switch (choice)
                {
                    case 1:
                        Program.ConnectionsList();
                        break;
                    case 2:
                        Program.ConnectionsList();
                        Program.IpSecConnectionCreate();
                        break;
                    case 3:
                        Program.ConnectionsList();
                        Program.IpSecConnectionDisconnectionDemoAsync().Wait();
                        break;
                    case 4:
                        Program.ConnectionsList();
                        Program.IpSecConnectionRemove();
                        break;
                    default:
                        Console.WriteLine("ERROR: invalid choice number");
                        break;
                }
            }
            else
            {
                Console.WriteLine("ERROR: invalid choice number");
            }

            Console.WriteLine("Press <ENTER> to terminate...");
            Console.ReadLine();
        }

        private static void ConnectionsList()
        {
            try
            {
                using (var ipSecClient = new IpSecClient())
                {
                    ipSecClient.ConnectionsList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void IpSecConnectionCreate()
        {
            try
            {
                Console.Write("IPSec connection name: ");
                var ipSecConnectionName = Console.ReadLine();

                Console.Write("Preshared key: ");
                var presharedKey = Console.ReadLine();

                using (var ipSecClient = new IpSecClient())
                {
                    ipSecClient.IpSecConnectionCreate(ipSecConnectionName, presharedKey);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void IpSecConnectionRemove()
        {
            try
            {
                Console.Write("IPSec connection name: ");
                var ipSecConnectionName = Console.ReadLine();

                using (var ipSecClient = new IpSecClient())
                {
                    ipSecClient.IpSecConnectionRemove(ipSecConnectionName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static async Task IpSecConnectionDisconnectionDemoAsync()
        {
            using (var ipSecClient = new IpSecClient())
            {
                try
                {
                    Console.Write("IPSec connection name: ");
                    var ipSecConnectionName = Console.ReadLine();

                    Console.Write("IPSec Server IP address: ");
                    var ipAddress = Console.ReadLine();

                    Console.Write("Username: ");
                    var username = Console.ReadLine();

                    Console.Write("Password: ");
                    var password = Console.ReadLine(); // PPTP/L2TP pass

                    var vpnEndPoint = new VpnEndpoint(ipAddress);
                    var parameters = new Dictionary<string, object>()
                    {
                        {"username", username},
                        {"password", password},
                        {"IpSecConnectionName", ipSecConnectionName}
                    };

                    Console.WriteLine("Press <ENTER> to Connect...");
                    Console.ReadLine();

                    await ipSecClient.ConnectAsync(vpnEndPoint, parameters, CancellationToken.None);
                    Program.PublicIpAddressVerify();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                Console.WriteLine("Press <ENTER> to Disconnect...");
                Console.ReadLine();

                try
                {
                    await ipSecClient.DisconnectAsync();
                    Program.PublicIpAddressVerify();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        private static void PublicIpAddressVerify()
        {
            Console.WriteLine("Verifying public IP address...");
            System.Diagnostics.Process.Start("https://geoip.hmageo.com");
        }
    }

    public class VpnEndpoint : IVpnEndpoint
    {
        public string IpAddress
        {
            get;
            private set;
        }

        public VpnEndpoint(string ipAddress)
        {
            if (ipAddress == null)
            {
                throw new ArgumentNullException(nameof(ipAddress));
            }

            if(ipAddress.Length == 0)
            {
                throw new ArgumentException("ipAddress is empty string");
            }

            this.IpAddress = ipAddress;
        }
    }
}
