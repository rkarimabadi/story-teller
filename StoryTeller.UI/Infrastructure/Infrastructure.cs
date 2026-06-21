using Blazored.LocalStorage;
using StoryTeller.UI.Application;
using StoryTeller.UI.Domain;
using System.Text.Json;

namespace StoryTeller.UI.Infrastructure;

#region Models
public class TableStorageModel
{
    public Guid Id { get; set; }
    public int Type { get; set; } // TableType به صورت int ذخیره شود
    public string Name { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new();
}
#endregion Models

#region Services
public class DiceRoller : IDiceRoller
{
    private static readonly Random _random = new Random();

    public DieRoll Roll()
    {
        int value = _random.Next(1, 7);
        return new DieRoll(value);
    }
}

#endregion Services

#region Repositories
public class TableRepository : ITableWriteRepository, ITableReadRepository
{
    private const string StorageKey = "tables";
    private readonly ILocalStorageService _localStorage;
    private List<TableStorageModel>? _cachedTables;

    public TableRepository(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }


    private async Task<List<TableStorageModel>> LoadTablesAsync()
    {
        if (_cachedTables is not null)
            return _cachedTables;

        var json = await _localStorage.GetItemAsync<string>(StorageKey);
        if (string.IsNullOrEmpty(json))
        {
            _cachedTables = new List<TableStorageModel>();
            return _cachedTables;
        }

        _cachedTables = JsonSerializer.Deserialize<List<TableStorageModel>>(json) ?? new List<TableStorageModel>();
        return _cachedTables;
    }

    private async Task SaveTablesAsync(List<TableStorageModel> tables)
    {
        var json = JsonSerializer.Serialize(tables);
        await _localStorage.SetItemAsync(StorageKey, json);
        _cachedTables = tables;
    }


    public async Task<Table?> GetByIdForWriteAsync(Guid id)
    {
        var tables = await LoadTablesAsync();
        var model = tables.FirstOrDefault(t => t.Id == id);
        return model is null ? null : MapToDomain(model);
    }

    public async Task<Table?> GetByTypeForWriteAsync(TableType type)
    {
        var tables = await LoadTablesAsync();
        var model = tables.FirstOrDefault(t => t.Type == (int)type);
        return model is null ? null : MapToDomain(model);
    }

    public async Task AddAsync(Table table)
    {
        var tables = await LoadTablesAsync();
        var model = MapToStorage(table);
        tables.Add(model);
        await SaveTablesAsync(tables);
    }

    public async Task UpdateAsync(Table table)
    {
        var tables = await LoadTablesAsync();
        var existing = tables.FirstOrDefault(t => t.Id == table.Id);
        if (existing is null)
            throw new InvalidOperationException($"جدول با شناسه {table.Id} یافت نشد");

        existing.Type = (int)table.Type;
        existing.Name = table.Name;
        existing.Items = table.Items.ToList();

        await SaveTablesAsync(tables);
    }

    public async Task DeleteAsync(Guid id)
    {
        var tables = await LoadTablesAsync();
        var item = tables.FirstOrDefault(t => t.Id == id);
        if (item is not null)
        {
            tables.Remove(item);
            await SaveTablesAsync(tables);
        }
    }

    public async Task SaveChangesAsync()
    {
        await Task.CompletedTask;
    }

    public async Task<TableReadDto?> GetByIdAsync(Guid id)
    {
        var tables = await LoadTablesAsync();
        var model = tables.FirstOrDefault(t => t.Id == id);
        return model is null ? null : MapToReadDto(model);
    }

    public async Task<TableReadDto?> GetByTypeAsync(TableType type)
    {
        var tables = await LoadTablesAsync();
        var model = tables.FirstOrDefault(t => t.Type == (int)type);
        return model is null ? null : MapToReadDto(model);
    }

    public async Task<IEnumerable<TableReadDto>> GetAllAsync()
    {
        var tables = await LoadTablesAsync();
        return tables.Select(MapToReadDto);
    }


    private Table MapToDomain(TableStorageModel model)
    {
        return new Table(
            id: model.Id,
            type: (TableType)model.Type,
            name: model.Name,
            items: model.Items
        );
    }

    private TableStorageModel MapToStorage(Table domain)
    {
        return new TableStorageModel
        {
            Id = domain.Id,
            Type = (int)domain.Type,
            Name = domain.Name,
            Items = domain.Items.ToList()
        };
    }

    private TableReadDto MapToReadDto(TableStorageModel model)
    {
        return new TableReadDto(
            Id: model.Id,
            Type: ((TableType)model.Type).ToString(),
            Name: model.Name,
            Items: model.Items
        );
    }
}

#endregion Repositories
