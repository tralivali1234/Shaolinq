﻿// Copyright (c) 2007-2018 Thong Nguyen (tumtumtum@gmail.com)
namespace Shaolinq.Persistence.Linq.Expressions
{
	public enum SqlExpressionType
	{
		First = 8192,
		Table,
		Select,
		Projection,
		Delete,
		Where, // 7
		Limit,
		Column, // 9
		FunctionCall,
		OrderBy,
		Join,
		Aggregate,
		AggregateSubquery,
		Subquery,
		ConstantPlaceholder,
		Tuple,
		Type,
		AlterTable,
		ConstraintAction,
		ColumnDefinition,
		CreateTable,
		CreateType, 
		CreateIndex,
		OrganizationIndex,
		IndexedColumn,
		EnumDefinition,
		Constraint,
		References,
		StatementList,
		InsertInto,
		TableHint,
		Update,
		Assign,
		Pragma,
		ObjectReference,
		SetCommand,
		TableOption,
		Over,
		Scalar,
		Exists,
		In,
		QueryArgument,
		Union,
		Keyword,
		VariableDeclaration,
		Declare
	}
}
