using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Repositories.Models;

namespace Repositories.Services
{
    public class RabbitRegistration:IRabbitRegistration
    {
        private readonly string HostName;
        private readonly string VirtualHost;
        private readonly string UserName;
        private readonly string Password;
        private readonly int Port;

        private const string QueueName = "Registrations";
        private const string AttendanceQueueName = "AttendanceEvents";

        public RabbitRegistration(IConfiguration config)
        {
            HostName = config["RabbitMQ:HostName"];
            VirtualHost = config["RabbitMQ:VirtualHost"];
            UserName = config["RabbitMQ:UserName"];
            Password = config["RabbitMQ:Password"];
            Port = int.Parse(config["RabbitMQ:Port"] ?? "5672");
        }
        public async Task<IConnection> GetConnection()
        {
            if (string.IsNullOrWhiteSpace(HostName))
            {
                throw new InvalidOperationException("RabbitMQ HostName is not configured. Add RabbitMQ:HostName (or RabbitMQ:Host) in appsettings.json.");
            }

            var factory = new ConnectionFactory
            {
                HostName = HostName,
                VirtualHost = VirtualHost,
                UserName = UserName,
                Password = Password,
                Port = Port
            };

            return await factory.CreateConnectionAsync();
        }
        public async Task PublishUserRegistrationAsync(IConnection conn, t_Employee user)
        {
            using var channel = await conn.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var message = new
            {
                UserName = user.Name,
                Email = user.Email,
                Role = user.Role ?? "Employee",
                RegisteredAt = DateTime.UtcNow,
                Message = $"{user.Name} registered and is waiting for admin review."
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: false,
            basicProperties: properties,
            body: body
        );
        }
        public async Task PublishAttendanceEventAsync(IConnection conn, int employeeId, string eventType, object payload)
        {
            using var channel = await conn.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: AttendanceQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var message = new
            {
                EmployeeId = employeeId,
                EventType = eventType,
                EventTime = DateTime.UtcNow,
                Payload = payload
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: AttendanceQueueName,
                mandatory: false,
                basicProperties: properties,
                body: body
            );
        }
    }
}
