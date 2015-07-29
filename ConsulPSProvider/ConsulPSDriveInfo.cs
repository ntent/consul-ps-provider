using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Consul;

namespace ConsulPSProvider
{
    public class ConsulPSDriveInfo : PSDriveInfo
    {
        public Client ConsulClient;
        
        public ConsulPSDriveInfo(PSDriveInfo driveInfo) : base(driveInfo)
        {

            // TODO: Check if the consul root is valid?

            // TODO: Support DataCenter, AuthToken, HttpAuthentication

            // connection is specified as a URI to the consul http interface
            var consulUri = new Uri(driveInfo.Root);

            ConsulClient = new Client(new ConsulClientConfiguration { Address = consulUri.Host + ":" + consulUri.Port , Scheme = consulUri.Scheme } );

        }
    }
}
