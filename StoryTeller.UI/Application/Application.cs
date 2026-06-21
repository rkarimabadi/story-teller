using StoryTeller.UI.Domain;

namespace StoryTeller.UI.Application;

#region Abstractions
public interface IMediator
{
    Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellation = default);
    Task<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellation = default);
}

public class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellation = default)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResponse));
        var handler = _serviceProvider.GetService(handlerType);
        if (handler == null)
            throw new InvalidOperationException($"هیچ هندلری برای کوئری {query.GetType().Name} یافت نشد.");

        var method = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResponse>, TResponse>.HandleAsync));
        if (method == null)
            throw new InvalidOperationException("متد HandleAsync در هندلر یافت نشد.");

        var result = method.Invoke(handler, new object[] { query, cancellation });
        return await (Task<TResponse>)result!;
    }

    public async Task<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellation = default)
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResponse));
        var handler = _serviceProvider.GetService(handlerType);
        if (handler == null)
            throw new InvalidOperationException($"هیچ هندلری برای کامند {command.GetType().Name} یافت نشد.");

        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResponse>, TResponse>.HandleAsync));
        if (method == null)
            throw new InvalidOperationException("متد HandleAsync در هندلر یافت نشد.");

        var result = method.Invoke(handler, new object[] { command, cancellation });
        return await (Task<TResponse>)result!;
    }
}
public interface ICommand<TResponse> { }
public interface IQuery<TResponse> { }
public interface ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellation = default);
}
public interface IQueryHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellation = default);
}
#endregion Abstractions

#region Interfaces
public interface ITableWriteRepository
{
    Task<Table> GetByIdForWriteAsync(Guid id);
    Task<Table> GetByTypeForWriteAsync(TableType type);
    Task AddAsync(Table table);
    Task UpdateAsync(Table table);
    Task DeleteAsync(Guid id);
    Task SaveChangesAsync();
}

public interface ITableReadRepository
{
    Task<TableReadDto> GetByIdAsync(Guid id);
    Task<TableReadDto> GetByTypeAsync(TableType type);
    Task<IEnumerable<TableReadDto>> GetAllAsync();
}
#endregion Interfaces

#region DTOs
public record TableReadDto(
    Guid Id,
    string Type,
    string Name,
    List<string> Items
);

public record PromptTripletDto(
    string Archetype,
    string Situation,
    string Twist
);

#endregion DTOs

#region Commands
public record UpdateTableCommand(
    Guid TableId,
    List<string> NewItems
) : ICommand<Unit>;
public class UpdateTableCommandHandler : ICommandHandler<UpdateTableCommand, Unit>
{
    private readonly ITableWriteRepository _writeRepo;

    public UpdateTableCommandHandler(ITableWriteRepository writeRepo)
    {
        _writeRepo = writeRepo;
    }

    public async Task<Unit> HandleAsync(UpdateTableCommand command, CancellationToken cancellation)
    {
        var table = await _writeRepo.GetByIdForWriteAsync(command.TableId);
        if (table is null)
            throw new ArgumentException("جدول یافت نشد");


        table.SetItems(command.NewItems);

        await _writeRepo.UpdateAsync(table);
        await _writeRepo.SaveChangesAsync();

        return Unit.Value;
    }
}
public sealed class Unit
{
    public static Unit Value { get; } = new Unit();
    private Unit() { }
}

public record SeedDefaultTablesCommand() : ICommand<Unit>;

public class SeedDefaultTablesCommandHandler : ICommandHandler<SeedDefaultTablesCommand, Unit>
{
    private readonly ITableWriteRepository _writeRepo;

    public SeedDefaultTablesCommandHandler(ITableWriteRepository writeRepo)
    {
        _writeRepo = writeRepo;
    }

    public async Task<Unit> HandleAsync(SeedDefaultTablesCommand command, CancellationToken cancellation = default)
    {
        foreach (var kvp in DefaultTableData.Data)
        {
            var tableType = kvp.Key;
            var (name, items) = kvp.Value;

            var existingTable = await _writeRepo.GetByTypeForWriteAsync(tableType);

            if (existingTable is null)
            {
                var newTable = new Table(
                    id: Guid.NewGuid(),
                    type: tableType,
                    name: name,
                    items: items
                );

                await _writeRepo.AddAsync(newTable);
            }
            else
            {
                existingTable.SetItems(items);
                await _writeRepo.UpdateAsync(existingTable);
            }
        }

        await _writeRepo.SaveChangesAsync();

        return Unit.Value;
    }
}
#endregion Commands

#region Queries
public record GetAllTablesQuery() : IQuery<IEnumerable<TableReadDto>>;

public class GetAllTablesQueryHandler : IQueryHandler<GetAllTablesQuery, IEnumerable<TableReadDto>>
{
    private readonly ITableReadRepository _readRepo;

    public GetAllTablesQueryHandler(ITableReadRepository readRepo)
    {
        _readRepo = readRepo;
    }

    public async Task<IEnumerable<TableReadDto>> HandleAsync(GetAllTablesQuery query, CancellationToken cancellation)
    {
        return await _readRepo.GetAllAsync();
    }
}

public record GeneratePromptQuery() : IQuery<PromptTripletDto>;

public class GeneratePromptQueryHandler : IQueryHandler<GeneratePromptQuery, PromptTripletDto>
{
    private readonly ITableReadRepository _readRepo;
    private readonly IDiceRoller _diceRoller;

    public GeneratePromptQueryHandler(ITableReadRepository readRepo, IDiceRoller diceRoller)
    {
        _readRepo = readRepo;
        _diceRoller = diceRoller;
    }

    public async Task<PromptTripletDto> HandleAsync(GeneratePromptQuery query, CancellationToken cancellation)
    {
        var archetypeDto = await _readRepo.GetByTypeAsync(TableType.Archetype);
        var situationDto = await _readRepo.GetByTypeAsync(TableType.Situation);
        var twistDto = await _readRepo.GetByTypeAsync(TableType.Twist);

        var roll1 = _diceRoller.Roll();
        var roll2 = _diceRoller.Roll();
        var roll3 = _diceRoller.Roll();

        var archetype = archetypeDto.Items[roll1.Value - 1];
        var situation = situationDto.Items[roll2.Value - 1];
        var twist = twistDto.Items[roll3.Value - 1];

        return new PromptTripletDto(archetype, situation, twist);
    }
}

public record GetTableByIdQuery(Guid TableId) : IQuery<TableReadDto>;

public class GetTableByIdQueryHandler : IQueryHandler<GetTableByIdQuery, TableReadDto>
{
    private readonly ITableReadRepository _readRepo;

    public GetTableByIdQueryHandler(ITableReadRepository readRepo)
    {
        _readRepo = readRepo;
    }

    public async Task<TableReadDto> HandleAsync(GetTableByIdQuery query, CancellationToken cancellation = default)
    {
        var result = await _readRepo.GetByIdAsync(query.TableId);

        if (result is null)
            throw EntityNotFoundException.ForTable(query.TableId);

        return result;
    }
}
#endregion Queries

#region DefaultTableData
public static class DefaultTableData
{
    public static readonly Dictionary<TableType, (string Name, List<string> Items)> Data = new()
    {
        [TableType.Archetype] = (
            "کهن‌الگوها",
            new List<string>
            {
                "پهلوان",
                "مرشد",
                "ضدقهرمان",
                "یاغی",
                "معصوم",
                "جادوگر"
            }
        ),
        [TableType.Situation] = (
            "موقعیت‌ها",
            new List<string>
            {
                "در آستانه یک تصمیم بزرگ",
                "چیزی را گم کرده",
                "رازی را فهمیده",
                "به کسی خیانت شده",
                "در تبعید است",
                "یک معما باید حل کند"
            }
        ),
        [TableType.Twist] = (
            "موانع/پیچش‌ها",
            new List<string>
            {
                "یک طوفان در راه است",
                "زمان محدود است",
                "دوستش دشمن شده",
                "یک دروغ همه چیز را عوض می‌کند",
                "باید چیزی را قربانی کند",
                "قدرت جدیدی کشف می‌کند"
            }
        )
    };
}
#endregion DefaultTableData

#region Exceptions

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string message) : base(message) { }

    public static EntityNotFoundException ForTable(Guid id) =>
        new($"جدولی با شناسه‌ی '{id}' یافت نشد.");
}

#endregion Exceptions