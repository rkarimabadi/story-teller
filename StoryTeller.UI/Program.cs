using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using StoryTeller.UI;
using StoryTeller.UI.Application;
using StoryTeller.UI.Domain;
using StoryTeller.UI.Infrastructure;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddBlazoredLocalStorage();

builder.Services.AddScoped<IDiceRoller, DiceRoller>();
builder.Services.AddScoped<ITableWriteRepository, TableRepository>();
builder.Services.AddScoped<ITableReadRepository, TableRepository>();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<IMediator, Mediator>();

builder.Services.AddScoped<IQueryHandler<GetAllTablesQuery, IEnumerable<TableReadDto>>, GetAllTablesQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GeneratePromptQuery, PromptTripletDto>, GeneratePromptQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetTableByIdQuery, TableReadDto>, GetTableByIdQueryHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateTableCommand, Unit>, UpdateTableCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SeedDefaultTablesCommand, Unit>, SeedDefaultTablesCommandHandler>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var writeRepo = scope.ServiceProvider.GetRequiredService<ITableWriteRepository>();
    var seeder = new SeedDefaultTablesCommandHandler(writeRepo);
    
    var existing = await writeRepo.GetByTypeForWriteAsync(TableType.Archetype);
    if (existing is null)
    {
        await seeder.HandleAsync(new SeedDefaultTablesCommand());
    }
}

await host.RunAsync();
