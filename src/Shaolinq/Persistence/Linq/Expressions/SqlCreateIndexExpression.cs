﻿// Copyright (c) 2007-2018 Thong Nguyen (tumtumtum@gmail.com)

using System.Collections.Generic;
using System.Linq.Expressions;

namespace Shaolinq.Persistence.Linq.Expressions
{
	public class SqlCreateIndexExpression
		: SqlIndexExpressionBase
	{
		public bool Unique { get; }
		public bool IfNotExist { get; }
		public IndexType IndexType { get; }
		public SqlTableExpression Table { get; }
		public Expression Where { get; }
		public bool? Clustered { get; }
		public override ExpressionType NodeType => (ExpressionType)SqlExpressionType.CreateIndex;

		public SqlCreateIndexExpression(string indexName, SqlTableExpression table, bool unique, IndexType indexType, bool ifNotExist, IEnumerable<SqlIndexedColumnExpression> columns, IEnumerable<SqlIndexedColumnExpression> includedColumns)
			: this(indexName, table, unique, indexType, ifNotExist, columns.ToReadOnlyCollection(), includedColumns.ToReadOnlyCollection())
		{
		}

		public SqlCreateIndexExpression(string indexName, SqlTableExpression table, bool unique, IndexType indexType, bool ifNotExist, IReadOnlyList<SqlIndexedColumnExpression> columns, IReadOnlyList<SqlIndexedColumnExpression>  includedColumns, Expression where = null, bool? clustered =  null)
			: base(indexName, columns, includedColumns)
		{
			this.Table = table;
			this.Unique = unique;
			this.IndexType = indexType;
			this.IfNotExist = ifNotExist;
			this.Where = where;
			this.Clustered = clustered;
		}

		public SqlCreateIndexExpression ChangeWhere(Expression where)
		{
			if (where == this.Where)
			{
				return this;
			}

			return new SqlCreateIndexExpression(this.IndexName, this.Table, this.Unique, this.IndexType, this.IfNotExist, this.Columns, this.IncludedColumns, where);
		}
	}
}
