using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using DbContextMocker;
using Xunit;

namespace DbContextMockerTests
{
    public class MockDbContextTests : IDisposable
    {
        readonly Person[] people =
        {
            new Person { Id = 1, Name = "Jonathan" },
            new Person { Id = 2, Name = "David" }
        };
        readonly Dog[] dogs =
        {
            new Dog{Id = 1, Name = "AA", PersonId = 1},
            new Dog{Id = 2, Name = "BB", PersonId = 2},
            new Dog{Id = 3, Name = "CC", PersonId = 1},
            new Dog{Id = 4, Name = "DD", PersonId = 2},
        };

        readonly TestDbContext db;

        public MockDbContextTests() => db = MockDbContext.For<TestDbContext>()
            .Add(people, p => p.Dogs = dogs.Where(d => d.PersonId == p.Id).ToList())
            .Add(dogs, d => d.Person = people.FirstOrDefault(p => p.Id == d.PersonId))
            .Create();

        [Fact]
        public void GetArrays()
        {
            Assert.Equal(people, db.Persons.ToArray());
            Assert.Equal(dogs, db.Dogs.ToArray());
        }

        [Fact]
        public void TestSelect()
        {
            var array = db.Persons.Select(p => p).ToArray();
            Assert.Equal(people, array);
        }

        [Fact]
        public async Task TestSelectAsync()
        {
            var array = await db.Persons.ToArrayAsync();
            Assert.Equal(people, array);
        }

        [Fact]
        public void TestWhereQuery()
        {
            var query = from d in db.Dogs
                        where d.PersonId == 1
                        select d;
            var array = query.ToArray();
            Assert.Equal(new[] { 1, 3 }, array.Select(d => d.Id));
        }

        [Fact]
        public void TestJoin()
        {
            var query = from p in db.Persons
                        where p.Id == 1
                        join d in db.Dogs on p.Id equals d.PersonId
                        select d.Name;
            var array = query.ToArray();
            Assert.Equal(new[] { "AA", "CC" }, array);
        }

        [Fact]
        public void NavigationJoin()
        {
            var query = from p in db.Persons
                        where p.Id == 1
                        from d in p.Dogs
                        select d.Name;
            var array = query.ToArray();
            Assert.Equal(new[] { "AA", "CC" }, array);
        }

        [Fact]
        public async Task NavigateBack()
        {
            var query = from d in db.Dogs
                        where d.Id == 4
                        select d.Person.Id;
            var personId = await query.SingleAsync();
            Assert.Equal(2, personId);
        }

        public void Dispose()
        {
            db?.Dispose();
        }
    }

    public class TestDbContext : DbContext
    {
        public IDbSet<Dog> Dogs { get; set; }
        public IDbSet<Person> Persons { get; set; }
    }

    public class Person
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<Dog> Dogs;
    }

    public class Dog
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }

        [ForeignKey(nameof(Person))]
        public int PersonId { get; set; }

        public Person Person;
    }
}