﻿// Copyright (c) 2007-2018 Thong Nguyen (tumtumtum@gmail.com)

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Shaolinq.Persistence.Linq.Expressions
{
	public class ExpressionInterpreter
	{
		protected static readonly object InterpretFailed = new object();

		public static object Interpret(Expression expression)
		{
			var interpreter = new ExpressionInterpreter();

			try
			{
				var result = interpreter.Visit(expression);
				
				if (result == InterpretFailed)
				{
					result = ExpressionFastCompiler.CompileAndRun(expression);
				}

				return result;
			}
			catch (TargetInvocationException e)
			{
				throw e.InnerException;
			}
		}

		protected object Visit(Expression expression)
		{
			switch (expression.NodeType)
			{
			case ExpressionType.New:
				return Visit((NewExpression)expression);
			case ExpressionType.MemberInit:
				return Visit((MemberInitExpression)expression);
			case ExpressionType.Convert:
				return Visit((UnaryExpression)expression);
			case ExpressionType.MemberAccess:
				return Visit((MemberExpression)expression);
			case ExpressionType.Add:
			case ExpressionType.AndAlso:
			case ExpressionType.Or:
			case ExpressionType.OrElse:
			case ExpressionType.Multiply:
			case ExpressionType.Equal:
				return Visit((BinaryExpression)expression);
			case ExpressionType.Call:
				return Visit((MethodCallExpression)expression);
			case ExpressionType.Constant:
				return Visit((ConstantExpression)expression);
			case ExpressionType.Conditional:
				return Visit((ConditionalExpression)expression);
			}

			return InterpretFailed;
		}

		protected object Visit(ConstantExpression expression)
		{
			return expression.Value;
		}

		protected object Visit(MemberInitExpression expression)
		{
			var obj = Visit(expression.NewExpression);

			if (obj == InterpretFailed)
			{
				return obj;
			}

			foreach (var binding in expression.Bindings)
			{
				var value = Visit(obj, binding);

				if (value == InterpretFailed)
				{
					return value;
				}
			}

			return obj;
		}

		protected object Visit(object obj, MemberBinding binding)
		{
			switch (binding.BindingType)
			{
			case MemberBindingType.Assignment:
				var assignment = ((MemberAssignment)binding);

				var value = Visit(assignment.Expression);

				if (value == InterpretFailed)
				{
					return value;
				}

				var fieldInfo = assignment.Member as FieldInfo;

				if (fieldInfo != null)
				{
					fieldInfo.SetValue(obj, value);

					return binding;
				}

				var propertyInfo = assignment.Member as PropertyInfo;

				if (propertyInfo != null)
				{
					propertyInfo.SetValue(obj, value, null);

					return binding;
				}

				return InterpretFailed;
			}

			return InterpretFailed;
		}

		protected object Visit(NewExpression expression)
		{
			// ReSharper disable once ConditionIsAlwaysTrueOrFalse

			if (expression.Constructor == null)
			{
				return Activator.CreateInstance(expression.Type);
			}

			var args = new object[expression.Arguments.Count];
			
			var i = 0;

			foreach (var arg in expression.Arguments)
			{
				var reflected = Visit(arg);

				if (reflected == InterpretFailed)
				{
					return reflected;
				}

				args[i++] = reflected;
			}

			return expression.Constructor.Invoke(args);
		}

		protected object Visit(MethodCallExpression expression)
		{
			var args = new object[expression.Arguments.Count];
			var parentValue = expression.Object != null ? Visit(expression.Object) : null;

			if (parentValue == InterpretFailed)
			{
				return InterpretFailed;
			}

			var i = 0;

			foreach (var arg in expression.Arguments)
			{
				var reflected = Visit(arg);

				if (reflected == InterpretFailed)
				{
					return InterpretFailed;
				}

				args[i++] = reflected;
			}

			return expression.Method.Invoke(parentValue, args);
		}

		protected object Visit(ConditionalExpression expression)
		{
			var result = Visit(expression.Test);

			if (result == InterpretFailed)
			{
				return InterpretFailed;
			}

			if ((bool)result)
			{
				return Visit(expression.IfTrue);
			}
			else
			{
				return Visit(expression.IfFalse);
			}
		}

		protected object Visit(BinaryExpression expression)
		{
			switch (expression.NodeType)
			{
			case ExpressionType.OrElse:
			{
				var left = Visit(expression.Left);

				if ((bool)left)
				{
					return true;
				}

				return (bool)Visit(expression.Right);
			}
			case ExpressionType.AndAlso:
			{
				var left = Visit(expression.Left);

				if (!(bool)left)
				{
					return false;
				}

				return (bool)Visit(expression.Right);
			}
			case ExpressionType.Or:
			{
				var left = (long)Convert.ChangeType(Visit(expression.Left), typeof(long));
				var right = (long)Convert.ChangeType(Visit(expression.Right), typeof(long));

				return Convert.ChangeType(left | right, expression.Type);
			}
			case ExpressionType.And:
			{
				var left = (long)Convert.ChangeType(Visit(expression.Left), typeof(long));
				var right = (long)Convert.ChangeType(Visit(expression.Right), typeof(long));

				return Convert.ChangeType(left & right, expression.Type);
			}
			case ExpressionType.ExclusiveOr:
			{
				var left = (long)Convert.ChangeType(Visit(expression.Left), typeof(long));
				var right = (long)Convert.ChangeType(Visit(expression.Right), typeof(long));

				return Convert.ChangeType(left ^ right, expression.Type);
			}
			case ExpressionType.Equal:
			case ExpressionType.NotEqual:
			{
				if (expression.Type == typeof(bool))
				{
					var left = Visit(expression.Left);

					if (left == InterpretFailed)
					{
						return InterpretFailed;
					}

					var right = Visit(expression.Right);

					if (right == InterpretFailed)
					{
						return InterpretFailed;
					}

					if (left == right)
					{
						return expression.NodeType == ExpressionType.Equal;
					}

					if (left is short && right is short)
					{
						return ((short)left == (short)right) && expression.NodeType == ExpressionType.Equal;
					}
					else if (left is int && right is int)
					{
						return (int)left == (int)right && expression.NodeType == ExpressionType.Equal;
					}
					else if (left is long && right is long)
					{
						return (long)left == (long)right && expression.NodeType == ExpressionType.Equal;
					}
					else if (left is string && right is string)
					{
						return (string)left == (string)right && expression.NodeType == ExpressionType.Equal;
					}
					else if (expression.Left.Type == typeof(object) && expression.Right.Type == typeof(object))
					{
						return EqualityComparer<object>.Default.Equals(left, right);
					}

					return InterpretFailed;
				}
				return InterpretFailed;
			}
			case ExpressionType.Add:
			case ExpressionType.Subtract:
			case ExpressionType.Multiply:
			case ExpressionType.Divide:
			{
				var left = Visit(expression.Left);

				if (left == InterpretFailed)
				{
					return InterpretFailed;
				}

				var right = Visit(expression.Right);

				if (right == InterpretFailed)
				{
					return InterpretFailed;
				}

				Type type = null;

				if (expression.Left.Type == typeof(string) || expression.Right.Type == typeof(string))
				{
					type = typeof(string);

					left = Convert.ChangeType(left, typeof(string));
					right = Convert.ChangeType(right, typeof(string));
				}
				else if (expression.Left.Type == typeof(decimal) || expression.Right.Type == typeof(decimal))
				{
					type = typeof(decimal);

					left = Convert.ChangeType(left, typeof(decimal));
					right = Convert.ChangeType(right, typeof(decimal));
				}
				else if (expression.Left.Type == typeof(double) || expression.Right.Type == typeof(double))
				{
					type = typeof(double);

					left = Convert.ChangeType(left, typeof(double));
					right = Convert.ChangeType(right, typeof(double));
				}
				else if (expression.Left.Type == typeof(float) || expression.Right.Type == typeof(float))
				{
					type = typeof(float);

					left = Convert.ChangeType(left, typeof(float));
					right = Convert.ChangeType(right, typeof(float));
				}
				else if (expression.Left.Type == typeof(long) || expression.Right.Type == typeof(long))
				{
					type = typeof(long);

					left = Convert.ChangeType(left, typeof(long));
					right = Convert.ChangeType(right, typeof(long));
				}
				else if (expression.Left.Type == typeof(uint) || expression.Right.Type == typeof(uint))
				{
					if (expression.Left.Type == typeof(uint) && expression.Right.Type == typeof(uint))
					{
						type = typeof(uint);

						left = Convert.ChangeType(left, typeof(uint));
						right = Convert.ChangeType(right, typeof(uint));
					}
					else
					{
						type = typeof(long);
						
						left = Convert.ChangeType(left, typeof(long));
						right = Convert.ChangeType(right, typeof(long));
					}
				}
				else if (expression.Left.Type == typeof(int) || expression.Right.Type == typeof(int))
				{
					type = typeof(int);

					left = Convert.ChangeType(left, typeof(int));
					right = Convert.ChangeType(right, typeof(int));
				}
				else if (expression.Left.Type == typeof(ushort) || expression.Right.Type == typeof(ushort))
				{
					if (expression.Left.Type == typeof(ushort) && expression.Right.Type == typeof(ushort))
					{
						type = typeof(ushort);

						left = Convert.ChangeType(left, typeof(ushort));
						right = Convert.ChangeType(right, typeof(ushort));
					}
					else
					{
						type = typeof(int);

						left = Convert.ChangeType(left, typeof(int));
						right = Convert.ChangeType(right, typeof(int));
					}
				}
				else if (expression.Left.Type == typeof(short) || expression.Right.Type == typeof(short))
				{
					type = typeof(short);

					left = Convert.ChangeType(left, typeof(short));
					right = Convert.ChangeType(right, typeof(short));
				}
				else if (expression.Left.Type == typeof(byte) || expression.Right.Type == typeof(byte))
				{
					type = typeof(byte);

					left = Convert.ChangeType(left, typeof(byte));
					right = Convert.ChangeType(right, typeof(byte));
				}
				else if (expression.Left.Type == typeof(sbyte) || expression.Right.Type == typeof(sbyte))
				{
					type = typeof(int);

					left = Convert.ChangeType(left, typeof(int));
					right = Convert.ChangeType(right, typeof(int));
				}
				else if (expression.Left.Type == typeof(byte) || expression.Right.Type == typeof(byte))
				{
					type = typeof(int);

					left = Convert.ChangeType(left, typeof(int));
					right = Convert.ChangeType(right, typeof(int));
				}

				if (type == null)
				{
					return InterpretFailed;
				}

				Func<object, object, object> func = null;

				switch (expression.NodeType)
				{
				case ExpressionType.Add:
					func = BinaryOperations.GetAddFunc(type);
					break;
				case ExpressionType.Subtract:
					func = BinaryOperations.GetSubtractFunc(type);
					break;
				case ExpressionType.Multiply:
					func = BinaryOperations.GetMultiplyFunc(type);
					break;
				case ExpressionType.Divide:
					func = BinaryOperations.GetDivideFunc(type);
					break;
				}

				if (func != null)
				{
					return func(left, right);
				}

				return InterpretFailed;
			}
			default:
				return InterpretFailed;
			}
		}

		protected object Visit(MemberExpression expression)
		{
			var parentValue = expression.Expression != null ? Visit(expression.Expression) : null;

			if (parentValue == InterpretFailed)
			{
				return expression;
			}

			var fieldInfo = expression.Member as FieldInfo;

			if (fieldInfo != null)
			{
				return fieldInfo.GetValue(parentValue);
			}

			var propertyInfo = expression.Member as PropertyInfo;

			if (propertyInfo != null)
			{
				return propertyInfo.GetValue(parentValue, null);
			}

			return InterpretFailed;
		}

		protected object Visit(UnaryExpression expression)
		{
			if (expression.Method != null)
			{
				return InterpretFailed;
			}

			if (expression.NodeType != ExpressionType.Convert)
			{
				return InterpretFailed;
			}

			var result = Visit(expression.Operand);

			if (result == InterpretFailed)
			{
				return InterpretFailed;
			}

			if (result == null)
			{
				return null;
			}

			var converter = System.ComponentModel.TypeDescriptor.GetConverter(expression.Operand.Type);

			if (converter.CanConvertTo(expression.Type))
			{
				return converter.ConvertTo(result, expression.Type);
			}

			return InterpretFailed;
		}
	}
}
