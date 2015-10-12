using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Resources;

namespace VidCoder
{
    public static class ResourceManagerInjection
    {
        public static void InjectResourceManager()
        {
            Assembly assembly = typeof (MainRes).Assembly;

            Type[] assemblyTypes = assembly.GetTypes();

            foreach (Type type in assemblyTypes)
            {
                if (type.Namespace == "VidCoder.Resources")
                {
                    type
                        .GetRuntimeFields()
                        .First(m => m.Name == "resourceMan")
                        .SetValue(
                            null,
                            new VidCoderResourceManager(type.Name));
                }
            }
        }
    }
}
