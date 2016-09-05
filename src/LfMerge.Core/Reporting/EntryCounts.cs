// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace LfMerge.Core.Reporting
{
	public class EntryCounts {
		public int Added    { get; set; }
		public int Modified { get; set; }
		public int Deleted  { get; set; }

		public EntryCounts()
		{
			this.Added    = 0;
			this.Modified = 0;
			this.Deleted  = 0;
		}

        public void Reset()
        {
            this.Added    = 0;
			this.Modified = 0;
			this.Deleted  = 0;
        }
	}

}

