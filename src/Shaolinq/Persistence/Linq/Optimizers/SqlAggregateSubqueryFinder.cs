// Copyright (c) 2007-2018 Thong Nguyen (tumtumtum@gmail.com)

using System.Collections.Generic;
using System.Linq.Expressions;
using Shaolinq.Persistence.Linq.Expressions;

namespace Shaolinq.Persistence.Linq.Optimizers
{
	/// <summary>
	/// Finds and returns all aggregates within an expression.
	/// </summary>
	public class SqlAggregateSubqueryFinder
		: SqlExpressionVisitor
	{
		private readonly List<Expression> aggregatesFound;
		
		private SqlAggregateSubqueryFinder()
		{
			this.aggregatesFound = new List<Expression>();
		}

		public static List<Expression> Find(Expression expression)
		{
			var finder = new SqlAggregateSubqueryFinder();

			finder.Visit(expression);

			return finder.aggregatesFound;
		}

		protected override Expression VisitAggregateSubquery(SqlAggregateSubqueryExpression aggregate)
		{
			this.aggregatesFound.Add(aggregate);

			return base.VisitAggregateSubquery(aggregate);
		}
	}
}
