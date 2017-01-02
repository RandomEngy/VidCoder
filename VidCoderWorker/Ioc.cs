using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Practices.Unity;
using VidCoderCommon.Services;

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
