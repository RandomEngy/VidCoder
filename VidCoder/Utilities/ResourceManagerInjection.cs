using System;
using System.Linq;
using System.Reflection;
using System.Resources;
using VidCoder.Resources;

namespace VidCoder
{
    public static class ResourceManagerInjection
    {
        private const string DefaultResourcesNamespace = "VidCoder.Resources";
        private const string TranslatedResourcesNamespace = "VidCoder.Resources.Translations";

        public static void InjectResourceManager()
        {
            Assembly assembly = typeof (MainRes).Assembly;

            Type[] assemblyTypes = assembly.GetTypes();

            foreach (Type type in assemblyTypes)
            {
                if (type.Namespace == "VidCoder.Resources")
                {
                    InjectManagerIntoType(new OrganizingResourceManager(type, DefaultResourcesNamespace, TranslatedResourcesNamespace), type);
                }
            }
        }

        private static void InjectManagerIntoType(ResourceManager manager, Type type)
        {
            type
                .GetRuntimeFields()
                .First(m => m.Name == "resourceMan")
                .SetValue(null, manager);
        }
    }
}
