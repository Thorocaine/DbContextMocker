using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Moq;

namespace DbContextMocker
{
    public class MockDbContext<TDbContext> where TDbContext : DbContext
    {
        readonly Mock<TDbContext> mockContext = new Mock<TDbContext>();
        
        public MockDbContext<TDbContext> Add<T>(IEnumerable<T> entityData, params Action<T>[] confgirations) where T : class
        {
            var list = entityData.ToList();
            foreach (var action in confgirations)
                list.ForEach(action);

            var data = list.AsQueryable();
            var mockSet = new Mock<IDbSet<T>>();
            mockSet.As<IDbAsyncEnumerable<T>>().Setup(m => m.GetAsyncEnumerator()).Returns(new TestDbAsyncEnumerator<T>(data.GetEnumerator()));
            mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestDbAsyncQueryProvider<T>(data.Provider));
            mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(data.Expression);
            mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(data.ElementType);
            mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(data.GetEnumerator());
            var property = typeof(TDbContext).GetProperties().First(p => p.PropertyType == typeof(IDbSet<T>));
            property.SetValue(mockContext.Object, mockSet.Object);
            
            return this;
        }

        public TDbContext Create() => mockContext.Object;

        
    }

    public static class MockDbContext
    {
        public static MockDbContext<T> For<T>() where T : DbContext => new MockDbContext<T>();
    }
}