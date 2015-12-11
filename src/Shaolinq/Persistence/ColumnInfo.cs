﻿// Copyright (c) 2007-2015 Thong Nguyen (tumtumtum@gmail.com)

using System;
using System.Linq;

namespace Shaolinq.Persistence
{
	public struct ColumnInfo
	{
		public TypeDescriptor ForeignType { get; set; }
		public PropertyDescriptor[] VisitedProperties { get; set; }
		public PropertyDescriptor DefinitionProperty { get; set; }
		
		private string columnName;
		private string tailColumnName;
		private string fullParentName;
		private string fullPropertyName;

		public string ColumnName => this.GetColumnName();

		public string GetTailColumnName()
		{
			if (this.VisitedProperties.Length == 0)
			{
				throw new InvalidOperationException();
			}

			if (this.tailColumnName == null)
			{
				this.tailColumnName = string.Concat(this.VisitedProperties.Skip(1).Select(c => c.PersistedPrefixName));

				if (this.VisitedProperties.Length == 1)
				{
					this.tailColumnName += this.DefinitionProperty.PersistedName;
				}
				else
				{
					this.tailColumnName += this.DefinitionProperty.PersistedShortName;
				}
			}

			return this.tailColumnName;
		}

		public string GetColumnName()
		{
			if (this.columnName != null)
			{
				return this.columnName;
			}

			if (this.VisitedProperties.Length == 0)
			{
				this.columnName = this.DefinitionProperty.PersistedName;
			}
			else
			{
				this.columnName = string.Concat(this.VisitedProperties.Select(c => c.PersistedPrefixName));
				this.columnName += this.DefinitionProperty.PersistedShortName;
			}

			return this.columnName;
		}

		public string GetFullParentName()
		{
			return this.fullParentName ?? (this.fullParentName = string.Join(".", this.VisitedProperties.Select(c => c.PropertyName)));
		}

		public string GetFullPropertyName()
		{
			return this.fullPropertyName ?? (this.fullPropertyName = string.Join(".", this.VisitedProperties.Select(c => c.PropertyName).Concat(new[] { this.DefinitionProperty.PropertyName })));
		}

		public override string ToString()
		{
			return this.ColumnName;
		}
	}
}
