using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Moq;

namespace DbContextMocker
{
    public class MockDbContext<TDbContext> where TDbContext : DbContext
    {
        readonly ICollection<DataSet<TDbContext>> dataSets = new List<DataSet<TDbContext>>();
        
        readonly Mock<TDbContext> mockContext = new Mock<TDbContext>();
        
        public MockDbContext<TDbContext> Add<T>(Expression<Func<TDbContext, IDbSet<T>>> expression, IEnumerable<T> entityData) where T : class
        {
            var dataSet = dataSets.SingleOrDefault(x => x.DataType == typeof(T));
            if (dataSet == null)
            {
                dataSet = new DataSet<TDbContext,T>(expression);
                dataSets.Add(dataSet);
            }
            dataSet.AddData(entityData);
            return this;
        }

        public TDbContext Create()
        {
            ConfigureDataSets();
            var mockedObject = mockContext.Object;
            return mockedObject;
        }

        void ConfigureDataSets()
        {
            foreach (var dataSet in dataSets) ConfigureDataSet(dataSet);
        }

        void ConfigureDataSet(DataSet<TDbContext> dataSet)
        {
            dataSet.ConfigureForignKeys(dataSets);
            var property = dataSet.GetContextProperty();
            dataSet.CreateDbSet(mockContext, property);   
        }
    }

    abstract class DataSet<TDbContext> where TDbContext : DbContext
    {
        public Type DataType { get; }
        
        protected DataSet(Type dataType) => DataType = dataType;

        public abstract void AddData(IEnumerable<object> data);
        public abstract void CreateDbSet(Mock<TDbContext> context, PropertyInfo property);
        public abstract PropertyInfo GetContextProperty();
        public abstract void ConfigureForignKeys(IEnumerable<DataSet<TDbContext>> allDataSets);
        public abstract object GetItemByKey(object value);
        public abstract void AddNavigationCollectionData<TNavigation>(object keyValue, TNavigation dataItem);
    }

    class DataSet<TDbContext, T> : DataSet<TDbContext> where T : class where TDbContext : DbContext
    {
       protected readonly List<T> DataList = new List<T>();
        private Expression<Func<TDbContext, IDbSet<T>>> expression;

        //public Mock<IDbSet<T>> MockDbSet { get; }= new Mock<IDbSet<T>>();

        public DataSet(Expression<Func<TDbContext, IDbSet<T>>> expression) : base(typeof(T)) => this.expression = expression;

        public override void AddData(IEnumerable<object> data)
        {
            DataList.AddRange(data.Where(d => d is T).Cast<T>());
        }
        
        public override void CreateDbSet(Mock<TDbContext> context, PropertyInfo property)
        {
            var queriable = DataList.AsQueryable();
            var mockDbSet = new Mock<IDbSet<T>>();
            mockDbSet.As<IDbAsyncEnumerable<T>>().Setup(m => m.GetAsyncEnumerator()).Returns(new TestDbAsyncEnumerator<T>(DataList.GetEnumerator()));
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestDbAsyncQueryProvider<T>(queriable.Provider));
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queriable.Expression);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queriable.ElementType);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queriable.GetEnumerator());
            var dbSet = mockDbSet.Object;

            context.SetupGet(expression).Returns(dbSet);
        }

        public override PropertyInfo GetContextProperty()
        {
            var property = typeof(TDbContext).GetProperties().First(p => p.PropertyType == typeof(IDbSet<T>));
            return property;
        }

        public override void ConfigureForignKeys(IEnumerable<DataSet<TDbContext>> allDataSets)
        {
            var query = from p in DataType.GetProperties()
                        from a in p.GetCustomAttributes(true)
                        where a is ForeignKeyAttribute
                        select new {Property = p, ForeignKey = (ForeignKeyAttribute) a};
            foreach (var forignKey in query)
            {
                var navigationProperty = DataType.GetProperties().FirstOrDefault(p => p.Name == forignKey.ForeignKey.Name);
                var matchedDataSet = allDataSets.FirstOrDefault(d => d.DataType == navigationProperty.PropertyType);
                if (matchedDataSet == null) continue;
                foreach (var dataItem in DataList)
                {
                    var forignId = forignKey.Property.GetValue(dataItem);
                    navigationProperty.SetValue(dataItem, matchedDataSet.GetItemByKey(forignId));
                    matchedDataSet.AddNavigationCollectionData(forignId, dataItem);
                }
            }
        }

        public override object GetItemByKey(object value)
        {
            var query = from p in DataType.GetProperties()
                        from a in p.GetCustomAttributes(true)
                        where a is KeyAttribute
                        select p;
            var keyProperty = query.FirstOrDefault();
            if (keyProperty == null) return null;
            var dataItem = DataList.FirstOrDefault(i => keyProperty.GetValue(i).Equals(value));
            return dataItem;
        }

        public override void AddNavigationCollectionData<TNavigation>(object keyValue, TNavigation dataItemToAdd)
        {
            var collectionProperty = DataType.GetProperties().FirstOrDefault(f => f.PropertyType == typeof(ICollection<TNavigation>));
            var dataItem = GetItemByKey(keyValue);
            if (collectionProperty == null || dataItem == null) return;
            var collection = collectionProperty.GetValue(dataItem) as List<TNavigation>;
            if (collection == null)
            {
                collection = new List<TNavigation>();
                collectionProperty.SetValue(dataItem, collection);
            }
            collection.Add(dataItemToAdd);
        }
    }

    public static class MockDbContext
    {
        public static MockDbContext<T> For<T>() where T : DbContext => new MockDbContext<T>();
    }
}