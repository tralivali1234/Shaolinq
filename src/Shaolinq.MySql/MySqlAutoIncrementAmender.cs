﻿// Copyright (c) 2007-2018 Thong Nguyen (tumtumtum@gmail.com)

using System.Linq;
using System.Linq.Expressions;
using Platform;
using Shaolinq.Persistence.Linq;
using Shaolinq.Persistence.Linq.Expressions;

namespace Shaolinq.MySql
{
	public class MySqlAutoIncrementAmender
		: SqlExpressionVisitor
	{
		private MySqlAutoIncrementAmender()
		{
		}

		protected override Expression VisitCreateTable(SqlCreateTableExpression createTableExpression)
		{
			var autoIncrementColumn = createTableExpression
				.ColumnDefinitionExpressions
				.SingleOrDefault(c => c.ConstraintExpressions.Any(d => (d.ConstraintType & ConstraintType.AutoIncrement) != 0));

			if (autoIncrementColumn != null)
			{
				var primaryKeyConstraint = createTableExpression
					.TableConstraints
					.SingleOrDefault(c => (c.ConstraintType & ConstraintType.PrimaryKey) != 0);

				if (primaryKeyConstraint != null)
				{
					if (!primaryKeyConstraint.ColumnNames.Contains(autoIncrementColumn.ColumnName))
					{
						var newPrimaryKeyConstraint = new SqlConstraintExpression(ConstraintType.PrimaryKey, /* TODO: name */ null, new [] { autoIncrementColumn.ColumnName }.ToReadOnlyCollection());
						var newUniqueConstraint = new SqlConstraintExpression(ConstraintType.Unique, /* TODO: name */ null, primaryKeyConstraint.ColumnNames.Concat(autoIncrementColumn.ColumnName).ToReadOnlyCollection());

						return createTableExpression.ChangeConstraints(createTableExpression.TableConstraints.Where(c => c != primaryKeyConstraint).Concat(newPrimaryKeyConstraint).Concat(newUniqueConstraint).ToReadOnlyCollection());
					}
				}
				else
				{
					var newPrimaryKeyConstraint = new SqlConstraintExpression(ConstraintType.PrimaryKey, /* TODO: name */ null, new[] { autoIncrementColumn.ColumnName }.ToReadOnlyCollection());

					return createTableExpression.ChangeConstraints(new [] { newPrimaryKeyConstraint }.ToReadOnlyCollection());
				}
			}

			return base.VisitCreateTable(createTableExpression);
		}

		public static Expression Amend(Expression expression)
		{
			var processor = new MySqlAutoIncrementAmender();

			return processor.Visit(expression);
		}
	}
}
