﻿// Copyright (c) 2007-2014 Thong Nguyen (tumtumtum@gmail.com)

using System;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.Generic;
using Shaolinq.Persistence.Linq.Expressions;
using PropertyPath = Shaolinq.Persistence.Linq.ObjectPath<System.Reflection.PropertyInfo>;

namespace Shaolinq.Persistence.Linq.Optimizers
{
	public  class ReferencedRelatedObjectPropertyGatherer
		: SqlExpressionVisitor
	{
		private bool disableCompare;
		private Expression currentParent;
		private readonly bool forProjection; 
		private readonly DataAccessModel model;
		private readonly ParameterExpression sourceParameterExpression;
		private List<ReferencedRelatedObject> referencedRelatedObjects = new List<ReferencedRelatedObject>();
		private readonly HashSet<IncludedPropertyInfo> includedPropertyInfos = new HashSet<IncludedPropertyInfo>(IncludedPropertyInfoEqualityComparer.Default);
		private readonly Dictionary<PropertyPath, Expression> rootExpressionsByPath = new Dictionary<PropertyPath, Expression>(PropertyPathEqualityComparer.Default);
		private readonly Dictionary<PropertyPath, ReferencedRelatedObject> results = new Dictionary<PropertyPath, ReferencedRelatedObject>(PropertyPathEqualityComparer.Default);
		
		private class DisableCompareContext
			: IDisposable
		{
			private readonly bool savedDisableCompare;
			private readonly ReferencedRelatedObjectPropertyGatherer gatherer;

			public DisableCompareContext(ReferencedRelatedObjectPropertyGatherer gatherer)
			{
				this.gatherer = gatherer;
				savedDisableCompare = gatherer.disableCompare;
				gatherer.disableCompare = true;
			}

			public void Dispose()
			{
				this.gatherer.disableCompare = savedDisableCompare;
			}
		}

		protected IDisposable AcquireDisableCompareContext()
		{
			return new DisableCompareContext(this);
		}
		
		public ReferencedRelatedObjectPropertyGatherer(DataAccessModel model, ParameterExpression sourceParameterExpression, bool forProjection)
		{
			this.model = model;
			this.sourceParameterExpression = sourceParameterExpression;
			this.forProjection = forProjection;
		}

		public static ReferencedRelatedObjectPropertyGathererResults Gather(DataAccessModel model, Expression[] expressions, ParameterExpression sourceParameterExpression, bool forProjection)
		{
			var gatherer = new ReferencedRelatedObjectPropertyGatherer(model, sourceParameterExpression, forProjection);

			var reducedExpressions = expressions.Select(gatherer.Visit).ToArray();

			return new ReferencedRelatedObjectPropertyGathererResults
			{
				ReducedExpressions = reducedExpressions,
				ReferencedRelatedObjectByPath = gatherer.results,
				RootExpressionsByPath = gatherer.rootExpressionsByPath,
				IncludedPropertyInfoByExpression = gatherer
					.includedPropertyInfos
					.GroupBy(c => c.RootExpression)
					.ToDictionary(c => c.Key, c => c.ToList())
			};
		}

		protected override Expression VisitBinary(BinaryExpression binaryExpression)
		{
			using (this.AcquireDisableCompareContext())
			{
				return base.VisitBinary(binaryExpression);
			}
		}

		private int nesting = 0;
		
		protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
		{
			if (methodCallExpression.Method.IsGenericMethod
				&& methodCallExpression.Method.GetGenericMethodDefinition() == MethodInfoFastRef.DataAccessObjectExtensionsIncludeMethod)
			{
				if (!this.forProjection)
				{
					throw new InvalidOperationException();
				}

				var selector = (LambdaExpression)QueryBinder.StripQuotes(methodCallExpression.Arguments[1]);
				var newSelector = ExpressionReplacer.Replace(selector.Body, selector.Parameters[0], methodCallExpression.Arguments[0]);

				var originalReferencedRelatedObjects = referencedRelatedObjects;
				var originalParent = this.currentParent;

				this.currentParent = methodCallExpression.Arguments[0];

				referencedRelatedObjects = new List<ReferencedRelatedObject>();

				nesting++;

				this.Visit(newSelector);

				if (referencedRelatedObjects.Count == 0)
				{
					return this.Visit(methodCallExpression.Arguments[0]);
				}

				var referencedRelatedObject = this.referencedRelatedObjects[0];
				
				this.referencedRelatedObjects = originalReferencedRelatedObjects;
				this.currentParent = originalParent;

				var retval = this.Visit(methodCallExpression.Arguments[0]);

				if (nesting > 1 &&  (retval != sourceParameterExpression) && retval is MemberExpression)
				{
					// For supporting: Select(c => c.Include(d => d.Address.Include(e => e.Region)))

					var prefixProperties = new List<PropertyInfo>();
					var current = (MemberExpression)retval;

					while (current != null)
					{
						if (!current.Member.ReflectedType.IsDataAccessObjectType()
							|| current == this.currentParent)
						{
							break;
						}

						prefixProperties.Add((PropertyInfo)current.Member);
						
						if (current.Expression == sourceParameterExpression)
						{
							break;
						}
						
						current = current.Expression as MemberExpression;
					}

					prefixProperties.Reverse();

					AddIncludedProperty(sourceParameterExpression, referencedRelatedObject, new PropertyPath(prefixProperties));
				}
				else
				{
					AddIncludedProperty(retval, referencedRelatedObject, PropertyPath.Empty);
				}

				nesting--;

				return retval;
			}

			return base.VisitMethodCall(methodCallExpression);
		}

		private void AddIncludedProperty(Expression root, ReferencedRelatedObject referencedRelatedObject, PropertyPath prefixPath)
		{
			for (var i = 0; i < referencedRelatedObject.IncludedPropertyPath.Length + prefixPath.Length; i++)
			{
				var fullAccessPropertyPath = new PropertyPath(referencedRelatedObject.FullAccessPropertyPath.Take(referencedRelatedObject.FullAccessPropertyPath.Length - i));
				var currentPropertyPath = new PropertyPath(prefixPath.Concat(referencedRelatedObject.IncludedPropertyPath.Take(referencedRelatedObject.IncludedPropertyPath.Length - i)));

				var includedPropertyInfo = new IncludedPropertyInfo
				{
					RootExpression = root,
					FullAccessPropertyPath = fullAccessPropertyPath,
					IncludedPropertyPath = currentPropertyPath
				};

				includedPropertyInfos.Add(includedPropertyInfo);
			}
		}

		protected override Expression VisitConditional(ConditionalExpression expression)
		{
			Expression test;

			using (this.AcquireDisableCompareContext())
			{
				test = this.Visit(expression.Test);
			}

			var ifTrue = this.Visit(expression.IfTrue);
			var ifFalse = this.Visit(expression.IfFalse);

			if (test != expression.Test || ifTrue != expression.IfTrue || ifFalse != expression.IfFalse)
			{
				return Expression.Condition(test, ifTrue, ifFalse);
			}
			else
			{
				return expression;
			}
		}

		protected override Expression VisitMemberAccess(MemberExpression memberExpression)
		{
			MemberExpression expression; 
			var visited = new List<MemberExpression>();
			var root = memberExpression.Expression;
			var memberIsDataAccessObjectGatheringForProjection = false;

			if (memberExpression.Type.IsDataAccessObjectType())
			{
				if (forProjection)
				{
					memberIsDataAccessObjectGatheringForProjection = true;

					expression = memberExpression;
				}
				else
				{
					return memberExpression;
				}
			}
			else
			{
				var typeDescriptor = this.model.TypeDescriptorProvider.GetTypeDescriptor(memberExpression.Expression.Type);

				if (typeDescriptor == null)
				{
					return memberExpression;
				}

				var property = typeDescriptor.GetPropertyDescriptorByPropertyName(memberExpression.Member.Name);

				if (property.IsPrimaryKey)
				{
					return memberExpression;
				}

				expression = memberExpression.Expression as MemberExpression;
			}

			var rootTake = 0;
			Expression rootExpression = null;
			var currentExpression = expression;

			while (currentExpression != null && currentExpression.Member is PropertyInfo)
			{
				visited.Add(currentExpression);

				root = currentExpression.Expression;
				currentExpression = root as MemberExpression;
			}

			var includedPathSkip = 0;

			var i = 0;

			foreach (var current in visited)
			{
				if (!current.Member.ReflectedType.IsDataAccessObjectType()
					|| current == currentParent /* @see: Test_Select_Project_Related_Object_And_Include1 */)
				{
					root = current;
					includedPathSkip = visited.Count - i;
					
					break;
				}

				i++;
			}

			visited.Reverse();

			i = 0;
			currentExpression = expression;

			while (currentExpression != null && currentExpression.Member is PropertyInfo)
			{
				var path = new PropertyPath(visited.Select(c=> (PropertyInfo)c.Member).Take(visited.Count - i).ToArray());
				var expressionPath = visited.Take(visited.Count - i).ToArray();

				ReferencedRelatedObject objectInfo;

				if (path.Length == 0)
				{
					break;
				}

				if (!path.Last.ReflectedType.IsDataAccessObjectType())
				{
					rootExpressionsByPath[path] = currentExpression;

					break;
				}

				if (!results.TryGetValue(path, out objectInfo))
				{
					var x = i + includedPathSkip - 1;
					var includedPropertyPath = new PropertyPath(path.Skip(includedPathSkip));
					var objectExpression = x >= 0 ? visited[x] : root;

					objectInfo = new ReferencedRelatedObject(path, includedPropertyPath, objectExpression);

					results[path] = objectInfo;
				}

				referencedRelatedObjects.Add(objectInfo);

				if (memberIsDataAccessObjectGatheringForProjection)
				{
					objectInfo.TargetExpressions.Add(currentExpression);
				}
				else if (currentExpression == expression && memberExpression.Expression is MemberExpression)
				{
					objectInfo.TargetExpressions.Add(memberExpression.Expression);
				}

				i++;
				currentExpression = currentExpression.Expression as MemberExpression;
			}

			return memberExpression;
		}
	}
}
