using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using DotRas;

namespace BojanKom.IpSecClient
{
    public class IpSecClient : IDisposable
    {
        private string _vpnConnectionName;
        private static string _phoneBookPath;
        private RasPhoneBook _rasPhoneBook;
        private RasDialer _rasDialer;
        private RasConnectionWatcher _rasConnectionWatcher;
        private TaskCompletionSource<object> _taskCompletionSource;

        static IpSecClient()
        {
            // Phone book path for current user: 
            //    C:\Users\UserName\AppData\Roaming\Microsoft\Network\Connections\Pbk\rasphone.pbk
            // Phone book path for All users: 
            //    C:\ProgramData\Microsoft\Network\Connections\Pbk\rasphone.pbk
            IpSecClient._phoneBookPath = RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User);
            Console.WriteLine(
                $"Phone book path for current user: {Environment.NewLine}{IpSecClient._phoneBookPath}{Environment.NewLine}"
            );
        }

        #region Public methods
        public void ConnectionsList()
        {
            using (var rasPhoneBook = new RasPhoneBook())
            {
                // If RasPhoneBookType.AllUsers and code is executed with non-admin privileges:
                // System.UnauthorizedAccessException: 
                // Access to the path 'C:\ProgramData\Microsoft\Network\Connections\Pbk\rasphone.pbk' is denied.
                rasPhoneBook.Open(IpSecClient._phoneBookPath);

                // Diagnostics
                Console.WriteLine("VPN connections: ");
                IpSecClient.PhoneBookEntriesList(rasPhoneBook);
                Console.WriteLine();
            }
        }

        public void IpSecConnectionCreate(string ipSecConnectionName, string presharedKey)
        {
            if (ipSecConnectionName == null)
            {
                throw new ArgumentNullException(nameof(ipSecConnectionName));
            }

            if (presharedKey == null)
            {
                throw new ArgumentNullException(nameof(presharedKey));
            }

            if (ipSecConnectionName.Length == 0)
            {
                throw new ArgumentException($"nameof(ipSecConnectionName) is empty string");
            }

            if (presharedKey.Length == 0)
            {
                throw new ArgumentException($"nameof(presharedKey) is empty string");
            }

            using (var rasPhoneBook = new RasPhoneBook())
            {
                rasPhoneBook.Open(IpSecClient._phoneBookPath);

                var ipSecRasEntry =
                    RasEntry.CreateVpnEntry(
                        ipSecConnectionName,
                        "0.0.0.0",
                        RasVpnStrategy.L2tpOnly,
                        RasDevice.Create(ipSecConnectionName, RasDeviceType.Vpn)
                    );

                //ipSecRasEntry.DnsAddress = System.Net.IPAddress.Parse("0.0.0.0");
                //ipSecRasEntry.DnsAddressAlt = System.Net.IPAddress.Parse("0.0.0.0");
                ipSecRasEntry.EncryptionType = RasEncryptionType.Require;
                ipSecRasEntry.EntryType = RasEntryType.Vpn;
                ipSecRasEntry.Options.RequireDataEncryption = true;
                ipSecRasEntry.Options.UsePreSharedKey = true; // used only for IPSec - L2TP/IPsec VPN
                ipSecRasEntry.Options.UseLogOnCredentials = false;
                ipSecRasEntry.Options.RequireMSChap2 = true;
                //rasEntry.Options.RemoteDefaultGateway = true;
                ipSecRasEntry.Options.SecureFileAndPrint = true;
                ipSecRasEntry.Options.SecureClientForMSNet = true;
                ipSecRasEntry.Options.ReconnectIfDropped = false;

                // If the entry with the same name has already been added, Add throws ArgumentException
                // with message: '<Entry name>' already exists in the phone book.\r\nParameter name: item"  
                rasPhoneBook.Entries.Add(ipSecRasEntry);

                // RasEntry.UpdateCredentials() has to be executed after RasEntry has been added to the Phone Book.
                // Otherwise InvalidOperationException is thrown  with message:
                // "The entry is not associated with a phone book."
                ipSecRasEntry.UpdateCredentials(RasPreSharedKey.Client, presharedKey);
                //ipSecRasEntry.UpdateCredentials(new System.Net.NetworkCredential("username", "password"));

                Console.WriteLine($"VPN connection {ipSecConnectionName} created successfully.");
            }
        }

        public void IpSecConnectionRemove(string ipSecConnectionName)
        {
            if (ipSecConnectionName == null)
            {
                throw new ArgumentNullException(nameof(ipSecConnectionName));
            }

            if (ipSecConnectionName.Length == 0)
            {
                throw new ArgumentException($"nameof(ipSecConnectionName) is empty string");
            }

            using (var rasPhoneBook = new RasPhoneBook())
            {
                rasPhoneBook.Open(IpSecClient._phoneBookPath);

                var ipSecRasEntry = IpSecClient.RasEntryFindByName(rasPhoneBook, ipSecConnectionName);

                if(ipSecRasEntry.Remove())
                {
                    Console.WriteLine($"VPN connection {ipSecConnectionName} removed successfully.");
                }
                else
                {
                    throw new Exception("RasEntry.Remove() failed.");
                }
            }
        }

        public Task ConnectAsync(
            IVpnEndpoint vpnEndpoint, 
            Dictionary<string, object> parameters, 
            CancellationToken cancellationToken
        )
        {
            if (vpnEndpoint == null)
            {
                throw new ArgumentNullException(nameof(vpnEndpoint));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (!parameters.ContainsKey("username"))
            {
                throw new ArgumentException($"{nameof(parameters)} does not contain key \"username\"");
            }

            if (!parameters.ContainsKey("password"))
            {
                throw new ArgumentException($"{nameof(parameters)} does not contain key \"password\"");
            }

            if (!parameters.ContainsKey("IpSecConnectionName"))
            {
                throw new ArgumentException($"{nameof(parameters)} does not contain key \"IpSecConnectionName\"");
            }

            var ipAddress = vpnEndpoint.IpAddress;

            if (String.IsNullOrEmpty(ipAddress))
            {
                throw new ArgumentException("ipAddress is null or empty string");
            }

            var username = (string)parameters["username"];
            
            if (String.IsNullOrEmpty(username))
            {
                throw new ArgumentException("username is null or empty string");
            }

            var password = (string)parameters["password"];

            if (String.IsNullOrEmpty(password))
            {
                throw new ArgumentException("password is null or empty string");
            }

            var ipSecConnectionName = (string)parameters["IpSecConnectionName"];

            if (String.IsNullOrEmpty(ipSecConnectionName))
            {
                throw new ArgumentException("IpSecConnectionName is null or empty string");
            }

            this._vpnConnectionName = ipSecConnectionName;

            this.VpnConnectionBind();
            var rasEntry = this.IpSecRasEntryGet(ipSecConnectionName);

            this._rasDialer.PhoneNumber = ipAddress;
            this._rasDialer.EntryName = ipSecConnectionName;
            this._rasDialer.PhoneBookPath = IpSecClient._phoneBookPath;
            this._rasDialer.Credentials = new System.Net.NetworkCredential(username, password);

           this._taskCompletionSource = new TaskCompletionSource<object>();
            
            var rasHandle = this._rasDialer.DialAsync();
            return this._taskCompletionSource.Task;
        }

        /// <summary>
        /// This method can be invoked independently from ConnectAsync().
        /// E.g. on applicaation launch, we want to terminate any live VPN connections.
        /// </summary>
        /// <returns></returns>
        public Task DisconnectAsync()
        {
            this._taskCompletionSource = new TaskCompletionSource<object>();

            var rasConnection = this.IpSecActiveConnectionGet(this._vpnConnectionName);
            if (rasConnection != null)
            {
                Console.WriteLine("Hanging up the connection...");
                rasConnection.HangUp();
            }

            return this._taskCompletionSource.Task;
        }

        #endregion

        #region Private Event Handlers
        private void _rasConnectionWatcher_Connected(object sender, RasConnectionEventArgs e)
        {
            Console.WriteLine("Connected");
            this._taskCompletionSource.SetResult(null);
        }

        private void _rasConnectionWatcher_Error(object sender, System.IO.ErrorEventArgs e)
        {
            Console.WriteLine("Error");
            this._taskCompletionSource.SetException(e.GetException());
        }

        private void _rasDialer_DialCompleted(object sender, DialCompletedEventArgs e)
        {
            Console.WriteLine("Dial completed");

            if (e.Cancelled)
            {
                Console.WriteLine("\tConnection canceled");
            }
            else if (e.TimedOut)
            {
                Console.WriteLine("\tConnection timeout");
            }
            else if (e.Error != null)
            {
                this._taskCompletionSource.SetException(e.Error);

                //if (this._rasDialer.IsBusy)
                //{
                //    Console.WriteLine("\trasDialer is BUSY");
                //}
            }
            else
            {
                // Start monitoring the connection
                var rasConnection = this.IpSecActiveConnectionGet(this._vpnConnectionName);
                if (rasConnection != null)
                {
                    RasIPInfo rasInfo = (RasIPInfo)rasConnection.GetProjectionInfo(RasProjectionType.IP);

                    this._rasConnectionWatcher.Handle = rasConnection.Handle;
                    this._rasConnectionWatcher.EnableRaisingEvents = true;
                }

                Console.WriteLine("\tConnection established");
                this._taskCompletionSource.SetResult(null);
            }
        }

        private void _rasDialer_StateChanged(object sender, StateChangedEventArgs e)
        {
            Console.WriteLine("State changed to: " + e.State.ToString());
        }

        private void _rasConnectionWatcher_Disconnected(object sender, RasConnectionEventArgs e)
        {
            Console.WriteLine("Disconnected");
            this._taskCompletionSource.SetResult(null);
        }
        #endregion

        #region Private methods

        /// <summary>
        /// Binds to existing IPSec VPN connection. 
        /// </summary>
        private void VpnConnectionBind()
        {
            this._rasDialer = new RasDialer();
            this._rasConnectionWatcher = new RasConnectionWatcher();

            //this._rasDialer.Credentials = null;
            //this._rasDialer.EapData = null;
            this._rasDialer.EapOptions = new RasEapOptions(false, false, false);
            this._rasDialer.HangUpPollingInterval = 0;
            this._rasDialer.Options =
                new RasDialOptions(false, false, false, false, false, false, false, false, false, false, false);
            this._rasDialer.DialCompleted += _rasDialer_DialCompleted;
            this._rasDialer.StateChanged += _rasDialer_StateChanged;

            this._rasConnectionWatcher.Handle = null;
            this._rasConnectionWatcher.Disconnected += _rasConnectionWatcher_Disconnected;
            this._rasConnectionWatcher.Connected += _rasConnectionWatcher_Connected;
            this._rasConnectionWatcher.Error += _rasConnectionWatcher_Error;
        }

        /// <summary>
        /// Method used for diagnostics.
        /// </summary>
        /// <param name="phoneBook"></param>
        private static void PhoneBookEntriesList(RasPhoneBook phoneBook)
        {
            foreach (RasEntry entry in phoneBook.Entries)
            {
                Console.WriteLine($"\t{entry.Name}");
            }
        }

        private RasConnection IpSecActiveConnectionGet(string vpnConnectionName)
        {
            return
                RasConnection.GetActiveConnectionByName(vpnConnectionName, IpSecClient._phoneBookPath);
        }

        private static RasEntry RasEntryFindByName(RasPhoneBook rasPhoneBook, string rasEntryName)
        {
            return rasPhoneBook.Entries.First(entry => entry.Name == rasEntryName);
        }

        private RasEntry IpSecRasEntryGet(string ipSecConnectionName)
        {
            RasEntry ipSecRasEntry = null;

            using (var rasPhoneBook = new RasPhoneBook())
            { 
                rasPhoneBook.Open(IpSecClient._phoneBookPath);

                ipSecRasEntry = IpSecClient.RasEntryFindByName(rasPhoneBook, ipSecConnectionName);
            }

            return ipSecRasEntry;
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    if (this._rasConnectionWatcher != null)
                    {
                        this._rasConnectionWatcher.Dispose();
                        this._rasConnectionWatcher = null;
                    }

                    if (this._rasPhoneBook != null)
                    {
                        this._rasPhoneBook.Dispose();
                        this._rasPhoneBook = null;
                    }

                    if (this._rasDialer != null)
                    {
                        this._rasDialer.Dispose();
                        this._rasDialer = null;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~IpSecClient() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
