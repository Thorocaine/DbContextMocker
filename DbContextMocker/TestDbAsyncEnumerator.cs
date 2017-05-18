using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Threading;
using System.Threading.Tasks;

namespace DbContextMocker
{
    class TestDbAsyncEnumerator<T> : IDbAsyncEnumerator<T>
    {
        readonly IEnumerator<T> inner;

        public TestDbAsyncEnumerator(IEnumerator<T> inner) => this.inner = inner;

        public void Dispose()
        {
            inner.Dispose();
        }

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken) => Task.FromResult(inner.MoveNext());

        public T Current => inner.Current;

        object IDbAsyncEnumerator.Current => Current;
    }
}