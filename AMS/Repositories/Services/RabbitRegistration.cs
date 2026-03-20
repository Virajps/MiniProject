using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Repositories.Interfaces;
using Repositories.Models;

namespace Repositories.Services
{
    public class RabbitRegistration:IRabbitRegistration
    {
        private class PendingQueueMessage
        {
            public ulong DeliveryTag { get; set; }
            public string QueueName { get; set; } = string.Empty;
            public string NotificationType { get; set; } = string.Empty;
        }
        private readonly string HostName;
        private readonly string VirtualHost;
        private readonly string UserName;
        private readonly string Password;
        private readonly int Port;

        private const string RegistrationQueueName = "Registrations";
        private const string AttendanceQueueName = "AttendanceEvents";
        private readonly IAttedanceCacheService _attendanceCacheService;
        private readonly IRedisUserService _redisUserService;
        private static readonly SemaphoreSlim QueueLock = new(1, 1);
        private static readonly ConcurrentDictionary<string, PendingQueueMessage> PendingMessages = new();
        private static IConnection? _adminConnection;
        private static IChannel? _registrationChannel;
        private static IChannel? _attendanceChannel;

        public RabbitRegistration(IConfiguration config,IAttedanceCacheService attedanceCacheService,IRedisUserService redisUserService)
        {
            _attendanceCacheService=attedanceCacheService;
            _redisUserService=redisUserService;
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
                queue: RegistrationQueueName,
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
            routingKey: RegistrationQueueName,
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
        public async Task<List<QueueNotificationItem>> GetRegistrationNotificationsAsync()
        {
            return await ReadQueueMessagesAsync(RegistrationQueueName, "registration");
        }

        public async Task<List<QueueNotificationItem>> GetAttendanceNotificationsAsync()
        {
            return await ReadQueueMessagesAsync(AttendanceQueueName, "attendance");
        }

        public async Task<bool> RemoveNotificationAsync(string notificationId)
        {
            await QueueLock.WaitAsync();
            try
            {
                if (!PendingMessages.TryRemove(notificationId, out var pendingMessage))
                {
                    return false;
                }

                var channel = pendingMessage.QueueName == RegistrationQueueName
                    ? _registrationChannel
                    : _attendanceChannel;

                if (channel == null)
                {
                    return false;
                }

                await channel.BasicAckAsync(pendingMessage.DeliveryTag, multiple: false);
                return true;
            }
            finally
            {
                QueueLock.Release();
            }
        }

        private async Task<List<QueueNotificationItem>> ReadQueueMessagesAsync(string queueName, string notificationType)
        {
            using var connection = await GetConnection();
            using var channel = await connection.CreateChannelAsync();
            var queueState = await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var notifications = new List<QueueNotificationItem>();
            var messageCount = (int)queueState.MessageCount;

            for (var i = 0; i < messageCount; i++)
            {
                var result = await channel.BasicGetAsync(queue: queueName, autoAck: false);
                if (result == null)
                {
                    break;
                }

                var rawMessage = Encoding.UTF8.GetString(result.Body.ToArray());
                notifications.Add(await BuildNotificationAsync(queueName, notificationType, rawMessage));
                await channel.BasicAckAsync(result.DeliveryTag, multiple: false);

                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: queueName,
                    mandatory: false,
                    basicProperties: new BasicProperties
                    {
                        Persistent = true
                    },
                    body: result.Body);
            }

            return notifications;
        }

        private async Task<QueueNotificationItem> BuildNotificationAsync(string queueName, string notificationType, string rawMessage)
        {
            var id = BuildNotificationId(queueName, rawMessage);

            if (notificationType == "registration")
            {
                using var document = JsonDocument.Parse(rawMessage);
                var root = document.RootElement;
                var name = root.TryGetProperty("UserName", out var nameElement) ? nameElement.GetString() : string.Empty;
                var email = root.TryGetProperty("Email", out var emailElement) ? emailElement.GetString() : string.Empty;

                return new QueueNotificationItem
                {
                    Id = id,
                    QueueType = notificationType,
                    Title = "Registration",
                    Message = $"{name} | {email}",
                    RawMessage = rawMessage,
                    DisplayTime = GetDisplayTime(root, "RegisteredAt")
                };
            }

            using (var document = JsonDocument.Parse(rawMessage))
            {
                var root = document.RootElement;
                var employeeId = root.TryGetProperty("EmployeeId", out var employeeElement) ? employeeElement.GetInt32().ToString() : string.Empty;
                var eventType = root.TryGetProperty("EventType", out var eventElement) ? eventElement.GetString() : string.Empty;
                var payload = root.TryGetProperty("Payload", out var payloadElement) ? payloadElement : default;
                var timeText = payload.ValueKind != JsonValueKind.Undefined && payload.TryGetProperty(eventType == "ClockIn" ? "ClockInTime" : "ClockOutTime", out var timeElement)
                    ? timeElement.GetString()
                    : string.Empty;
                var employeeName = employeeId;
                if (int.TryParse(employeeId, out var parsedEmployeeId))
                {
                    var cachedClockIn = await _attendanceCacheService.GetClockInAsync(parsedEmployeeId);
                    if (!string.IsNullOrWhiteSpace(cachedClockIn?.EmployeeName))
                    {
                        employeeName = cachedClockIn.EmployeeName;
                    }
                    else
                    {
                        var cachedAttendanceName = await _attendanceCacheService.GetEmployeeNameAsync(parsedEmployeeId);
                        if (!string.IsNullOrWhiteSpace(cachedAttendanceName))
                        {
                            employeeName = cachedAttendanceName;
                        }
                        else
                        {
                            var cachedUser = await _redisUserService.GetUserByIdAsync(parsedEmployeeId);
                            employeeName = !string.IsNullOrWhiteSpace(cachedUser?.Name) ? cachedUser.Name : $"Employee {employeeId}";
                        }
                    }
                }
                else
                {
                    employeeName = "Employee";
                }

                return new QueueNotificationItem
                {
                    Id = id,
                    QueueType = notificationType,
                    Title = eventType ?? "Attendance",
                    Message = $"{employeeName} | {timeText}",
                    RawMessage = rawMessage,
                     DisplayTime= GetDisplayTime(root, "EventTime")
                };
            }
        }
        private static string GetDisplayTime(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var timeElement))
            {
                return string.Empty;
            }

            var rawValue = timeElement.GetString();
            if (string.IsNullOrWhiteSpace(rawValue) || !DateTime.TryParse(rawValue, out var parsedTime))
            {
                return string.Empty;
            }

            return parsedTime.ToLocalTime().ToString("dd-MM-yyyy hh:mm tt");
        }

        private static string BuildNotificationId(string queueName, string rawMessage)
        {
            return $"{queueName}|{Convert.ToHexString(Encoding.UTF8.GetBytes(rawMessage))}";
        }

        private static string GetQueueNameFromNotificationId(string notificationId)
        {
            var parts = notificationId.Split('|', 2, StringSplitOptions.None);
            return parts.Length == 2 ? parts[0] : string.Empty;
        }

        private static string GetRawMessageFromNotificationId(string notificationId)
        {
            var parts = notificationId.Split('|', 2, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                return string.Empty;
            }

            try
            {
                return Encoding.UTF8.GetString(Convert.FromHexString(parts[1]));
            }
            catch
            {
                return string.Empty;
            }
        }

        // private async Task EnsureAdminChannelsAsync()
        // {
        //     if (_adminConnection != null && _registrationChannel != null && _attendanceChannel != null)
        //     {
        //         return;
        //     }

        //     _adminConnection = await GetConnection();
        //     _registrationChannel = await _adminConnection.CreateChannelAsync();
        //     _attendanceChannel = await _adminConnection.CreateChannelAsync();

        //     await _registrationChannel.QueueDeclareAsync(
        //         queue: RegistrationQueueName,
        //         durable: true,
        //         exclusive: false,
        //         autoDelete: false,
        //         arguments: null);

        //     await _attendanceChannel.QueueDeclareAsync(
        //         queue: AttendanceQueueName,
        //         durable: true,
        //         exclusive: false,
        //         autoDelete: false,
        //         arguments: null);
        // }
    }
 }









       
      
        