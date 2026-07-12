using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Services
{
    public class QdrantInitializationHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public QdrantInitializationHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
            await vectorStore.EnsureCollectionsAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
