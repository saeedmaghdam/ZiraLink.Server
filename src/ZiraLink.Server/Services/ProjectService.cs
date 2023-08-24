﻿using Microsoft.Extensions.Caching.Memory;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IZiraApiClient _ziraApiClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly IModel _channel;

        public ProjectService(IZiraApiClient ziraApiClient, IConfiguration configuration, IMemoryCache memoryCache, IModel channel)
        {
            _ziraApiClient = ziraApiClient;
            _configuration = configuration;
            _memoryCache = memoryCache;
            _channel = channel;
        }

        public Project GetByHost(string host)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentNullException(nameof(host));

            if (!_memoryCache.TryGetValue(host, out Project project)) throw new ApplicationException("Project not found");
            return project;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var queueName = $"api_to_server_external_bus";

            _channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var external_bus_consumer = new AsyncEventingBasicConsumer(_channel);
            external_bus_consumer.Received += async (model, ea) =>
            {
                try
                {
                    await UpdateProjectsAsync(cancellationToken);
                }
                finally
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                await Task.Yield();
            };

            await UpdateProjectsAsync(cancellationToken);
            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: external_bus_consumer);
        }

        private async Task UpdateProjectsAsync(CancellationToken cancellationToken)
        {
            var projects = await _ziraApiClient.GetProjectsAsync(CancellationToken.None);

            var projectDictionary = new Dictionary<string, Project>();
            foreach (var project in projects.Where(x => x.State == Enums.ProjectState.Active))
                _memoryCache.Set(project.GetProjectHost(_configuration), project);
        }
    }
}
