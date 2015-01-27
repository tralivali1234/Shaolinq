﻿using System;
using System.Linq.Expressions;
using Shaolinq.Persistence;
using Shaolinq.Persistence.Linq;
using Shaolinq.Persistence.Linq.Expressions;

namespace Shaolinq.SqlServer
{
	public class SqlServerSqlQueryFormatter
		: Sql92QueryFormatter
	{
		public SqlServerSqlQueryFormatter(SqlQueryFormatterOptions options, SqlDialect sqlDialect, SqlDataTypeProvider sqlDataTypeProvider)
			: base(options, sqlDialect, sqlDataTypeProvider)
		{
		}

		protected override FunctionResolveResult ResolveSqlFunction(SqlFunctionCallExpression functionCallExpression)
		{
			var function = functionCallExpression.Function;
			var arguments = functionCallExpression.Arguments;

			switch (function)
			{
				case SqlFunction.ServerUtcNow:
					return new FunctionResolveResult("SYSDATETIME", false, arguments);
				case SqlFunction.ServerNow:
					return new FunctionResolveResult("SYSUTCDATETIME", false, arguments);
				case SqlFunction.DateTimeAddTimeSpan:
					return new FunctionResolveResult("DATEADD", false, Expression.Constant("SECOND"), functionCallExpression.Arguments[1], functionCallExpression.Arguments[0]);
			}

			return base.ResolveSqlFunction(functionCallExpression);
		}


		protected override Expression VisitConstant(ConstantExpression constantExpression)
		{
			if (constantExpression.Value == null)
			{
				return base.VisitConstant(constantExpression);
			}

			var type = constantExpression.Value.GetType();

			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Boolean:
					if (Convert.ToBoolean(constantExpression.Value))
					{
						var trueExpression = Expression.Equal(Expression.Constant(1), Expression.Constant(1));

						this.Visit(trueExpression);

						return constantExpression;
					}
					else
					{
						var falseExpression = Expression.NotEqual(Expression.Constant(1), Expression.Constant(1));

						this.Visit(falseExpression);

						return constantExpression;
					}
					break;
			}

			return base.VisitConstant(constantExpression);
		}

		protected override Expression PreProcess(Expression expression)
		{
			expression = SqlServerLimitAmmender.Ammend(expression);
			expression = SqlServerBooleanNormalizer.Normalize(expression);

			return base.PreProcess(expression);
		}

		protected override void AppendTop(SqlSelectExpression selectExpression)
		{
			if (selectExpression.Take != null && selectExpression.Skip == null)
			{
				this.Write("TOP(");
				this.Visit(selectExpression.Take);
				this.Write(") ");
			}
		}

		protected override void AppendLimit(SqlSelectExpression selectExpression)
		{
			if (selectExpression.Skip != null && selectExpression.Take != null)
			{
				throw new InvalidOperationException("Skip/Take not supported");
			}
		}

		protected override void Write(SqlColumnReferenceAction action)
		{
			if (action == SqlColumnReferenceAction.Restrict)
			{
				this.Write("NO ACTION");

				return;
			}

			base.Write(action);
		}

		protected override Expression VisitOver(SqlOverExpression selectExpression)
		{
			this.Visit(selectExpression.Source);

			this.Write(" OVER (ORDER BY ");

			this.WriteDeliminatedListOfItems<Expression>(selectExpression.OrderBy, c =>
			{
				this.Visit(c);

				if (((SqlOrderByExpression)c).OrderType == OrderType.Descending)
				{
					this.Write(" DESC");
				}

				return c;
			});

			this.Write(")");

			return selectExpression;
		}

		protected override void WriteInsertIntoReturning(SqlInsertIntoExpression expression)
		{
			if (expression.ReturningAutoIncrementColumnNames == null
				|| expression.ReturningAutoIncrementColumnNames.Count == 0)
			{
				return;
			}

			this.Write(" OUTPUT ");
			this.WriteDeliminatedListOfItems<string>(expression.ReturningAutoIncrementColumnNames, c =>
			{
				this.WriteQuotedIdentifier("INSERTED");
				this.Write(".");
				this.WriteQuotedIdentifier(c);

				return null;
			}, ",");
			this.Write("");
		}

		protected override Expression VisitSetCommand(SqlSetCommandExpression expression)
		{
			this.Write("SET ");
			switch (expression.ConfigurationParameter)
			{
			case "IdentityInsert":
				this.Write("IDENTITY_INSERT");
				break;
			default:
				this.Write(expression.ConfigurationParameter);
				break;
			}
			
			if (expression.Target != null)
			{
				this.Write(" ");
				this.Write(((SqlTableExpression)expression.Target).Name);
				this.Write(" ");
			}

			if (expression.ConfigurationParameter == "IdentityInsert")
			{
				this.Write((bool)((ConstantExpression)expression.Arguments[0]).Value ? "ON" : "OFF");
			}
			else
			{
				this.Write(" ");
				this.Write(expression.Arguments);
			}

			this.WriteLine();

			return expression;
		}
	}
}
