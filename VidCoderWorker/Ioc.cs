using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonServiceLocator;
using Unity;
using Unity.Lifetime;
using Unity.ServiceLocation;

namespace VidCoderWorker
{
	public static class Ioc
	{
		static Ioc()
		{
			Container = new UnityContainer();

			ServiceLocator.SetLocatorProvider(() => new UnityServiceLocator(Container));
		}

		public static UnityContainer Container { get; set; }

		public static T Get<T>()
		{
			return Container.Resolve<T>();
		}

		public static object Get(Type type)
		{
			return Container.Resolve(type);
		}

		public static ContainerControlledLifetimeManager Singleton => new ContainerControlledLifetimeManager();
	}
}
