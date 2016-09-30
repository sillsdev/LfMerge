// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;

namespace LfMerge.Core.FieldWorks
{
	/// <summary>
	/// Caching for the FW service locator class. Since the service locator is so slow on Linux,
	/// we will improve it by caching the instances it returns. This is only safe with singletons,
	/// but all the FW services we're looking up this way return singletons, so this will work.
	/// </summary>
	public class FwServiceLocatorCache
	{
		private IFdoServiceLocator fdoServLoc;
		private IDictionary<Type, object> cache;

		public FwServiceLocatorCache(IFdoServiceLocator fwServLoc)
		{
			this.fdoServLoc = fwServLoc;
			this.cache = new Dictionary<Type, object>();
		}

		public FwServiceLocatorCache(FdoCache cache) : this(cache.ServiceLocator) { }

		public T GetInstance<T>() where T : class
		{
			Type TType = typeof(T);
			object result;
			if (cache.TryGetValue(TType, out result))
			{
				return (T)result;
			}
			else
			{
				T singleton = fdoServLoc.GetInstance<T>();
				cache.Add(TType, singleton);
				return singleton;
			}
		}

		// Properties that ServiceLocator offers directly that we might want to simulate

		public IFwMetaDataCacheManaged MetaDataCache { get { return GetInstance<IFwMetaDataCacheManaged>(); } }

		public IWritingSystemManager WritingSystemManager { get { return GetInstance<IWritingSystemManager>(); } }

		public ILgWritingSystemFactory WritingSystemFactory { get { return GetInstance<ILgWritingSystemFactory>(); } }

		public ICmObjectRepository ObjectRepository { get { return GetInstance<ICmObjectRepository>(); } }

		public ILangProject LanguageProject { get { return GetInstance<ILangProjectRepository>().Singleton; } }
	}
}

