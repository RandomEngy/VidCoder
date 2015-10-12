using System;
using System.Resources;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VidCoder.Resources;

namespace VidCoder
{
    public class VidCoderResourceManager : ResourceManager
    {
        private static readonly Dictionary<string, Assembly> SatelliteAssemblies = new Dictionary<string, Assembly>();
        private static readonly Dictionary<string, HashSet<string>> ResourceNames = new Dictionary<string, HashSet<string>>();

        private readonly Dictionary<string, ResourceSet> CachedResourceSets = new Dictionary<string, ResourceSet>();

        private string name;

        public VidCoderResourceManager(string name)
        {
            this.name = name;
        }

        protected override ResourceSet InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
        {
            ResourceSet result;
            if (this.CachedResourceSets.TryGetValue(culture.Name, out result))
            {
                return result;
            }

            if (tryParents)
            {
                result = this.GetResourceSetRecursive(culture);
            }
            else
            {
                result = this.LoadResourceSet(culture.Name);
            }

            this.CachedResourceSets.Add(culture.Name, result);

            return result;
        }

        /// <summary>
        /// Finds a resource set for the given culture
        /// </summary>
        /// <param name="culture"></param>
        /// <returns></returns>
        private ResourceSet GetResourceSetRecursive(CultureInfo culture)
        {
            ResourceSet resourceSet = this.LoadResourceSet(culture.Name);
            if (resourceSet == null && culture.Name != string.Empty)
            {
                return this.GetResourceSetRecursive(culture.Parent);
            }
            else
            {
                return resourceSet;
            }
        }

        /// <summary>
        /// Loads in a ResourceSet for a culture.
        /// </summary>
        /// <param name="cultureName">The culture's name.</param>
        /// <returns>The ResourceSet or null if no resources with that culture could be found.</returns>
        private ResourceSet LoadResourceSet(string cultureName)
        {
            Assembly assembly = GetAssembly(cultureName);
            if (assembly == null)
            {
                // No satellite assembly found.
                return null;
            }
            else
            {
                string resourcePath = this.GetResourceName(cultureName);

                if (ResourceNames[cultureName].Contains(resourcePath))
                {
                    using (var stream = assembly.GetManifestResourceStream(resourcePath))
                    {
                        return new ResourceSet(stream);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the resource name for the given culture.
        /// </summary>
        /// <param name="cultureName">The culture's name.</param>
        /// <returns></returns>
        private string GetResourceName(string cultureName)
        {
            var builder = new StringBuilder("VidCoder.Resources.");
            if (cultureName != string.Empty)
            {
                builder.Append("Translations.");
            }

            builder.Append(this.name);

            if (cultureName != string.Empty)
            {
                builder.Append(".");
                builder.Append(cultureName);
            }

            builder.Append(".resources");

            return builder.ToString();
        }

        /// <summary>
        /// Gets the resource assembly for a culture.
        /// </summary>
        /// <param name="cultureName">The culture's name.</param>
        /// <returns>The assembly for the culture or null if no assembly could be found. Returns a satellite assembly if a
        /// specific culture or the main assembly if an invariant culture.</returns>
        private static Assembly GetAssembly(string cultureName)
        {
            Assembly assembly;
            if (SatelliteAssemblies.TryGetValue(cultureName, out assembly))
            {
                return assembly;
            }

            if (cultureName == string.Empty)
            {
                assembly = (typeof (MainRes)).Assembly;
            }
            else
            {
                string satelliteAssemblyPath = Path.Combine(Utilities.ProgramFolder, cultureName, "VidCoder.resources.dll");
                if (!File.Exists(satelliteAssemblyPath))
                {
                    return null;
                }

                assembly = Assembly.LoadFile(satelliteAssemblyPath);
            }

            SatelliteAssemblies.Add(cultureName, assembly);
            ResourceNames.Add(cultureName, new HashSet<string>(assembly.GetManifestResourceNames()));
            return assembly;
        }
    }
}
