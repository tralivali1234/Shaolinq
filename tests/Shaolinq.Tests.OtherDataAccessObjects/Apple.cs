﻿// Copyright (c) 2007-2018 Thong Nguyen (tumtumtum@gmail.com)

using Platform.Validation;

namespace Shaolinq.Tests.OtherDataAccessObjects
{
	[DataAccessObject]
	public abstract class Apple
		: Fruit
	{
		[PersistedMember, DefaultValue(0)]
		public abstract float Quality { get; set; }
	}
}
