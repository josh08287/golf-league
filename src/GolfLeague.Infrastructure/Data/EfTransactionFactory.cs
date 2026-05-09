using GolfLeague.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GolfLeague.Infrastructure.Data;

/// <summary>
/// Adapts EF Core's <see cref="IDbContextTransaction"/> to the
/// <see cref="IDbTransactionFactory"/> abstraction so the Application
/// layer can wrap a write command in a transaction without taking a
/// direct dependency on EF Core.
/// </summary>
public sealed class EfTransactionFactory : IDbTransactionFactory
{
    private readonly AppDbContext _context;

    public EfTransactionFactory(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IDbTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new EfTransactionScope(tx);
    }

    private sealed class EfTransactionScope : IDbTransactionScope
    {
        private readonly IDbContextTransaction _transaction;
        private bool _completed;

        public EfTransactionScope(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_completed) return;
            await _transaction.CommitAsync(cancellationToken);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_completed) return;
            await _transaction.RollbackAsync(cancellationToken);
            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            await _transaction.DisposeAsync();
        }
    }
}
