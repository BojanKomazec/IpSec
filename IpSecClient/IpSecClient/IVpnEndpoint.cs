using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BojanKom.IpSecClient
{
    public interface IVpnEndpoint
    {
        string IpAddress { get; }
    }
}
