using Consul;
using ConsulPSProvider;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Runtime.Caching;
using System.Text;

namespace Ntent.PowerShell.Providers.Consul
{
    [CmdletProvider("ConsulProvider",ProviderCapabilities.None)]
    public class ConsulProvider : NavigationCmdletProvider
    {
        static ConsulProvider()
        {
            NewCache();
        }

        private static void NewCache()
        {
            _cache = new MemoryCache("ConsulOpCache", new NameValueCollection {{"CacheMemoryLimitMegabytes", "5"}});
        }

        private static MemoryCache _cache;
        private const string PATH_SEPARATOR = "/";
        private ConsulPSDriveInfo ConsulDriveInfo {
            get
            {
                var consulPsDrive = this.PSDriveInfo as ConsulPSDriveInfo;
                if (consulPsDrive == null)
                    throw new Exception("Consul Provider must contain a ConsulPSDriveInfo instance for PSDriveInfo!");
                return consulPsDrive;
            } 
        }

        #region DriveCmdletProvider Methods

        protected override PSDriveInfo NewDrive(PSDriveInfo drive)
        {
            // Check if the drive object is null.
            if (drive == null)
            {
                WriteError(new ErrorRecord(
                           new ArgumentNullException("drive"),
                           "NullDrive",
                           ErrorCategory.InvalidArgument,
                           null));

                return null;
            }

            // Check if the drive root is not null or empty
            // and if it is an existing file.
            if (String.IsNullOrEmpty(drive.Root))
            {
                WriteError(new ErrorRecord(
                           new ArgumentException("drive.Root"),
                           "NoRoot",
                           ErrorCategory.InvalidArgument,
                           drive));

                return null;
            }

            // Create a new Consul drive from the default one passed.
            ConsulPSDriveInfo consulPSDriveInfo;

            try
            {
                consulPSDriveInfo = new ConsulPSDriveInfo(drive);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(
                           new ArgumentException("drive.Root"),
                           ex.Message,
                           ErrorCategory.InvalidArgument,
                           drive));

                return null;
            }

            return consulPSDriveInfo;
        }
        #endregion DriveCmdletProvider Methods

        #region ItemCmdletProvider Methods
        protected override bool IsValidPath(string path)
        {
            // cannot be null
            if (path == null)
                return false;

            // cannot be empty or whitespace
            if (path.Trim() == "")
                return false;

            bool result = true;

            // split the path into individual chunks
            string[] pathChunks = path.Split("/".ToCharArray());

            foreach (string pathChunk in pathChunks)
            {
                if (pathChunk.Length == 0)
                {
                    result = false;
                }

                // TODO: Validate other Uri path requirements?
            }

            return result;
        }

        protected override void GetItem(string path)
        {
            // check if the path represented is a drive
            if (PathIsDrive(path))
            {
                WriteItemObject(this.PSDriveInfo, path, true);
                return;
            }// if (PathIsDrive

            var normalPath = RemoveDriveFromPath(NormalizePath(path));
            var result = ConsulDriveInfo.ConsulClient.KV.Get(normalPath);
            if (result.Response == null)
                throw new ArgumentException("The item at the specified path could not be found: " + path);

            // for now, "IsContainer" uses the convention of the Consul UI that if the key ends in a trailing slash, it is a folder (container)
            WriteItemObject(new ConsulItem(result.Response), path, IsItemContainer(path, true));

        }

        protected override void SetItem(string path, object value)
        {
            var normalPath = RemoveDriveFromPath(NormalizePath(path));
            ConsulDriveInfo.ConsulClient.KV.Put(new KVPair(normalPath) { Value = Encoding.UTF8.GetBytes(value.ToString()) });

            // clear the cache since we changed data
            NewCache();

        }

        protected override bool ItemExists(string path)
        {
            // check for this path. The Keys command will list the exact path and any children.
            var normalPath = RemoveDriveFromPath(NormalizePath(path));

            // the root always exists.
            if (normalPath == "/")
                return true;
            var cacheKey = MakeKey(normalPath, "Exists");
            var exists = _cache.Get(cacheKey) as bool?;
            if (exists.HasValue)
                return exists.Value;

            var res = ConsulDriveInfo.ConsulClient.KV.Keys(normalPath, PATH_SEPARATOR);
            // the list must contain the incoming path or a subpath. 
            exists = res.Response != null && res.Response.Any(p=>IsSamePath(p,normalPath) || TrimSeparator(p).StartsWith(TrimSeparator(normalPath) + "/"));
            _cache.Set(cacheKey, exists, DateTimeOffset.Now.AddSeconds(2));
            return exists.Value;

        }

        /// <summary>
        /// Checks whether the specified path is a "container". 
        /// In Consul, there is really no notion of container other than by convention the key ends in a trailing slash.
        /// when navigating the PS provider, we don't want to force people to have to end paths with a trailing slash, so 
        /// we will by default be lenient on whether the path requested ends with a trailing slash.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        protected override bool IsItemContainer(string path)
        {
            return IsItemContainer(path, false);
        }

        /// <summary>
        /// Checks whether the specified path is a "container". 
        /// If the 'exact' parameter is passed as true, the path must end in a trailing slash. This form is called 
        /// from the Get-Item and Get-ChildItems methods when sending back whether that item is a container.
        /// This must be done because in Consul you can have both ./container and ./container/ where the former is not a container, but the latter is.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="exact"></param>
        /// <returns></returns>
        protected bool IsItemContainer(string path, bool exact)
        {
            // check for any children of this path. In Consul, a child path indicates that this is a container.
            var normalPath = RemoveDriveFromPath(NormalizePath(path));

            // looking for children, if the normalized path does not end in a path separator, then add one.
            var normalPathAsContainer = normalPath;
            if (!normalPath.EndsWith(PATH_SEPARATOR))
                normalPathAsContainer = TrimStartSeparator(normalPath + PATH_SEPARATOR);

            var cacheKey = MakeKey(normalPath, "IsContainer" + (exact ? "Exact" : ""));
            var isContainer = _cache.Get(cacheKey) as bool?;
            if (isContainer.HasValue)
                return isContainer.Value;

            var res = ConsulDriveInfo.ConsulClient.KV.Keys(normalPath, PATH_SEPARATOR);
            // the list always contains itself, so length > 1 means child items exist. Lenth == 1 and the item ends with a trailing / means this item is an empty container.
            isContainer = res.Response != null && res.Response.Length > 0 && (!exact || normalPath.EndsWith(PATH_SEPARATOR));

            _cache.Set(cacheKey, isContainer, DateTimeOffset.Now.AddSeconds(2));
            return isContainer.Value;
        }

        #endregion ItemCmdletProvider Methods

        #region ContainerCmdletProviderMethods

        protected override void GetChildItems(string path, bool recurse)
        {
            var normalPath = RemoveDriveFromPath(NormalizePath(path));

            // we are assuming that path is a container, so we will force a trailing separator if it isn't there.
            if (!normalPath.EndsWith(PATH_SEPARATOR))
                normalPath = normalPath + PATH_SEPARATOR;

            var res = ConsulDriveInfo.ConsulClient.KV.Keys(normalPath, PATH_SEPARATOR);
            if (res.Response != null)
            {
                foreach (var child in res.Response.Where(p => !IsSamePath(p, normalPath)))
                {
                    WriteItemObject(child.Substring(normalPath.Length - 1), child, IsItemContainer(child, true));
                }
            }

        }

        protected override void RemoveItem(string path, bool recurse)
        {
            var normalPath = RemoveDriveFromPath(NormalizePath(path));

            if (!recurse)
                ConsulDriveInfo.ConsulClient.KV.Delete(normalPath);
            else
                ConsulDriveInfo.ConsulClient.KV.DeleteTree(normalPath);

            // clear the cache since we changed data
            NewCache();
        }

        protected override bool HasChildItems(string path)
        {
            if (!IsItemContainer(path, false))
                return false;

            var normalPath = RemoveDriveFromPath(NormalizePath(path));
            var normalPathAsContainer = normalPath.EndsWith(PATH_SEPARATOR) ? normalPath : normalPath + PATH_SEPARATOR;

            var res = ConsulDriveInfo.ConsulClient.KV.Keys(normalPathAsContainer, PATH_SEPARATOR);

            // when checking keys, we know we have a container already per above, so look for any items with length greater than itself.
            return res.Response != null && (res.Response.Length > 1
                || res.Response.Any(k => k.Length > TrimStartSeparator(normalPathAsContainer).Length));
        }

        protected override void NewItem(string path, string itemTypeName, object newItemValue)
        {
            var normalPath = RemoveDriveFromPath(NormalizePath(path));
            if (itemTypeName == "Directory")
                normalPath = normalPath.EndsWith(PATH_SEPARATOR) ? normalPath : normalPath + PATH_SEPARATOR;
            else
                normalPath = normalPath.EndsWith(PATH_SEPARATOR) ? normalPath.Substring(0,normalPath.Length-1) : normalPath;

            ConsulDriveInfo.ConsulClient.KV.Put(new KVPair(normalPath) { Value = newItemValue == null ? null : Encoding.UTF8.GetBytes(newItemValue.ToString())});

            // clear the cache since we changed data
            NewCache();
        }

        #endregion ContainerCmdletProviderMethods

        #region NavigationCmdletProviderMethods

        protected override void MoveItem(string path, string destination)
        {
            CopyItem(path, destination, true, true);
        }

        protected override void CopyItem(string path, string copyPath, bool recurse)
        {
            CopyItem(path, copyPath, recurse, false);
        }

        protected void CopyItem(string path, string copyPath, bool recurse, bool deleteSrc)
        {
            var normalSrc = RemoveDriveFromPath(NormalizePath(path));
            var normalDst = RemoveDriveFromPath(NormalizePath(copyPath));

            // if we are moving (deleteSrc == true) then destination can't be a child of source.
            if (normalDst.StartsWith(normalSrc))
                throw new ArgumentException(string.Format("The destination {0} cannot be a child of the source {1}", normalDst, normalSrc));

            // if the destination exists as a container, we want to copy the source INTO the destination container (keeping the source's name).
            //    e.g. cp src dst => dst/src/...
            // if the destination does NOT exist as a container, we want to copy the source to an item *called* the destination name.
            var dstExists = ItemExists(normalDst) && IsItemContainer(normalDst);
            var dstRoot = dstExists ? normalDst : GetParentPath(normalDst, "");
            var dstName = dstExists ? GetChildName(normalSrc) : GetChildName(normalDst);

            KVPair[] itemsToCopy;
            // if they did NOT pass -recurse, then all we need to do is copy one item (the exact path of the source, be it a container or an item)
            if (!recurse)
            {
                var item = ConsulDriveInfo.ConsulClient.KV.Get(normalSrc);
                if (item.Response == null)
                    item = ConsulDriveInfo.ConsulClient.KV.Get(normalSrc + PATH_SEPARATOR);
                if (item.Response == null)
                    return;
                itemsToCopy = new [] { item.Response };
            }
            else
            {
                // iterate and copy all values from source to dest.
                var res = ConsulDriveInfo.ConsulClient.KV.List(normalSrc);
                if (res.Response == null)
                    return;

                itemsToCopy = res.Response;
            }
            

            foreach (var item in itemsToCopy)
            {
                // trim the parent path from the source, and add to the destination
                var itemRelPath = ("/" + item.Key).Substring(normalSrc.Length);

                var dstPath = NormalizePath(MakePath(dstRoot, dstName));

                // combine the destination root with the destination name and the item relative path
                if (itemRelPath != "")
                    dstPath = NormalizePath(MakePath(dstPath, itemRelPath));

                ConsulDriveInfo.ConsulClient.KV.Put(new KVPair(dstPath) { Value = item.Value, Flags = item.Flags });
            }

            // if we copied everything and this is a move operation, delete the source
            if (deleteSrc)
                ConsulDriveInfo.ConsulClient.KV.DeleteTree(normalSrc);

            // clear the cache since we changed data
            NewCache();
        }

        #endregion NavigationCmdletProviderMethods


        #region Helper Methods

        /// <summary>
        /// Checks if a given path is actually a drive name.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>
        /// True if the path given represents a drive, false otherwise.
        /// </returns>
        private bool PathIsDrive(string path)
        {
            // Remove the drive name and first path separator.  If the 
            // path is reduced to nothing, it is a drive. Also if its
            // just a drive then there wont be any path separators
            if (String.IsNullOrEmpty(
                        path.Replace(this.PSDriveInfo.Root, "")) ||
                String.IsNullOrEmpty(
                        path.Replace(this.PSDriveInfo.Root + PATH_SEPARATOR, ""))

               )
            {
                return true;
            }
            else
            {
                return false;
            }
        } // PathIsDrive

        /// <summary>
        /// Breaks up the path into individual elements.
        /// </summary>
        /// <param name="path">The path to split.</param>
        /// <returns>An array of path segments.</returns>
        private string[] ChunkPath(string path)
        {
            // Normalize the path before splitting
            string normalPath = NormalizePath(path);

            // Return the path with the drive name and first path 
            // separator character removed, split by the path separator.
            string pathNoDrive = normalPath.Replace(this.PSDriveInfo.Root
                                           + PATH_SEPARATOR, "");

            return pathNoDrive.Split(PATH_SEPARATOR.ToCharArray());
        } // ChunkPath

        /// <summary>
        /// Adapts the path, making sure the correct path separator
        /// character is used.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string NormalizePath(string path)
        {
            string result = path;

            if (!String.IsNullOrEmpty(path))
            {
                // this was originally in the samples to replace forward slash with backslash (as filesystem uses backslash)
                // we will adopt it to replace backslash with forward slash (URI) separator.
                result = path.Replace("\\", PATH_SEPARATOR);
            }

            return result;
        } // NormalizePath


        /// <summary>
        /// Ensures that the drive is removed from the specified path
        /// </summary>
        /// 
        /// <param name="path">Path from which drive needs to be removed</param>
        /// <returns>Path with drive information removed</returns>
        private string RemoveDriveFromPath(string path)
        {
            string result = path;
            string root;

            if (this.PSDriveInfo == null)
            {
                root = String.Empty;
            }
            else
            {
                root = this.PSDriveInfo.Root;
            }

            if (result == null)
            {
                result = String.Empty;
            }

            if (result.Contains(root))
            {
                result = result.Substring(result.IndexOf(root, StringComparison.OrdinalIgnoreCase) + root.Length);
            }

            return result;
        }

        private string TrimStartSeparator(string path)
        {
            if (path.StartsWith(PATH_SEPARATOR))
            {
                return path.Substring(1);
            }

            return path;
        }

        private string TrimEndSeparator(string path)
        {
            if (path.EndsWith(PATH_SEPARATOR))
            {
                return path.Substring(0, path.Length - 1);
            }

            return path;
        }

        private string TrimSeparator(string path)
        {
            return TrimStartSeparator(TrimEndSeparator(path));
        }

        private bool IsSamePath(string path1, string path2)
        {
            return TrimSeparator(path1) == TrimSeparator(path2);
        }

        private string MakeKey(string path, string action)
        {
            return ConsulDriveInfo.Root + "::" + path + "::" + action;
        }

        #endregion Helper Methods

    }

    public class ConsulItem
    {
        public ulong CreateIndex;
        public ulong Flags;
        public string Key;
        public byte[] Value;
        public string ValueAsString { get { return Encoding.UTF8.GetString(Value); } }

        public ConsulItem(KVPair kvPair)
        {
            CreateIndex = kvPair.CreateIndex;
            Flags = kvPair.Flags;
            Key = kvPair.Key;
            Value = kvPair.Value;
        }

    }
}
