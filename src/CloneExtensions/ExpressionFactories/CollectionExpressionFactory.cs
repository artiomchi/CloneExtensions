using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace CloneExtensions.ExpressionFactories
{
    class CollectionExpressionFactory<T> : DeepShallowExpressionFactoryBase<T>
    {
        private static Type _type = typeof(T);
        private static Type _itemType;

        static CollectionExpressionFactory()
        {
            _itemType = _type.GetGenericArguments()[0];
        }

        private Expression _collectionLength;

        public CollectionExpressionFactory(
            ParameterExpression source,
            Expression target,
            ParameterExpression flags,
            ParameterExpression initializers,
            ParameterExpression clonedObjects)
            : base(source, target, flags, initializers, clonedObjects)
        {
            _collectionLength = Expression.Property(Source, "Count");
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
            var enumeratorVar = Expression.Variable(typeof(IEnumerator<>).MakeGenericType(_itemType));

            var listType = typeof(List<>).MakeGenericType(_itemType);
            var listConstructor = listType
                .GetConstructors()
                .Where(x =>
                    x.GetParameters().Length == 1 &&
                    x.GetParameters().ElementAt(0).ParameterType == typeof(int))
                .FirstOrDefault();

            var moveNextMethod = typeof(IEnumerator).GetMethod("MoveNext");
            var addMethod = typeof(ICollection<>).MakeGenericType(_itemType).GetMethod("Add");

            return Expression.Block(
                new[] { counter, enumeratorVar },
                Expression.Assign(Target, Expression.New(listConstructor, _collectionLength)),
                GetAddToClonedObjectsExpression(),
                Expression.Assign(counter, Expression.Constant(0)),
                Expression.Assign(enumeratorVar, Expression.Call(Source, typeof(IEnumerable<>).MakeGenericType(_itemType).GetMethod("GetEnumerator"))),
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Equal(Expression.Call(enumeratorVar, moveNextMethod), Expression.Constant(true)),
                        Expression.Block(
                            Expression.Call(Target, addMethod, Expression.Property(enumeratorVar, "Current")),
                            Expression.AddAssign(counter, Expression.Constant(1))
                        ),
                        Expression.Break(breakLabel)
                    ),
                    breakLabel
                )
            );
        }

        public override Expression GetShallowCloneExpression() => GetDeepCloneExpression();
    }
}