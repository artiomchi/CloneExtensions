using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CloneExtensions.ExpressionFactories
{
    class ListExpressionFactory<T> : DeepShallowExpressionFactoryBase<T>
    {
        private static Type _itemType = typeof(T).GetGenericArguments()[0];
        private static Type _type = typeof(T).IsInterface()
            ? typeof(List<>).MakeGenericType(_itemType)
            : typeof(T);
        private static Type _enumeratorType = typeof(IEnumerator<>).MakeGenericType(_itemType);

        private static ConstructorInfo _capacityContructor = _type
            .GetConstructors()
            .Where(x =>
                x.GetParameters().Length == 1 &&
                x.GetParameters().ElementAt(0).ParameterType == typeof(int))
            .FirstOrDefault();

        private static PropertyInfo _countProperty = typeof(ICollection<>).MakeGenericType(_itemType).GetProperty("Count");
        private static MethodInfo _moveNextMethod = typeof(IEnumerator).GetMethod("MoveNext");
        private static MethodInfo _addMethod = typeof(ICollection<>).MakeGenericType(_itemType).GetMethod("Add");
        private static MethodInfo _getEnumeratorMethod = typeof(IEnumerable<>).MakeGenericType(_itemType).GetMethod("GetEnumerator");
        private static PropertyInfo _currentProperty = _enumeratorType.GetProperty("Current");

        private Expression _collectionLength;

        public ListExpressionFactory(
            ParameterExpression source,
            Expression target,
            ParameterExpression flags,
            ParameterExpression initializers,
            ParameterExpression clonedObjects)
            : base(source, target, flags, initializers, clonedObjects)
        {
            _collectionLength = Expression.Property(Source, _countProperty);
        }

        public override bool AddNullCheck
        {
            get { return true; }
        }

        public override bool VerifyIfAlreadyClonedByReference
        {
            get { return false; }
        }

        protected override Expression GetCloneExpression(Func<Type, Expression, Expression> getItemCloneExpression)
        {
            var counter = Expression.Variable(typeof(int));
            var breakLabel = Expression.Label();
            var enumeratorVar = Expression.Variable(_enumeratorType);

            return Expression.Block(
                new[] { counter, enumeratorVar },
                Expression.Assign(Target, Expression.New(_capacityContructor, _collectionLength)),
                GetAddToClonedObjectsExpression(),
                Expression.Assign(counter, Expression.Constant(0)),
                Expression.Assign(enumeratorVar, Expression.Call(Source, _getEnumeratorMethod)),
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Equal(Expression.Call(enumeratorVar, _moveNextMethod), Expression.Constant(true)),
                        Expression.Block(
                            Expression.Call(Target, _addMethod, getItemCloneExpression(_itemType, Expression.Property(enumeratorVar, _currentProperty))),
                            Expression.AddAssign(counter, Expression.Constant(1))
                        ),
                        Expression.Break(breakLabel)
                    ),
                    breakLabel
                )
            );
        }
    }
}