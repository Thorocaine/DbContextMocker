using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Moq;

namespace DbContextMocker
{
    public class MockDbContext<TDbContext> where TDbContext : DbContext
    {
        readonly ICollection<DataSet> dataSets = new List<DataSet>();
        
        readonly Mock<TDbContext> mockContext = new Mock<TDbContext>();
        
        public MockDbContext<TDbContext> Add<T>(IEnumerable<T> entityData, params Action<T>[] confgirations) where T : class
        {
            var dataSet = dataSets.SingleOrDefault(x => x.DataType == typeof(T));
            if (dataSet == null)
            {
                dataSet = new DataSet<T>();
                dataSets.Add(dataSet);
            }
            dataSet.AddData(entityData);
            return this;
        }

        public TDbContext Create()
        {
            ConfigureDataSets();
            return mockContext.Object;
        }

        void ConfigureDataSets()
        {
            foreach (var dataSet in dataSets) ConfigureDataSet(dataSet);
        }

        void ConfigureDataSet(DataSet dataSet)
        {
            var property = dataSet.GetContextProperty<TDbContext>();
            property.SetValue(mockContext.Object, dataSet.CreateDbSet());
        }
    }

    abstract class DataSet
    {
        public Type DataType { get; }
        
        protected DataSet(Type dataType) => DataType = dataType;

        public abstract void AddData(IEnumerable<object> data);
        public abstract object CreateDbSet();
        public abstract PropertyInfo GetContextProperty<TDbContext>();
    }

    class DataSet<T> : DataSet where T : class
    {
        readonly List<T> dataList = new List<T>();
        
        public Mock<IDbSet<T>> MockDbSet { get; }= new Mock<IDbSet<T>>();

        public DataSet() : base(typeof(T)){}

        public override void AddData(IEnumerable<object> data)
        {
            dataList.AddRange(data.Where(d => d is T).Cast<T>());
        }
        
        public override object CreateDbSet()
        {
            var queriable = dataList.AsQueryable();
            MockDbSet.As<IDbAsyncEnumerable<T>>().Setup(m => m.GetAsyncEnumerator()).Returns(new TestDbAsyncEnumerator<T>(dataList.GetEnumerator()));
            MockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestDbAsyncQueryProvider<T>(queriable.Provider));
            MockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queriable.Expression);
            MockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queriable.ElementType);
            MockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queriable.GetEnumerator());
            return MockDbSet.Object;
        }

        public override PropertyInfo GetContextProperty<TDbContext>()
        {
            var property = typeof(TDbContext).GetProperties().First(p => p.PropertyType == typeof(IDbSet<T>));
            return property;
        }
    }

    public static class MockDbContext
    {
        public static MockDbContext<T> For<T>() where T : DbContext => new MockDbContext<T>();
    }
}