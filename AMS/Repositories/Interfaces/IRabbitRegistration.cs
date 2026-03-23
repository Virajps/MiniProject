using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Repositories.Models;

namespace Repositories.Services
{
    public class QueueNotificationItem
    {
        public string Id { get; set; } = string.Empty;
        public string QueueType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string RawMessage { get; set; } = string.Empty;
        public string DisplayTime {get;set;}
    }

    public interface IRabbitRegistration
    {
        Task<IConnection> GetConnection();
        Task PublishUserRegistrationAsync(IConnection conn, t_Employee user);
        Task PublishAttendanceEventAsync(IConnection conn, int employeeId, string eventType, object payload);
        Task<List<QueueNotificationItem>> GetRegistrationNotificationsAsync();
        Task<List<QueueNotificationItem>> GetAttendanceNotificationsAsync();
        Task<bool> RemoveNotificationAsync(string notificationId);
        Task<bool> RemoveAllNotificationsAsync();
    }
}
