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

            // TODO: Support DataCenter, HttpAuthentication

            // connection is specified as a URI to the consul http interface. Don't want a trailing slash, just host and port.
            var consulUri = new Uri(driveInfo.Root.TrimEnd('/'));

            var config = new ConsulClientConfiguration
            {
                Address = consulUri.Host + ":" + consulUri.Port,
                Scheme = consulUri.Scheme
            };
            
            // AuthToken taken from Credential UserName if available.
            if (driveInfo.Credential != null && !string.IsNullOrWhiteSpace(driveInfo.Credential.UserName))
                config.Token = driveInfo.Credential.UserName;
            
            ConsulClient = new Client(config);

        }
    }
}
