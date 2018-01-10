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

        // Issue #1 - Don't want a trailing slash, just host and port (this needs to be done at the 'Root' of the drive)
        public ConsulPSDriveInfo(PSDriveInfo driveInfo) : base(driveInfo.Name,driveInfo.Provider,driveInfo.Root.TrimEnd('/'),driveInfo.Description,driveInfo.Credential,driveInfo.DisplayRoot)
        {

            // TODO: Check if the consul root is valid?

            // TODO: Support DataCenter, HttpAuthentication

            // connection is specified as a URI to the consul http interface. .
            var consulUri = new Uri(Root);

            var config = new ConsulClientConfiguration
            {
                Address = consulUri.Host + ":" + consulUri.Port,
                Scheme = consulUri.Scheme
            };

            if (driveInfo.Credential != null && !string.IsNullOrEmpty(driveInfo.Credential.UserName))
            {
                config.HttpAuth = driveInfo.Credential.GetNetworkCredential();
            }

            // AuthToken taken from Credential UserName if available.
            if (driveInfo.Credential != null && !string.IsNullOrWhiteSpace(driveInfo.Credential.UserName))
                config.Token = driveInfo.Credential.UserName;
            
            ConsulClient = new Client(config);

        }
    }
}
