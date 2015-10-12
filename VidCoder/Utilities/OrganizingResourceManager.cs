using System;
using System.Resources;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VidCoder
{
    public class OrganizingResourceManager : ResourceManager
    {
        /// <summary>
        /// Key is culture, value is the assembly that contains the resources for the culture.
        /// </summary>
        private static readonly Dictionary<string, Assembly> SatelliteAssemblies = new Dictionary<string, Assembly>();

        /// <summary>
        /// Key is culture, value is the set of resource names in the assembly for the culture.
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> ResourceNames = new Dictionary<string, HashSet<string>>();

        /// <summary>
        /// Key is culture, value is the <see cref="ResourceSet"/> for the culture.
        /// </summary>
        private readonly Dictionary<string, ResourceSet> CachedResourceSets = new Dictionary<string, ResourceSet>();

        private Type resourceType;
        private string defaultResourcesNamespace;
        private string translatedResourcesNamespace;

        public OrganizingResourceManager(Type resourceType, string defaultResourcesNamespace, string translatedResourcesNamespace)
        {
            this.translatedResourcesNamespace = translatedResourcesNamespace;
            this.defaultResourcesNamespace = defaultResourcesNamespace;
            this.resourceType = resourceType;
        }

        /// <summary>
        /// Provides the implementation for finding a resource set.
        /// </summary>
        /// <param name="culture">The culture to look for.</param>
        /// <param name="createIfNotExists">true to load the resource set, if it has not been loaded yet; otherwise, false.</param>
        /// <param name="tryParents">true to check parent CultureInfo objects if the resource set cannot be loaded; otherwise, false.</param>
        /// <returns>The specified resource set.</returns>
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
        /// Finds a resource set for the given culture, recursively.
        /// </summary>
        /// <param name="culture">The culture to look for.</param>
        /// <returns>The ResourceSet for the culture.</returns>
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
            Assembly assembly = this.GetAssembly(cultureName);
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
        /// <returns>The resource name for the culture.</returns>
        private string GetResourceName(string cultureName)
        {
            var builder = new StringBuilder();

            if (cultureName == string.Empty)
            {
                builder.Append(this.defaultResourcesNamespace);
            }
            else
            {
                builder.Append(this.translatedResourcesNamespace);
            }

            builder.Append(".");
            builder.Append(this.resourceType.Name);

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
        private Assembly GetAssembly(string cultureName)
        {
            Assembly assembly;
            if (SatelliteAssemblies.TryGetValue(cultureName, out assembly))
            {
                return assembly;
            }

            if (cultureName == string.Empty)
            {
                assembly = this.resourceType.Assembly;
            }
            else
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string assemblyName = Path.GetFileNameWithoutExtension(assemblyLocation) + ".resources.dll";

                string assemblyPath = Path.Combine(
                    Path.GetDirectoryName(assemblyLocation),
                    cultureName,
                    assemblyName);
                if (!File.Exists(assemblyPath))
                {
                    return null;
                }

                assembly = Assembly.LoadFile(assemblyPath);
            }

            SatelliteAssemblies.Add(cultureName, assembly);
            ResourceNames.Add(cultureName, new HashSet<string>(assembly.GetManifestResourceNames()));
            return assembly;
        }
    }
}
