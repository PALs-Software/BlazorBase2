using System.Linq.Expressions;
using AppTemplate.Shared.Modules.Notes.Entities;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;

namespace AppTemplate.Tests.Infrastructure;

public sealed class StubNoteDataProvider : IBaseDataProvider<Note>
{
    private readonly List<Note> Notes;

    public StubNoteDataProvider(params Note[] notes)
    {
        Notes = [.. notes];
    }

    public Task<BaseQueryResult<Note>> GetListAsync(BaseQuery query, Expression<Func<Note, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new BaseQueryResult<Note> { Items = [.. Notes], TotalCount = Notes.Count });

    public Task<Note?> GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<Note, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Notes.FirstOrDefault(note => note.Id == Guid.Parse(id.ToString()!)));

    public Task<int> GetCountAsync(Expression<Func<Note, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Notes.Count);

    public Task<Note> CreateAsync(Note model, CancellationToken cancellationToken = default)
    {
        Notes.Add(model);
        return Task.FromResult(model);
    }

    public Task<Note> PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Notes.FirstOrDefault(note => note.Id == Guid.Parse(id.ToString()!)) ?? new Note());

    public Task DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        Notes.RemoveAll(note => note.Id == Guid.Parse(id.ToString()!));
        return Task.CompletedTask;
    }
}
