﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
            dataSet.ConfigureForignKeys(dataSets);
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
        public abstract void ConfigureForignKeys(IEnumerable<DataSet> allDataSets);
        public abstract object GetItemByKey(object value);
        public abstract void AddNavigationCollectionData<N>(object keyValue, N dataItem);
    }

    class DataSet<T> : DataSet where T : class
    {
       protected readonly List<T> dataList = new List<T>();
        
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

        public override void ConfigureForignKeys(IEnumerable<DataSet> allDataSets)
        {
            var query = from p in DataType.GetProperties()
                        from a in p.GetCustomAttributes(true)
                        where a is ForeignKeyAttribute
                        select new {Property = p, ForeignKey = (ForeignKeyAttribute) a};
            foreach (var forignKey in query)
            {
                var navigationField = DataType.GetFields().FirstOrDefault(p => p.Name == forignKey.ForeignKey.Name);
                var matchedDataSet = allDataSets.FirstOrDefault(d => d.DataType == navigationField.FieldType);
                if (matchedDataSet == null) continue;
                foreach (var dataItem in dataList)
                {
                    var forignId = forignKey.Property.GetValue(dataItem);
                    navigationField.SetValue(dataItem, matchedDataSet.GetItemByKey(forignId));
                    matchedDataSet.AddNavigationCollectionData<T>(forignId, dataItem);
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
            var dataItem = dataList.FirstOrDefault(i => keyProperty.GetValue(i).Equals(value));
            return dataItem;
        }

        public override void AddNavigationCollectionData<N>(object keyValue, N dataItemToAdd)
        {
            var collectionField = DataType.GetFields().FirstOrDefault(f => f.FieldType == typeof(ICollection<N>));
            var dataItem = GetItemByKey(keyValue);
            if (collectionField == null || dataItem == null) return;
            var collection = collectionField.GetValue(dataItem) as List<N>;
            if (collection == null)
            {
                collection = new List<N>();
                collectionField.SetValue(dataItem, collection);
            }
            collection.Add(dataItemToAdd);
        }
    }

    public static class MockDbContext
    {
        public static MockDbContext<T> For<T>() where T : DbContext => new MockDbContext<T>();
    }
}